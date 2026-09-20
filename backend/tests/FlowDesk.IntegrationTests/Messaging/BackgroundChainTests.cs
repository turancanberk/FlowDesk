using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Infrastructure;
using FlowDesk.Infrastructure.Observability;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FlowDesk.IntegrationTests.Messaging;

/// <summary>
/// The whole background path in one piece: a request through the API, the
/// outbox row it writes, the worker publishing it, the broker routing it, the
/// consumer handling it, and the notice and mail that come out the other end.
/// </summary>
/// <remarks>
/// Every link has its own tests. What none of them can catch is a chain that
/// is correct link by link and still broken: a message type the worker has no
/// consumer for, a routing key the queue does not bind, a payload the consumer
/// cannot read. Here the worker is composed exactly as the Worker host composes
/// it, and the result is read back the way a person would — the notice through
/// the API as the assignee, the mail from a real SMTP server's inbox.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class BackgroundChainTests : IClassFixture<MailpitContainerFixture>
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly PostgresContainerFixture _postgres;
    private readonly RabbitMqContainerFixture _broker;
    private readonly MailpitContainerFixture _mail;

    public BackgroundChainTests(
        PostgresContainerFixture postgres,
        RabbitMqContainerFixture broker,
        MailpitContainerFixture mail)
    {
        _postgres = postgres;
        _broker = broker;
        _mail = mail;
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_invitation_and_an_assignment_arrive_as_mail_and_a_notice()
    {
        // What the worker writes and measures while the chain runs.
        var workerLogs = new CapturedLogs();
        using var meters = new MeterProbe(FlowDeskTelemetry.MeterName);

        // A database of its own: the worker drains every pending outbox row
        // it can see, and the shared one holds every earlier test's messages.
        var connectionString = await _postgres.CreateIsolatedDatabaseAsync(Cancellation);

        await using var api = new FlowDeskApiFactory(connectionString, _broker.Host, _broker.Port);
        using var worker = await StartWorkerAsync(api.Settings, workerLogs);

        try
        {
            using var workspace = await TestWorkspace.CreateAsync(api, Cancellation);
            using var agent = await AuthTestClient.SignInNewUserAsync(api, Cancellation, "Can Yıldırım");

            // Invitation: the mail carries the one-time link.
            var invitation = await TeamTestClient.InviteAndReadAsync(
                workspace.Client, workspace.Slug, agent.Email, MembershipRole.Agent, Cancellation);

            var invitationMail = await EventuallyAsync(
                async () => (await _mail.MessagesToAsync(agent.Email, Cancellation)).SingleOrDefault());

            Assert.Contains(
                invitation.Token,
                await _mail.HtmlBodyAsync(invitationMail.Id, Cancellation),
                StringComparison.Ordinal);

            using (var accepted = await TeamTestClient.AcceptAsync(agent.Client, invitation.Token, Cancellation))
            {
                Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
            }

            // Assignment: a notice in the app and a mail, both for the assignee.
            var ticket = await TicketTestClient.CreateAndReadAsync(
                workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation,
                subject: "Mobil uygulamada giriş hatası");

            /*
              A trace of the test's own making, so the assertions below can
              follow this one request all the way into the worker. A browser
              or a gateway sends the same header; the API continues whatever
              trace it is given.
            */
            var traceId = System.Diagnostics.ActivityTraceId.CreateRandom().ToHexString();

            using (var assignment = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(
                    $"/api/workspaces/{workspace.Slug}/tickets/{ticket.Id}/assignment",
                    UriKind.Relative)))
            {
                assignment.Headers.Add(
                    "traceparent",
                    $"00-{traceId}-{System.Diagnostics.ActivitySpanId.CreateRandom().ToHexString()}-01");
                assignment.Content = JsonContent.Create(
                    new { assignedUserId = agent.Id }, options: FlowDeskJson.Options);

                using var assigned = await workspace.Client.SendAsync(assignment, Cancellation);

                Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
            }

            var notice = await EventuallyAsync(async () =>
            {
                var feed = await agent.Client.GetFromJsonAsync<NotificationFeedResponse>(
                    new Uri($"/api/workspaces/{workspace.Slug}/notifications", UriKind.Relative),
                    FlowDeskJson.Options,
                    Cancellation);

                return feed?.Items.SingleOrDefault(item => item.Type == NotificationType.TicketAssigned);
            });

            Assert.False(notice.IsRead);
            Assert.Contains("Mobil uygulamada giriş hatası", notice.Payload.GetRawText(), StringComparison.Ordinal);

            var ticketNumber = $"TLP-{ticket.Number}";
            var assignmentMail = await EventuallyAsync(async () =>
                (await _mail.MessagesToAsync(agent.Email, Cancellation))
                    .SingleOrDefault(message => message.Subject.Contains(ticketNumber, StringComparison.Ordinal)));

            Assert.Contains(
                $"/tickets/{ticket.Id}",
                await _mail.HtmlBodyAsync(assignmentMail.Id, Cancellation),
                StringComparison.Ordinal);

            // Nothing left behind, and each message handled once per consumer.
            await using var dbContext = CreateDbContext(connectionString);

            Assert.False(await dbContext.OutboxMessages.AnyAsync(
                message => message.ProcessedAt == null, Cancellation));

            var handled = await dbContext.ProcessedMessages
                .GroupBy(processed => new { processed.MessageId, processed.Consumer })
                .Select(group => group.Count())
                .ToListAsync(Cancellation);

            Assert.NotEmpty(handled);
            Assert.All(handled, count => Assert.Equal(1, count));

            // The invitation mail and the assignment mail — nothing twice.
            Assert.Equal(2, (await _mail.MessagesToAsync(agent.Email, Cancellation)).Count);

            /*
              The request's trace reached the worker. The row carried it out of
              the API, the broker carried it to the consumer, and the consumer
              wrote its work under it — which is what makes a notice sent
              minutes later traceable to the click that caused it (ADR-0042).
            */
            var assignmentRow = await dbContext.OutboxMessages
                .Where(message => message.Type == "TicketAssigned")
                .Select(message => message.TraceParent)
                .SingleAsync(Cancellation);

            Assert.NotNull(assignmentRow);
            Assert.Contains(traceId, assignmentRow, StringComparison.Ordinal);
            Assert.True(
                workerLogs.TraceIds.Contains(traceId, StringComparer.Ordinal),
                $"Worker, isteğin izi altında hiçbir şey yazmadı. Yazdığı izler: "
                + string.Join(", ", workerLogs.TraceIds));

            /*
              What the worker wrote while doing it (docs/SECURITY.md §12). The
              API's log hygiene test cannot reach here: mail is sent by a
              consumer, in the worker, and that is exactly where the address
              used to be logged.
            */
            Assert.False(
                workerLogs.Mentions(agent.Email),
                "Worker logunda alıcının e-posta adresi var.");
            Assert.False(
                workerLogs.Mentions(invitation.Token),
                "Worker logunda davet token'ı var.");

            // Npgsql logs every statement, with its SQL, unless its level is
            // lowered. Query text in a log nobody asked for is noise at best.
            Assert.DoesNotContain(
                workerLogs.Rendered,
                line => line.Contains("SELECT ", StringComparison.Ordinal));

            // And it measured what it did.
            meters.CollectObservable();

            Assert.True(meters.Total("flowdesk.outbox.published") >= 2);
            Assert.Equal(0, meters.Total("flowdesk.outbox.publish_failures"));
            Assert.True(meters.Total("flowdesk.messages.consumed", "outcome=handled") >= 2);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Starts the worker's services the way the Worker host does, pointed at
    /// the API's database and exchange and at the test's SMTP server.
    /// </summary>
    private async Task<IHost> StartWorkerAsync(
        IReadOnlyDictionary<string, string?> apiSettings,
        CapturedLogs logs)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development,
            DisableDefaults = true,
        });

        var settings = new Dictionary<string, string?>(apiSettings)
        {
            ["Email:Host"] = _mail.SmtpHost,
            ["Email:Port"] = _mail.SmtpPortNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Email:UseStartTls"] = "false",
            // The shortest interval allowed, so the test waits on the chain
            // rather than on the clock.
            ["Outbox:PollIntervalSeconds"] = "1",
        };

        builder.Configuration.AddInMemoryCollection(settings);

        // The Worker's own composition, not a copy of it.
        builder.Services.AddFlowDeskWorker(builder.Configuration);

        // Read back through the worker's own logging pipeline.
        builder.Services.AddSingleton<Serilog.Core.ILogEventSink>(logs);

        var host = builder.Build();
        await host.StartAsync(Cancellation);

        return host;
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T?>> probe)
        where T : class
    {
        var deadline = DateTimeOffset.UtcNow + Patience;

        while (true)
        {
            if (await probe() is { } result)
            {
                return result;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"Arka plan zinciri {Patience.TotalSeconds} saniyede sonuç üretmedi.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), Cancellation);
        }
    }

    private static Infrastructure.Persistence.FlowDeskDbContext CreateDbContext(string connectionString) =>
        new(new DbContextOptionsBuilder<Infrastructure.Persistence.FlowDeskDbContext>()
            .UseNpgsql(connectionString)
            .Options);
}
