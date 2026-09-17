using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FlowDesk.Api.Contracts;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Attachments;

/// <summary>
/// Uploading, listing, downloading and removing a ticket's files.
/// </summary>
/// <remarks>
/// Run against real object storage. What matters here is not that a byte array
/// survives a round trip — it is that content written under a generated key
/// comes back only to someone entitled to it, and that every rule about what
/// may be uploaded actually holds at the endpoint (docs/SECURITY.md).
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class AttachmentTests
{
    private readonly PostgresContainerFixture _postgres;
    private readonly AzuriteContainerFixture _storage;

    public AttachmentTests(PostgresContainerFixture postgres, AzuriteContainerFixture storage)
    {
        _postgres = postgres;
        _storage = storage;
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private FlowDeskApiFactory CreateFactory() =>
        new(_postgres.ConnectionString, storageConnectionString: _storage.ConnectionString);

    [Fact]
    public async Task An_uploaded_file_comes_back_byte_for_byte()
    {
        await using var factory = CreateFactory();
        var ticket = await RaiseTicketAsync(factory);

        var content = Encoding.UTF8.GetBytes("konu,adet\nfatura,3\n");

        var attachment = await UploadAsync(
            ticket, "şubat-raporu.csv", "text/csv", content);

        Assert.Equal("şubat-raporu.csv", attachment.FileName);
        Assert.Equal("text/csv", attachment.ContentType);
        Assert.Equal(content.Length, attachment.SizeInBytes);

        using var download = await ticket.Workspace.Client.GetAsync(
            AttachmentUri(ticket, attachment.Id), Cancellation);

        var downloaded = await download.Content.ReadAsByteArrayAsync(Cancellation);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(content, downloaded);
        Assert.Equal("text/csv", download.Content.Headers.ContentType?.MediaType);

        /*
          Always an attachment, never inline. Serving an uploaded file in a way
          the browser renders would let it run in the application's own origin.
        */
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task A_ticket_lists_its_files_oldest_first()
    {
        await using var factory = CreateFactory();
        var ticket = await RaiseTicketAsync(factory);

        await UploadAsync(ticket, "ilk.txt", "text/plain", "ilk"u8.ToArray());
        await UploadAsync(ticket, "ikinci.txt", "text/plain", "ikinci"u8.ToArray());

        using var response = await ticket.Workspace.Client.GetAsync(
            AttachmentsUri(ticket), Cancellation);

        var attachments = await response.Content.ReadFromJsonAsync<List<AttachmentResponse>>(
            FlowDeskJson.Options, Cancellation);

        Assert.NotNull(attachments);
        Assert.Equal(["ilk.txt", "ikinci.txt"], attachments.Select(item => item.FileName));
    }

    /// <summary>
    /// A type that is not on the allow-list is refused.
    /// </summary>
    /// <remarks>
    /// SVG in particular: an image by name, a document that can carry script in
    /// fact.
    /// </remarks>
    [Theory]
    [InlineData("image/svg+xml", "cizim.svg")]
    [InlineData("text/html", "sayfa.html")]
    [InlineData("application/octet-stream", "program.bin")]
    public async Task A_type_off_the_list_is_refused(string contentType, string fileName)
    {
        await using var factory = CreateFactory();
        var ticket = await RaiseTicketAsync(factory);

        using var response = await PostAsync(ticket, fileName, contentType, "veri"u8.ToArray());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            "attachment.type_not_allowed",
            await AuthTestClient.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task A_file_over_the_limit_is_refused()
    {
        await using var factory = CreateFactory();
        var ticket = await RaiseTicketAsync(factory);

        // The test factory sets the limit to one megabyte.
        var tooLarge = new byte[(1024 * 1024) + 1];

        using var response = await PostAsync(ticket, "büyük.pdf", "application/pdf", tooLarge);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            "attachment.too_large",
            await AuthTestClient.ReadProblemCodeAsync(response, Cancellation));
    }

    [Fact]
    public async Task An_empty_file_is_refused()
    {
        await using var factory = CreateFactory();
        var ticket = await RaiseTicketAsync(factory);

        using var response = await PostAsync(ticket, "bos.txt", "text/plain", []);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>
    /// A name carrying a path is stored stripped of it.
    /// </summary>
    /// <remarks>
    /// The name never addresses the bytes, but it is written into a download
    /// header and shown to people.
    /// </remarks>
    [Fact]
    public async Task A_name_carrying_a_path_is_stripped()
    {
        await using var factory = CreateFactory();
        var ticket = await RaiseTicketAsync(factory);

        var attachment = await UploadAsync(
            ticket, "../../../etc/passwd", "text/plain", "veri"u8.ToArray());

        Assert.Equal("passwd", attachment.FileName);
    }

    /// <summary>
    /// A file belongs to one workspace and reaches nobody outside it.
    /// </summary>
    /// <remarks>
    /// A security boundary, not a convenience check. The container is private
    /// and nothing hands out a signed link, so this is the only path to the
    /// bytes — and it must refuse (docs/SECURITY.md).
    /// </remarks>
    [Fact]
    public async Task A_stranger_cannot_reach_another_workspaces_file()
    {
        await using var factory = CreateFactory();

        var ticket = await RaiseTicketAsync(factory);
        var attachment = await UploadAsync(
            ticket, "gizli.pdf", "application/pdf", "gizli"u8.ToArray());

        using var stranger = await TestWorkspace.CreateAsync(factory, Cancellation);

        // Through the stranger's own workspace, using the other ids.
        using var throughOwnWorkspace = await stranger.Client.GetAsync(
            new Uri(
                $"/api/workspaces/{stranger.Slug}/tickets/{ticket.TicketId}/attachments/{attachment.Id}",
                UriKind.Relative),
            Cancellation);

        // And directly against the workspace they do not belong to.
        using var throughForeignWorkspace = await stranger.Client.GetAsync(
            AttachmentUri(ticket, attachment.Id), Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, throughOwnWorkspace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, throughForeignWorkspace.StatusCode);
    }

    /// <summary>
    /// An attachment is reachable only through the ticket it belongs to.
    /// </summary>
    /// <remarks>
    /// Not a tenant leak — both tickets are in the same workspace — but a file
    /// served under a ticket that does not own it is a route that lies about
    /// what it returns.
    /// </remarks>
    [Fact]
    public async Task An_attachment_is_not_reachable_through_another_ticket()
    {
        await using var factory = CreateFactory();

        var ticket = await RaiseTicketAsync(factory);
        var attachment = await UploadAsync(
            ticket, "rapor.pdf", "application/pdf", "rapor"u8.ToArray());

        var otherTicket = await TicketTestClient.CreateAndReadAsync(
            ticket.Workspace.Client, ticket.Workspace.Slug, ticket.Workspace.CustomerId,
            Cancellation, "Başka talep");

        using var response = await ticket.Workspace.Client.GetAsync(
            new Uri(
                $"/api/workspaces/{ticket.Workspace.Slug}/tickets/{otherTicket.Id}/attachments/{attachment.Id}",
                UriKind.Relative),
            Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>A Viewer reads files but does not add or remove them.</summary>
    [Fact]
    public async Task A_viewer_can_download_but_not_upload_or_delete()
    {
        await using var factory = CreateFactory();

        var ticket = await RaiseTicketAsync(factory);
        var attachment = await UploadAsync(
            ticket, "rapor.pdf", "application/pdf", "rapor"u8.ToArray());

        using var viewer = await TeamTestClient.AddMemberAsync(
            factory, ticket.Workspace.Client, ticket.Workspace.Slug,
            MembershipRole.Viewer, Cancellation);

        using var download = await viewer.Client.GetAsync(
            AttachmentUri(ticket, attachment.Id), Cancellation);

        using var upload = await PostAsync(
            ticket, "yeni.pdf", "application/pdf", "yeni"u8.ToArray(), viewer.Client);

        using var delete = await viewer.Client.DeleteAsync(
            AttachmentUri(ticket, attachment.Id), Cancellation);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);

        // 403, not 404: they belong to this workspace, so only the action is
        // refused (docs/API_CONVENTIONS.md).
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Deleting_an_attachment_removes_it_and_its_content()
    {
        await using var factory = CreateFactory();

        var ticket = await RaiseTicketAsync(factory);
        var attachment = await UploadAsync(
            ticket, "silinecek.txt", "text/plain", "veri"u8.ToArray());

        using var deleted = await ticket.Workspace.Client.DeleteAsync(
            AttachmentUri(ticket, attachment.Id), Cancellation);

        using var download = await ticket.Workspace.Client.GetAsync(
            AttachmentUri(ticket, attachment.Id), Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    /// <summary>
    /// Deleting a ticket takes its files with it.
    /// </summary>
    /// <remarks>
    /// The rows go through the database cascade, but nothing in the database
    /// can reach object storage — so the bytes are removed explicitly, and this
    /// is what proves it happens.
    /// </remarks>
    [Fact]
    public async Task Deleting_a_ticket_removes_its_files()
    {
        await using var factory = CreateFactory();

        var ticket = await RaiseTicketAsync(factory);
        var attachment = await UploadAsync(
            ticket, "ekli.txt", "text/plain", "veri"u8.ToArray());

        using var deleted = await TicketTestClient.DeleteAsync(
            ticket.Workspace.Client, ticket.Workspace.Slug, ticket.TicketId, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var download = await ticket.Workspace.Client.GetAsync(
            AttachmentUri(ticket, attachment.Id), Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    private sealed record TicketUnderTest(TestWorkspace Workspace, Guid TicketId);

    private static async Task<TicketUnderTest> RaiseTicketAsync(FlowDeskApiFactory factory)
    {
        var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        return new TicketUnderTest(workspace, ticket.Id);
    }

    private static Uri AttachmentsUri(TicketUnderTest ticket) =>
        new(
            $"/api/workspaces/{ticket.Workspace.Slug}/tickets/{ticket.TicketId}/attachments",
            UriKind.Relative);

    private static Uri AttachmentUri(TicketUnderTest ticket, Guid attachmentId) =>
        new(
            $"/api/workspaces/{ticket.Workspace.Slug}/tickets/{ticket.TicketId}/attachments/{attachmentId}",
            UriKind.Relative);

    private static async Task<HttpResponseMessage> PostAsync(
        TicketUnderTest ticket,
        string fileName,
        string contentType,
        byte[] content,
        HttpClient? client = null)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(content);

        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        // The field name matches the parameter the endpoint binds.
        form.Add(file, "file", fileName);

        return await (client ?? ticket.Workspace.Client)
            .PostAsync(AttachmentsUri(ticket), form, Cancellation);
    }

    private static async Task<AttachmentResponse> UploadAsync(
        TicketUnderTest ticket,
        string fileName,
        string contentType,
        byte[] content)
    {
        using var response = await PostAsync(ticket, fileName, contentType, content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var attachment = await response.Content.ReadFromJsonAsync<AttachmentResponse>(
            FlowDeskJson.Options, Cancellation);

        Assert.NotNull(attachment);

        return attachment;
    }
}
