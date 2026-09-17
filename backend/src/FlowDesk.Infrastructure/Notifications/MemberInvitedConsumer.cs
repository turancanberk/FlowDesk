using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Team;
using FlowDesk.Infrastructure.Email;
using FlowDesk.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Notifications;

/// <summary>
/// Sends the invitation e-mail.
/// </summary>
/// <remarks>
/// E-mail only, with no notification row: the recipient has no account in this
/// workspace yet, so there is nowhere in the app to show them anything.
///
/// <para>
/// The link carries the raw token, which is why this message exists at all —
/// only a hash is stored, so the e-mail is the only copy that reaches the
/// person (docs/SECURITY.md).
/// </para>
/// </remarks>
public sealed class MemberInvitedConsumer : IMessageConsumer<MemberInvited>
{
    public const string QueueName = "flowdesk.member-invited.email";

    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly IClock _clock;

    public MemberInvitedConsumer(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(emailOptions);

        _dbContext = dbContext;
        _accountStore = accountStore;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _clock = clock;
    }

    public async Task HandleAsync(MemberInvited message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        /*
          An invitation that has already been accepted, revoked or expired gets
          no e-mail. The outbox can be behind — a broker outage, a restart — and
          sending a dead link is worse than sending nothing: the recipient
          clicks it, is refused, and has no way to tell whether the invitation
          was real.
        */
        if (message.ExpiresAt <= _clock.UtcNow)
        {
            return;
        }

        var stillPending = await _dbContext.Invitations
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                invitation => invitation.Id == message.InvitationId
                    && invitation.TenantId == message.TenantId
                    && invitation.AcceptedAt == null
                    && invitation.RevokedAt == null,
                cancellationToken);

        if (!stillPending)
        {
            return;
        }

        var workspaceName = await _dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == message.TenantId)
            .Select(tenant => tenant.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "bir çalışma alanı";

        var invitedBy = await _accountStore.FindByIdAsync(message.InvitedByUserId, cancellationToken);
        var inviterName = invitedBy?.DisplayName ?? "Bir ekip üyesi";

        // The accept page reads the token from the query string; it is escaped
        // because a token is opaque bytes and may contain characters a URL
        // treats as structure.
        var link = $"{_emailOptions.WebBaseUrl.TrimEnd('/')}" +
                   $"/davet?token={Uri.EscapeDataString(message.Token)}";

        await _emailSender.SendAsync(
            new EmailMessage(
                message.Email,
                ToDisplayName: null,
                $"{workspaceName} çalışma alanına davet edildiniz",
                EmailBodies.Html(
                    $"{workspaceName} çalışma alanına davet edildiniz",
                    $"{EmailBodies.Escape(inviterName)}, sizi <strong>{EmailBodies.Escape(workspaceName)}</strong> " +
                    "çalışma alanına katılmaya davet etti. Bağlantı tek kullanımlıktır ve " +
                    $"{message.ExpiresAt.ToLocalTime():dd.MM.yyyy} tarihinde geçerliliğini yitirir.",
                    "Daveti kabul et",
                    link),
                EmailBodies.Text(
                    $"{inviterName}, sizi {workspaceName} çalışma alanına katılmaya davet etti. " +
                    "Bağlantı tek kullanımlıktır.",
                    link)),
            cancellationToken);
    }
}
