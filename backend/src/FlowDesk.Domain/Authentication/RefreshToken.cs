using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Authentication;

/// <summary>
/// A single issued refresh token belonging to a sign-in session.
/// </summary>
/// <remarks>
/// Tokens are grouped into a <em>family</em>: every rotation within one sign-in
/// keeps the same <see cref="FamilyId"/>. That grouping is what makes theft
/// detectable. A refresh token is single use, so if a token that was already
/// exchanged is presented again, either the legitimate client or an attacker is
/// replaying it — and since we cannot tell which, the whole family is revoked
/// and both are forced to sign in again.
///
/// The raw token value never reaches this entity. Only a hash is stored, so a
/// database leak does not hand out usable sessions.
/// </remarks>
public sealed class RefreshToken
{
    // EF Core materialises entities through this constructor.
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    private RefreshToken(
        Guid id,
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Groups every token issued through rotation of one sign-in.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>Hash of the token value. The raw value is never persisted.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Set when the token has been exchanged for a new one.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Set when the token was invalidated before it was used.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>True while the token can still be exchanged.</summary>
    public bool IsUsable(DateTimeOffset now) =>
        UsedAt is null && RevokedAt is null && now < ExpiresAt;

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Starts a new session family. Used when a user signs in.
    /// </summary>
    public static RefreshToken StartFamily(
        Guid userId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime) =>
        Create(userId, Guid.NewGuid(), tokenHash, now, lifetime);

    /// <summary>
    /// Continues an existing session family. Used when a token is rotated.
    /// </summary>
    public static RefreshToken ContinueFamily(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime) =>
        Create(userId, familyId, tokenHash, now, lifetime);

    private static RefreshToken Create(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A refresh token must belong to a user.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainRuleViolationException("A refresh token must carry a token hash.");
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new DomainRuleViolationException("A refresh token lifetime must be positive.");
        }

        return new RefreshToken(Guid.CreateVersion7(), userId, familyId, tokenHash, now, now + lifetime);
    }

    /// <summary>
    /// Marks this token as exchanged. A token can only be exchanged once; the
    /// second attempt is what reveals a replay.
    /// </summary>
    public void MarkUsed(DateTimeOffset now)
    {
        if (UsedAt is not null)
        {
            throw new DomainRuleViolationException(
                "A refresh token cannot be exchanged twice. Check IsUsable before rotating.");
        }

        if (RevokedAt is not null)
        {
            throw new DomainRuleViolationException("A revoked refresh token cannot be exchanged.");
        }

        if (IsExpired(now))
        {
            throw new DomainRuleViolationException("An expired refresh token cannot be exchanged.");
        }

        UsedAt = now;
    }

    /// <summary>
    /// Invalidates this token. Already-revoked tokens are left untouched so
    /// that revoking a family stays a safe, repeatable operation.
    /// </summary>
    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }
}
