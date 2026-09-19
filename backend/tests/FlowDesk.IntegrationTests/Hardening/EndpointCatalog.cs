using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Application.Customers.CreateCustomer;
using FlowDesk.Application.Customers.UpdateCustomer;
using FlowDesk.Application.Tasks.ChangeTaskStatus;
using FlowDesk.Application.Tasks.CreateTask;
using FlowDesk.Application.Tasks.UpdateTask;
using FlowDesk.Application.Team.InviteMember;
using FlowDesk.Application.Tenancy.UpdateWorkspace;
using FlowDesk.Application.Tickets.AddTicketComment;
using FlowDesk.Application.Tickets.AssignTicket;
using FlowDesk.Application.Tickets.ChangeTicketStatus;
using FlowDesk.Application.Tickets.CreateTicket;
using FlowDesk.Application.Tickets.UpdateTicket;
using FlowDesk.Domain.Customers;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using FlowDesk.IntegrationTests.Support;
using Microsoft.AspNetCore.Http;

namespace FlowDesk.IntegrationTests.Hardening;

/// <summary>
/// A request against one workspace-scoped endpoint, with the path written
/// relative to <c>/api/workspaces/{slug}</c>.
/// </summary>
/// <remarks>
/// The slug is left out on purpose. The tenant boundary tests arrange a request
/// in one workspace and send it under another's slug, and that is only possible
/// if the two are joined at send time rather than baked in here.
/// </remarks>
internal sealed record ScopedRequest(HttpMethod Method, string Path, HttpContent? Body = null)
{
    public HttpRequestMessage ToMessage(string slug) =>
        new(Method, new Uri($"/api/workspaces/{slug}{Path}", UriKind.Relative)) { Content = Body };
}

/// <summary>
/// One workspace-scoped endpoint: who may call it, what success looks like and
/// how to build a request that would succeed for someone allowed to make it.
/// </summary>
/// <param name="Template">
/// The route as ASP.NET Core reports it, constraints removed. The coverage test
/// compares this with the endpoints the running application actually exposes.
/// </param>
/// <param name="MinimumRole">
/// The least authoritative role that may call the endpoint. Written out here
/// rather than read from <c>WorkspacePermissions</c>: a test that derived its
/// expectations from the code under test would agree with any mistake in it.
/// </param>
/// <param name="Arrange">
/// Creates whatever the request needs — a fresh ticket to delete, a pending
/// invitation to revoke — as the owner, so each case starts from its own
/// resources and cannot be disturbed by another case having deleted them.
/// </param>
internal sealed record EndpointCase(
    string Method,
    string Template,
    MembershipRole MinimumRole,
    HttpStatusCode Success,
    Func<EndpointScene, Task<ScopedRequest>> Arrange)
{
    /// <summary>
    /// Whether the path names a specific record. Only those can be tried with a
    /// record from another workspace.
    /// </summary>
    public bool NamesARecord => Template.Contains("Id}", StringComparison.Ordinal);

    public bool IsWrite => Method != HttpMethods.Get;

    public override string ToString() => $"{Method} {Template}";
}

/// <summary>
/// A workspace prepared for the endpoint catalog: an owner, one customer and a
/// bystander member whose role and membership the team endpoints can change.
/// </summary>
internal sealed class EndpointScene
{
    private readonly CancellationToken _cancellationToken;

    private EndpointScene(
        HttpClient ownerClient,
        string slug,
        Guid customerId,
        Guid bystanderId,
        CancellationToken cancellationToken)
    {
        OwnerClient = ownerClient;
        Slug = slug;
        CustomerId = customerId;
        BystanderId = bystanderId;
        _cancellationToken = cancellationToken;
    }

    public HttpClient OwnerClient { get; }

    public string Slug { get; }

    public Guid CustomerId { get; }

    /// <summary>
    /// A member nobody in the test signs in as. Role changes and removals are
    /// aimed at them, because aiming them at the caller or the owner would test
    /// the self-service and last-owner rules instead of the permission matrix.
    /// </summary>
    public Guid BystanderId { get; }

