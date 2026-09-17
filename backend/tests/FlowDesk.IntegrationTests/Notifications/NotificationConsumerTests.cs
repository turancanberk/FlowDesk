using System.Collections.Concurrent;
using System.Text.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Team;
using FlowDesk.Application.Tickets;
using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Infrastructure.Email;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Notifications;
using FlowDesk.Infrastructure.Time;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowDesk.IntegrationTests.Notifications;

/// <summary>
/// What each consumer decides to do with a message.
/// </summary>
/// <remarks>
/// The consumers are driven directly rather than through the broker. The
/// delivery machinery — routing, acknowledgement, idempotency — is covered by
/// <c>MessageConsumerTests</c>; what these check is the judgement inside:
/// who gets told, who does not, and what is written down.
///
/// The mail sender is a stand-in that records instead of sending. Everything
/// else is real, including the database, because the rows a consumer writes and
/// the scoping that keeps them in one workspace are the point.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class NotificationConsumerTests
{
    private readonly PostgresContainerFixture _postgres;

    public NotificationConsumerTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_assignment_writes_a_notice_and_sends_one_mail()
    {
        await using var world = await ConsumerWorld.CreateAsync(_postgres, Cancellation);

        await world.TicketAssigned.HandleAsync(
            world.Assignment(subject: "Fatura PDF'i indirilemiyor"), Cancellation);

        var notice = await world.SingleNoticeForAsync(world.Assignee.Id, Cancellation);
        var payload = JsonSerializer.Deserialize<TicketAssignedPayload>(
            notice.Payload, FlowDeskMessageJson.Options);

        Assert.Equal(NotificationType.TicketAssigned, notice.Type);
        Assert.NotNull(payload);
        Assert.Equal("Fatura PDF'i indirilemiyor", payload.Subject);
        Assert.Equal(world.Assigner.DisplayName, payload.AssignedByDisplayName);

        var mail = Assert.Single(world.SentMail);

        Assert.Equal(world.Assignee.Email, mail.ToAddress);
        Assert.Contains("TLP-1001", mail.Subject, StringComparison.Ordinal);
        // The link points at the ticket, through the configured web address.
        Assert.Contains($"/tickets/{world.TicketId}", mail.HtmlBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// Assigning a ticket to yourself tells you nothing.
    /// </summary>
    [Fact]
    public async Task Assigning_to_yourself_notifies_nobody()
    {
        await using var world = await ConsumerWorld.CreateAsync(_postgres, Cancellation);

        var message = world.Assignment() with { AssignedByUserId = world.Assignee.Id };

        await world.TicketAssigned.HandleAsync(message, Cancellation);

        Assert.Empty(await world.NoticesForAsync(world.Assignee.Id, Cancellation));
        Assert.Empty(world.SentMail);
    }

    /// <summary>
    /// A ticket subject cannot smuggle markup into a colleague's inbox.
    /// </summary>
    /// <remarks>
    /// Every value in these messages is typed by a person, so none of it can be
    /// trusted as HTML (docs/SECURITY.md).
    /// </remarks>
    [Fact]
    public async Task A_subject_containing_markup_is_escaped_in_the_mail()
    {
        await using var world = await ConsumerWorld.CreateAsync(_postgres, Cancellation);

        await world.TicketAssigned.HandleAsync(
            world.Assignment(subject: "<script>alert(1)</script>"), Cancellation);

        var mail = Assert.Single(world.SentMail);

        Assert.DoesNotContain("<script>", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", mail.HtmlBody, StringComparison.Ordinal);
    }

    /// <summary>
    /// A comment tells the person the ticket belongs to, in the app only.
    /// </summary>
    /// <remarks>
    /// No e-mail on purpose: a mail per comment is the fastest way to teach
    /// people to filter FlowDesk out of their inbox.
    /// </remarks>
    [Fact]
    public async Task A_comment_notifies_the_assignee_without_sending_mail()
    {
        await using var world = await ConsumerWorld.CreateAsync(_postgres, Cancellation);

        await world.AssignTicketToAssigneeAsync(Cancellation);

        await world.TicketCommented.HandleAsync(
            world.Comment(authorId: world.Assigner.Id), Cancellation);

        var notice = await world.SingleNoticeForAsync(world.Assignee.Id, Cancellation);

        Assert.Equal(NotificationType.TicketCommented, notice.Type);
        Assert.Empty(world.SentMail);
    }

    [Fact]
    public async Task Commenting_on_your_own_ticket_notifies_nobody()
    {
        await using var world = await ConsumerWorld.CreateAsync(_postgres, Cancellation);

        await world.AssignTicketToAssigneeAsync(Cancellation);

        await world.TicketCommented.HandleAsync(
            world.Comment(authorId: world.Assignee.Id), Cancellation);

        Assert.Empty(await world.NoticesForAsync(world.Assignee.Id, Cancellation));
    }

    [Fact]
    public async Task A_comment_on_an_unassigned_ticket_notifies_nobody()
    {
        await using var world = await ConsumerWorld.CreateAsync(_postgres, Cancellation);

        await world.TicketCommented.HandleAsync(
            world.Comment(authorId: world.Assigner.Id), Cancellation);

        Assert.Empty(await world.NoticesForAsync(world.Assignee.Id, Cancellation));
    }

    [Fact]
    public async Task An_invitation_sends_a_mail_carrying_the_token()
    {
        await using var world = await ConsumerWorld.CreateAsync(_postgres, Cancellation);

        var invitation = await world.CreateInvitationAsync("davetli@ornek.test", Cancellation);

        await world.MemberInvited.HandleAsync(
            world.InvitationMessage(invitation.Id, invitation.Email, "gizli-token"), Cancellation);

        var mail = Assert.Single(world.SentMail);

        Assert.Equal("davetli@ornek.test", mail.ToAddress);
        Assert.Contains("/davet?token=gizli-token", mail.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(world.WorkspaceName, mail.Subject, StringComparison.Ordinal);
    }

    /// <summary>
    /// An invitation that has already been dealt with gets no e-mail.
    /// </summary>
    /// <remarks>
    /// The outbox can be behind — a broker outage, a restart — and a dead link
    /// is worse than no link: the recipient clicks it, is refused, and cannot
    /// tell whether the invitation was ever real.
    /// </remarks>
    [Fact]
    public async Task A_revoked_invitation_sends_nothing()
    {
        await using var world = await ConsumerWorld.CreateAsync(_postgres, Cancellation);

        var invitation = await world.CreateInvitationAsync("iptal@ornek.test", Cancellation);
        await world.RevokeAsync(invitation.Id, Cancellation);

        await world.MemberInvited.HandleAsync(
            world.InvitationMessage(invitation.Id, invitation.Email, "gizli-token"), Cancellation);

        Assert.Empty(world.SentMail);
    }

    [Fact]
    public async Task An_expired_invitation_sends_nothing()
    {
        await using var world = await ConsumerWorld.CreateAsync(_postgres, Cancellation);

        var invitation = await world.CreateInvitationAsync("suresi@ornek.test", Cancellation);

        var message = world.InvitationMessage(invitation.Id, invitation.Email, "gizli-token") with
        {
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        await world.MemberInvited.HandleAsync(message, Cancellation);

        Assert.Empty(world.SentMail);
    }

    /// <summary>Records what would have been sent, instead of sending it.</summary>
    private sealed class RecordingEmailSender : IEmailSender
    {
        public ConcurrentQueue<EmailMessage> Sent { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Enqueue(message);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A workspace, two people and a ticket, with the real consumers wired to
    /// the real database.
    /// </summary>
    private sealed class ConsumerWorld : IAsyncDisposable
    {
        private const string WebBaseUrl = "http://localhost:3000";

        private readonly PostgresContainerFixture _postgres;
        private readonly RecordingEmailSender _emailSender;

        private ConsumerWorld(
            PostgresContainerFixture postgres,
            RecordingEmailSender emailSender,
            Guid tenantId,
            string workspaceSlug,
            string workspaceName,
            UserAccount assigner,
            UserAccount assignee,
            Guid customerId,
            Guid ticketId)
        {
            _postgres = postgres;
            _emailSender = emailSender;

            TenantId = tenantId;
            WorkspaceSlug = workspaceSlug;
            WorkspaceName = workspaceName;
            Assigner = assigner;
            Assignee = assignee;
            CustomerId = customerId;
            TicketId = ticketId;
        }

        public Guid TenantId { get; }

        public string WorkspaceSlug { get; }

        public string WorkspaceName { get; }

        public UserAccount Assigner { get; }

        public UserAccount Assignee { get; }

        public Guid CustomerId { get; }

        public Guid TicketId { get; }

        public IReadOnlyCollection<EmailMessage> SentMail => _emailSender.Sent;

        public TicketAssignedConsumer TicketAssigned => new(
            _postgres.CreateDbContext(),
            new FixedAccountStore(Assigner, Assignee),
            _emailSender,
            Options.Create(EmailOptions()),
            new SystemClock());

        public TicketCommentedConsumer TicketCommented => new(
            _postgres.CreateDbContext(),
            new FixedAccountStore(Assigner, Assignee),
            new SystemClock());

        public MemberInvitedConsumer MemberInvited => new(
            _postgres.CreateDbContext(),
            new FixedAccountStore(Assigner, Assignee),
            _emailSender,
            Options.Create(EmailOptions()),
            new SystemClock());

        public static async Task<ConsumerWorld> CreateAsync(
            PostgresContainerFixture postgres,
            CancellationToken cancellationToken)
        {
            await using var dbContext = postgres.CreateDbContext();

            /*
              NewGuid, not CreateVersion7. A version 7 GUID starts with a
              timestamp, so the first eight characters of two created in the
              same millisecond are identical — and tests create these back to
              back, which collided on the slug's unique index.
            */
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var workspaceName = $"Acme {suffix}";

            var tenant = Tenant.Create(
                workspaceName,
                FlowDesk.Domain.Tenancy.WorkspaceSlug.Create($"acme-{suffix}"),
                DateTimeOffset.UtcNow);
            dbContext.Tenants.Add(tenant);

            var customer = FlowDesk.Domain.Customers.Customer.Create(
                tenant.Id,
                new FlowDesk.Domain.Customers.CustomerDetails(
                    "Kuzey Yazılım", null, null, null,
                    FlowDesk.Domain.Customers.CustomerStatus.Active, null),
                DateTimeOffset.UtcNow);
            dbContext.Customers.Add(customer);

            /*
              Real Identity rows, not invented ids. Notifications and ticket
              assignments both carry a foreign key to AspNetUsers, so a fixture
              that made ids up would fail on the constraint — and rightly:
              writing a notice addressed to nobody is what the constraint exists
              to prevent.
            */
            var assigner = AddUser(dbContext, $"atayan.{suffix}@ornek.test", "Ayşe Demir");
            var assignee = AddUser(dbContext, $"atanan.{suffix}@ornek.test", "Zeynep Arslan");

            var ticket = FlowDesk.Domain.Tickets.Ticket.Create(
                tenant.Id,
                FlowDesk.Domain.Tickets.TicketNumber.FirstNumber,
                customer.Id,
                "Fatura PDF'i indirilemiyor",
                string.Empty,
                FlowDesk.Domain.Tickets.TicketPriority.Medium,
                assigner.Id,
                assignedUserId: null,
                DateTimeOffset.UtcNow);
            dbContext.Tickets.Add(ticket);

            await dbContext.SaveChangesAsync(cancellationToken);

            return new ConsumerWorld(
                postgres,
                new RecordingEmailSender(),
                tenant.Id,
                tenant.Slug,
                workspaceName,
                assigner,
                assignee,
                customer.Id,
                ticket.Id);
        }

        /// <summary>Adds an Identity row and returns it as the application sees it.</summary>
        private static UserAccount AddUser(
            FlowDesk.Infrastructure.Persistence.FlowDeskDbContext dbContext,
            string email,
            string displayName)
        {
            var user = new FlowDesk.Infrastructure.Identity.ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                DisplayName = displayName,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                // Identity requires both stamps; no password is set because
                // nothing here signs in.
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            dbContext.Users.Add(user);

            return new UserAccount(user.Id, email, displayName);
        }

        public TicketAssigned Assignment(string subject = "Fatura PDF'i indirilemiyor") =>
            new(
                Guid.CreateVersion7(),
                TenantId,
                DateTimeOffset.UtcNow,
                TicketId,
                FlowDesk.Domain.Tickets.TicketNumber.FirstNumber,
                subject,
                Assignee.Id,
                Assigner.Id,
                WorkspaceSlug);

        public TicketCommented Comment(Guid authorId) =>
            new(
                Guid.CreateVersion7(),
                TenantId,
                DateTimeOffset.UtcNow,
                TicketId,
                FlowDesk.Domain.Tickets.TicketNumber.FirstNumber,
                "Fatura PDF'i indirilemiyor",
                authorId,
                "Log kayıtları incelendi.",
                WorkspaceSlug);

        public MemberInvited InvitationMessage(Guid invitationId, string email, string token) =>
            new(
                Guid.CreateVersion7(),
                TenantId,
                DateTimeOffset.UtcNow,
                invitationId,
                email,
                MembershipRole.Agent,
                token,
                DateTimeOffset.UtcNow.AddDays(7),
                Assigner.Id,
                WorkspaceSlug);

        public async Task AssignTicketToAssigneeAsync(CancellationToken cancellationToken)
        {
            await using var dbContext = _postgres.CreateDbContext();

            var ticket = await dbContext.Tickets
                .IgnoreQueryFilters()
                .FirstAsync(candidate => candidate.Id == TicketId, cancellationToken);

            ticket.Assign(Assignee.Id, DateTimeOffset.UtcNow);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<Invitation> CreateInvitationAsync(
            string email,
            CancellationToken cancellationToken)
        {
            await using var dbContext = _postgres.CreateDbContext();

            /*
              A distinct hash per invitation. The column is globally unique —
              two invitations cannot share a token — so a fixed literal collides
              the second time a test creates one.
            */
            var invitation = Invitation.Create(
                TenantId, email, MembershipRole.Agent, Guid.NewGuid().ToString("N"), Assigner.Id,
                DateTimeOffset.UtcNow, TimeSpan.FromDays(7));

            dbContext.Invitations.Add(invitation);
            await dbContext.SaveChangesAsync(cancellationToken);

            return invitation;
        }

        public async Task RevokeAsync(Guid invitationId, CancellationToken cancellationToken)
        {
            await using var dbContext = _postgres.CreateDbContext();

            var invitation = await dbContext.Invitations
                .IgnoreQueryFilters()
                .FirstAsync(candidate => candidate.Id == invitationId, cancellationToken);

            invitation.Revoke(DateTimeOffset.UtcNow);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Notification>> NoticesForAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            await using var dbContext = _postgres.CreateDbContext();

            return await dbContext.Notifications
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(notification => notification.TenantId == TenantId
                    && notification.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task<Notification> SingleNoticeForAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var notices = await NoticesForAsync(userId, cancellationToken);

            return Assert.Single(notices);
        }

        private static EmailOptions EmailOptions() => new()
        {
            Host = "127.0.0.1",
            Port = 1,
            UseStartTls = false,
            FromAddress = "tests@flowdesk.invalid",
            FromDisplayName = "FlowDesk Tests",
            WebBaseUrl = WebBaseUrl,
        };

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Answers for the two people in the world and nobody else.
    /// </summary>
    /// <remarks>
    /// These consumers only ever look up display names, and Identity is not
    /// what is under test here.
    /// </remarks>
    private sealed class FixedAccountStore : IUserAccountStore
    {
        private readonly Dictionary<Guid, UserAccount> _accounts;

        public FixedAccountStore(params UserAccount[] accounts) =>
            _accounts = accounts.ToDictionary(account => account.Id);

        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.GetValueOrDefault(userId));

        public Task<IReadOnlyDictionary<Guid, UserAccount>> FindByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, UserAccount>>(
                _accounts.Where(entry => userIds.Contains(entry.Key))
                    .ToDictionary(entry => entry.Key, entry => entry.Value));

        public Task<FlowDesk.Application.Common.Result<UserAccount>> CreateAsync(
            string email, string displayName, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<UserAccount?> FindByCredentialsAsync(
            string email, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.Values.FirstOrDefault(account => account.Email == email));
    }
}
