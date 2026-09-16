using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Tenancy;

/// <summary>
/// A pending offer of membership in a workspace.
/// </summary>
/// <remarks>
/// The invitation link is the only credential involved, so it is treated like
/// one: the raw token never reaches this entity, only a hash of it, and the
/// invitation is single use and time limited.
///
/// Acceptance is also bound to the invited address. Without that, anyone
/// holding the link — forwarded, logged, or found in a browser history — could
/// join the workspace as someone else.
/// </remarks>
public sealed class Invitation : ITenantOwned
{
    public const int MaximumEmailLength = 256;

    private Invitation()
    {
        Email = string.Empty;
        TokenHash = string.Empty;
    }

    private Invitation(
        Guid id,
        Guid tenantId,
        string email,
        MembershipRole role,
        string tokenHash,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Normalised address the invitation was sent to.</summary>
    public string Email { get; private set; }

    /// <summary>The role the invited person will hold once they accept.</summary>
    public MembershipRole Role { get; private set; }

    /// <summary>Hash of the invitation token. The raw value is never persisted.</summary>
    public string TokenHash { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Set once the invitation has been used. A used invitation cannot be reused.</summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>Set when the invitation was withdrawn before it was accepted.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsPending(DateTimeOffset now) =>
        AcceptedAt is null && RevokedAt is null && now < ExpiresAt;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public static Invitation Create(
        Guid tenantId,
        string email,
        MembershipRole role,
        string tokenHash,
        Guid createdByUserId,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleViolationException("An invitation must belong to a workspace.");
        }

        var normalisedEmail = NormaliseEmail(email);

        if (normalisedEmail.Length == 0)
        {
            throw new DomainRuleViolationException("An invitation must have an e-mail address.");
        }

        if (normalisedEmail.Length > MaximumEmailLength)
        {
            throw new DomainRuleViolationException(
                $"An invitation e-mail address cannot exceed {MaximumEmailLength} characters.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new DomainRuleViolationException($"'{role}' is not a valid membership role.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainRuleViolationException("An invitation must carry a token hash.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainRuleViolationException("An invitation lifetime must be positive.");
        }

        return new Invitation(
            Guid.CreateVersion7(),
            tenantId,
            normalisedEmail,
            role,
            tokenHash,
            createdByUserId,
            now,
            now + lifetime);
    }

    /// <summary>
    /// Marks the invitation as used by the given address.
    /// </summary>
    /// <remarks>
    /// The address check lives here rather than at the call site because it is
    /// the rule that stops a forwarded link from being redeemed by whoever
    /// received it.
    /// </remarks>
    public void Accept(string acceptingEmail, DateTimeOffset now)
    {
        if (AcceptedAt is not null)
        {
            throw new DomainRuleViolationException(
                "This invitation has already been accepted. Check IsPending before accepting.");
        }

        if (RevokedAt is not null)
        {
            throw new DomainRuleViolationException("A withdrawn invitation cannot be accepted.");
        }

        if (IsExpired(now))
        {
            throw new DomainRuleViolationException("An expired invitation cannot be accepted.");
        }

        if (!MatchesEmail(acceptingEmail))
        {
            throw new DomainRuleViolationException(
                "An invitation can only be accepted by the address it was sent to.");
        }

        AcceptedAt = now;
    }

    /// <summary>
    /// Withdraws the invitation. Already-withdrawn invitations are left
    /// untouched so that revoking stays safe to repeat.
    /// </summary>
    public void Revoke(DateTimeOffset now)
    {
        if (AcceptedAt is not null)
        {
            throw new DomainRuleViolationException(
                "An accepted invitation cannot be withdrawn. Remove the member instead.");
        }

        RevokedAt ??= now;
    }

    public bool MatchesEmail(string candidate) =>
        string.Equals(Email, NormaliseEmail(candidate), StringComparison.Ordinal);

    /// <summary>
    /// Trims and lowercases the address.
    /// </summary>
    /// <remarks>
    /// Invariant lowercasing is deliberate. Turkish culture rules map "I" to
    /// the dotless "ı", so an address typed as "AYSE@ORNEK.TEST" would stop
    /// matching one stored from "ayse@ornek.test" — and the invitation would
    /// silently refuse the very person it was sent to.
    /// </remarks>
    private static string NormaliseEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();
}