    public static Task<EndpointScene> CreateAsync(
        FlowDeskApiFactory factory,
        TestWorkspace workspace,
        CancellationToken cancellationToken) =>
        CreateAsync(factory, workspace.Client, workspace.Slug, workspace.CustomerId, cancellationToken);

    public static async Task<EndpointScene> CreateAsync(
        FlowDeskApiFactory factory,
        HttpClient ownerClient,
        string slug,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        using var bystander = await TeamTestClient.AddMemberAsync(
            factory, ownerClient, slug, MembershipRole.Agent, cancellationToken,
            displayName: "Zeynep Arslan");

        return new EndpointScene(ownerClient, slug, customerId, bystander.Id, cancellationToken);
    }

    public async Task<TicketDetailResponse> NewTicketAsync() =>
        await TicketTestClient.CreateAndReadAsync(OwnerClient, Slug, CustomerId, _cancellationToken);

    public async Task<TaskDetailResponse> NewTaskAsync() =>
        await TaskTestClient.CreateAndReadAsync(OwnerClient, Slug, _cancellationToken);

    public async Task<Guid> NewCustomerAsync() =>
        (await CustomerTestClient.CreateAndReadAsync(
            OwnerClient, Slug, "Yıldız Lojistik", _cancellationToken)).Id;

    public async Task<Guid> NewArchivedCustomerAsync()
    {
        var customerId = await NewCustomerAsync();

        using var archived = await CustomerTestClient.ArchiveAsync(
            OwnerClient, Slug, customerId, _cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, archived.StatusCode);

        return customerId;
    }

    public async Task<Guid> NewInvitationAsync() =>
        (await TeamTestClient.InviteAndReadAsync(
            OwnerClient, Slug, AuthTestClient.UniqueEmail("davetli"), MembershipRole.Agent,
            _cancellationToken)).Invitation.Id;

    public async Task<(Guid TicketId, Guid AttachmentId)> NewAttachmentAsync()
    {
        var ticket = await NewTicketAsync();

        using var response = await OwnerClient.PostAsync(
            new Uri($"/api/workspaces/{Slug}/tickets/{ticket.Id}/attachments", UriKind.Relative),
            EndpointCatalog.SmallTextFile(),
            _cancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var attachment = await response.Content.ReadFromJsonAsync<AttachmentResponse>(
            FlowDeskJson.Options, _cancellationToken);

        Assert.NotNull(attachment);

        return (ticket.Id, attachment.Id);
    }
}

/// <summary>
/// Every workspace-scoped endpoint the API exposes, in one table.
/// </summary>
/// <remarks>
/// Before this table, each module tested its own permissions. That caught a
/// handler that forgot its check, but nothing caught an endpoint that was never
/// tested at all. The coverage test now fails when the application exposes a
/// route that is not listed here, so adding an endpoint without deciding who
/// may call it is no longer possible.
///
/// Order matters in two places: the team cases change and then remove the
/// bystander other cases rely on, and deleting the workspace ends every case,
/// so those come last.
/// </remarks>
internal static class EndpointCatalog
{
    private const string Workspace = "/api/workspaces/{workspaceSlug}";

    public static IReadOnlyList<EndpointCase> All { get; } =
    [
        // Workspace
        Case(HttpMethods.Get, "", MembershipRole.Viewer, HttpStatusCode.OK,
            _ => Get("")),
        Case(HttpMethods.Patch, "", MembershipRole.Admin, HttpStatusCode.OK,
            _ => Task.FromResult(new ScopedRequest(
                HttpMethod.Patch, "", Json(new UpdateWorkspaceCommand("Yeniden Adlandırılmış Alan"))))),

        // Team
        Case(HttpMethods.Get, "/members", MembershipRole.Viewer, HttpStatusCode.OK,
            _ => Get("/members")),
        Case(HttpMethods.Get, "/invitations", MembershipRole.Viewer, HttpStatusCode.OK,
            _ => Get("/invitations")),
        Case(HttpMethods.Post, "/invitations", MembershipRole.Admin, HttpStatusCode.Created,
            _ => Task.FromResult(new ScopedRequest(
                HttpMethod.Post, "/invitations",
                Json(new InviteMemberCommand(AuthTestClient.UniqueEmail("aday"), MembershipRole.Agent))))),
        Case(HttpMethods.Delete, "/invitations/{invitationId}", MembershipRole.Admin, HttpStatusCode.NoContent,
            async scene => new ScopedRequest(
                HttpMethod.Delete, $"/invitations/{await scene.NewInvitationAsync()}")),

        // Customers
        Case(HttpMethods.Get, "/customers", MembershipRole.Viewer, HttpStatusCode.OK,
            _ => Get("/customers")),
        Case(HttpMethods.Get, "/customers/{customerId}", MembershipRole.Viewer, HttpStatusCode.OK,
            scene => Get($"/customers/{scene.CustomerId}")),
        Case(HttpMethods.Post, "/customers", MembershipRole.Agent, HttpStatusCode.Created,
            _ => Task.FromResult(new ScopedRequest(
                HttpMethod.Post, "/customers", Json(new CreateCustomerCommand("Deniz Gıda", null, null, null, CustomerStatus.Active, null))))),
        Case(HttpMethods.Patch, "/customers/{customerId}", MembershipRole.Agent, HttpStatusCode.OK,
            async scene => new ScopedRequest(
                HttpMethod.Patch, $"/customers/{await scene.NewCustomerAsync()}",
                Json(new UpdateCustomerCommand("Deniz Gıda A.Ş.", null, null, null, CustomerStatus.Active, null)))),
        Case(HttpMethods.Delete, "/customers/{customerId}", MembershipRole.Admin, HttpStatusCode.NoContent,
            async scene => new ScopedRequest(
                HttpMethod.Delete, $"/customers/{await scene.NewCustomerAsync()}")),
        Case(HttpMethods.Post, "/customers/{customerId}/restore", MembershipRole.Admin, HttpStatusCode.NoContent,
            async scene => new ScopedRequest(
                HttpMethod.Post, $"/customers/{await scene.NewArchivedCustomerAsync()}/restore")),

        // Tickets
        Case(HttpMethods.Get, "/tickets", MembershipRole.Viewer, HttpStatusCode.OK,
            _ => Get("/tickets")),
        Case(HttpMethods.Get, "/tickets/{ticketId}", MembershipRole.Viewer, HttpStatusCode.OK,
            async scene => new ScopedRequest(HttpMethod.Get, $"/tickets/{(await scene.NewTicketAsync()).Id}")),
        Case(HttpMethods.Post, "/tickets", MembershipRole.Agent, HttpStatusCode.Created,
            scene => Task.FromResult(new ScopedRequest(
                HttpMethod.Post, "/tickets",
                Json(new CreateTicketCommand(
                    scene.CustomerId, "Giriş yapılamıyor", "Parola sıfırlama e-postası gelmiyor.",
                    TicketPriority.High, null))))),
        Case(HttpMethods.Patch, "/tickets/{ticketId}", MembershipRole.Agent, HttpStatusCode.OK,
            async scene =>
            {
                var ticket = await scene.NewTicketAsync();

                return new ScopedRequest(
                    HttpMethod.Patch, $"/tickets/{ticket.Id}",
                    Json(new UpdateTicketCommand(
                        "Giriş yapılamıyor (güncellendi)", ticket.Description, ticket.Priority,
                        scene.CustomerId, ticket.Version)));
            }),
        Case(HttpMethods.Delete, "/tickets/{ticketId}", MembershipRole.Admin, HttpStatusCode.NoContent,
            async scene => new ScopedRequest(HttpMethod.Delete, $"/tickets/{(await scene.NewTicketAsync()).Id}")),
        Case(HttpMethods.Post, "/tickets/{ticketId}/status", MembershipRole.Agent, HttpStatusCode.OK,
            async scene => new ScopedRequest(
                HttpMethod.Post, $"/tickets/{(await scene.NewTicketAsync()).Id}/status",
                Json(new ChangeTicketStatusCommand(TicketStatus.InProgress)))),
        Case(HttpMethods.Post, "/tickets/{ticketId}/assignment", MembershipRole.Agent, HttpStatusCode.OK,
            async scene => new ScopedRequest(
                HttpMethod.Post, $"/tickets/{(await scene.NewTicketAsync()).Id}/assignment",
                Json(new AssignTicketCommand(scene.BystanderId)))),
        Case(HttpMethods.Get, "/tickets/{ticketId}/comments", MembershipRole.Viewer, HttpStatusCode.OK,
            async scene => new ScopedRequest(
                HttpMethod.Get, $"/tickets/{(await scene.NewTicketAsync()).Id}/comments")),
        Case(HttpMethods.Post, "/tickets/{ticketId}/comments", MembershipRole.Agent, HttpStatusCode.Created,
            async scene => new ScopedRequest(
                HttpMethod.Post, $"/tickets/{(await scene.NewTicketAsync()).Id}/comments",
                Json(new AddTicketCommentCommand("Müşteriyle görüşüldü.")))),
        Case(HttpMethods.Get, "/tickets/{ticketId}/attachments", MembershipRole.Viewer, HttpStatusCode.OK,
            async scene => new ScopedRequest(
                HttpMethod.Get, $"/tickets/{(await scene.NewTicketAsync()).Id}/attachments")),
        Case(HttpMethods.Post, "/tickets/{ticketId}/attachments", MembershipRole.Agent, HttpStatusCode.Created,
            async scene => new ScopedRequest(
                HttpMethod.Post, $"/tickets/{(await scene.NewTicketAsync()).Id}/attachments",
                SmallTextFile())),
        Case(HttpMethods.Get, "/tickets/{ticketId}/attachments/{attachmentId}", MembershipRole.Viewer,
            HttpStatusCode.OK,
            async scene =>
            {
                var (ticketId, attachmentId) = await scene.NewAttachmentAsync();

                return new ScopedRequest(HttpMethod.Get, $"/tickets/{ticketId}/attachments/{attachmentId}");
            }),
        Case(HttpMethods.Delete, "/tickets/{ticketId}/attachments/{attachmentId}", MembershipRole.Agent,
            HttpStatusCode.NoContent,
            async scene =>
            {
                var (ticketId, attachmentId) = await scene.NewAttachmentAsync();

                return new ScopedRequest(HttpMethod.Delete, $"/tickets/{ticketId}/attachments/{attachmentId}");
            }),

        // Tasks
        Case(HttpMethods.Get, "/tasks", MembershipRole.Viewer, HttpStatusCode.OK,
            _ => Get("/tasks")),
        Case(HttpMethods.Get, "/tasks/{taskId}", MembershipRole.Viewer, HttpStatusCode.OK,
            async scene => new ScopedRequest(HttpMethod.Get, $"/tasks/{(await scene.NewTaskAsync()).Id}")),
        Case(HttpMethods.Post, "/tasks", MembershipRole.Agent, HttpStatusCode.Created,
            _ => Task.FromResult(new ScopedRequest(
                HttpMethod.Post, "/tasks", Json(new CreateTaskCommand("Teklifi hazırla", null, null, null, null))))),
        Case(HttpMethods.Patch, "/tasks/{taskId}", MembershipRole.Agent, HttpStatusCode.OK,
            async scene => new ScopedRequest(
                HttpMethod.Patch, $"/tasks/{(await scene.NewTaskAsync()).Id}",
                Json(new UpdateTaskCommand("Teklifi hazırla ve gönder", null, null, null, null)))),
        Case(HttpMethods.Post, "/tasks/{taskId}/status", MembershipRole.Agent, HttpStatusCode.OK,
            async scene => new ScopedRequest(
                HttpMethod.Post, $"/tasks/{(await scene.NewTaskAsync()).Id}/status",
                Json(new ChangeTaskStatusCommand(TaskItemStatus.Done)))),
        Case(HttpMethods.Delete, "/tasks/{taskId}", MembershipRole.Agent, HttpStatusCode.NoContent,
            async scene => new ScopedRequest(HttpMethod.Delete, $"/tasks/{(await scene.NewTaskAsync()).Id}")),

        // Read models
        Case(HttpMethods.Get, "/dashboard", MembershipRole.Viewer, HttpStatusCode.OK,
            _ => Get("/dashboard")),
        Case(HttpMethods.Get, "/activity", MembershipRole.Viewer, HttpStatusCode.OK,
            _ => Get("/activity")),
        Case(HttpMethods.Get, "/notifications", MembershipRole.Viewer, HttpStatusCode.OK,
            _ => Get("/notifications")),
        // Marking read touches only the caller's own notices, so it is open to
        // anyone who can see the workspace (ADR-0034).
        Case(HttpMethods.Post, "/notifications/read", MembershipRole.Viewer, HttpStatusCode.NoContent,
            _ => Task.FromResult(new ScopedRequest(
                HttpMethod.Post, "/notifications/read", Json(new MarkNotificationsReadRequest([]))))),

        /*
          Both aimed at the bystander, so they come late: a ticket case assigns
          work to the bystander, which fails once they have been removed. The
          role change comes first for the same reason.
        */
        Case(HttpMethods.Patch, "/members/{userId}", MembershipRole.Admin, HttpStatusCode.NoContent,
            scene => Task.FromResult(new ScopedRequest(
                HttpMethod.Patch, $"/members/{scene.BystanderId}",
                Json(new ChangeMemberRoleRequest(MembershipRole.Viewer))))),
        Case(HttpMethods.Delete, "/members/{userId}", MembershipRole.Admin, HttpStatusCode.NoContent,
            scene => Task.FromResult(new ScopedRequest(HttpMethod.Delete, $"/members/{scene.BystanderId}"))),

        // Last: nothing can run after it.
        Case(HttpMethods.Delete, "", MembershipRole.Owner, HttpStatusCode.NoContent,
            _ => Task.FromResult(new ScopedRequest(HttpMethod.Delete, ""))),
    ];

    /// <summary>
    /// Routes deliberately outside the catalog, with the reason each one is.
    /// </summary>
    /// <remarks>
    /// None of these is scoped to a workspace, so a role inside one means
    /// nothing to them. Each is covered by its own module's tests.
    /// </remarks>
    public static IReadOnlyDictionary<string, string> OutOfScope { get; } = new Dictionary<string, string>
    {
        ["POST /api/auth/register"] = "Anonim; kayıt testleri kapsıyor.",
        ["POST /api/auth/login"] = "Anonim; giriş testleri kapsıyor.",
        ["POST /api/auth/refresh"] = "Çereze bağlı; rotasyon testleri kapsıyor.",
        ["POST /api/auth/logout"] = "Çereze bağlı; rotasyon testleri kapsıyor.",
        ["GET /api/me"] = "Çalışma alanından bağımsız hesap bilgisi.",
        ["GET /api/workspaces"] = "Çağıranın kendi üyelikleri; izolasyon testleri kapsıyor.",
        ["POST /api/workspaces"] = "Her oturum açmış kullanıcı alan açabilir.",
        ["POST /api/invitations/accept"] = "Yetki rol değil token'dır; davet testleri kapsıyor.",
        ["* /health/live"] = "Anonim sağlık denetimi.",
        ["* /health/ready"] = "Anonim sağlık denetimi.",
        ["GET /openapi/{documentName}.json"] = "Yalnızca Development ortamında açılan şema.",
    };

    public static string ScopedTemplate(EndpointCase endpoint) => Workspace + endpoint.Template;

    /// <summary>A file the attachment rules accept, in the form the endpoint binds.</summary>
    public static MultipartFormDataContent SmallTextFile()
    {
        var file = new ByteArrayContent("Kurulum notları"u8.ToArray());
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

        return new MultipartFormDataContent { { file, "file", "notlar.txt" } };
    }

    private static EndpointCase Case(
        string method,
        string template,
        MembershipRole minimumRole,
        HttpStatusCode success,
        Func<EndpointScene, Task<ScopedRequest>> arrange) =>
        new(method, template, minimumRole, success, arrange);

    private static Task<ScopedRequest> Get(string path) =>
        Task.FromResult(new ScopedRequest(HttpMethod.Get, path));

    private static JsonContent Json<T>(T value) => JsonContent.Create(value, options: FlowDeskJson.Options);
}
