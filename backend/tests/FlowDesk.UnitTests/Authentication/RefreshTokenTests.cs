using FlowDesk.Domain.Authentication;
using FlowDesk.Domain.Common;

namespace FlowDesk.UnitTests.Authentication;

/// <summary>
/// The state rules <see cref="RefreshToken"/> enforces on itself.
/// </summary>
/// <remarks>
/// These rules exist so that a caller cannot produce a token in a state the
/// session model does not allow — a token exchanged twice, or one revived after
/// revocation. The application layer checks before it acts; these assertions
/// prove the entity refuses anyway.
/// </remarks>
public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void A_new_token_is_usable()
    {
        var token = RefreshToken.StartFamily(UserId, "hash", Now, Lifetime);

        Assert.True(token.IsUsable(Now));
        Assert.Null(token.UsedAt);
        Assert.Null(token.RevokedAt);
        Assert.Equal(Now + Lifetime, token.ExpiresAt);
    }

    [Fact]
    public void Starting_a_family_creates_a_new_family_identifier()
    {
        var first = RefreshToken.StartFamily(UserId, "hash-1", Now, Lifetime);
        var second = RefreshToken.StartFamily(UserId, "hash-2", Now, Lifetime);

        Assert.NotEqual(first.FamilyId, second.FamilyId);
    }

    [Fact]
    public void Continuing_a_family_keeps_the_family_identifier()
    {
        var first = RefreshToken.StartFamily(UserId, "hash-1", Now, Lifetime);

        var rotated = RefreshToken.ContinueFamily(UserId, first.FamilyId, "hash-2", Now, Lifetime);

        Assert.Equal(first.FamilyId, rotated.FamilyId);
        Assert.NotEqual(first.Id, rotated.Id);
    }

    [Fact]
    public void A_token_becomes_unusable_once_exchanged()
    {
        var token = RefreshToken.StartFamily(UserId, "hash", Now, Lifetime);

        token.MarkUsed(Now);

        Assert.False(token.IsUsable(Now));
        Assert.Equal(Now, token.UsedAt);
    }

    /// <summary>
    /// Single use is the rule that makes replay detectable. If the entity
    /// allowed a second exchange, a stolen token could be rotated forever
    /// alongside the real session.
    /// </summary>
    [Fact]
    public void A_token_cannot_be_exchanged_twice()
    {
        var token = RefreshToken.StartFamily(UserId, "hash", Now, Lifetime);
        token.MarkUsed(Now);

        Assert.Throws<DomainRuleViolationException>(() => token.MarkUsed(Now.AddMinutes(1)));
    }

    [Fact]
    public void A_revoked_token_cannot_be_exchanged()
    {
        var token = RefreshToken.StartFamily(UserId, "hash", Now, Lifetime);
        token.Revoke(Now);

        Assert.Throws<DomainRuleViolationException>(() => token.MarkUsed(Now.AddMinutes(1)));
    }

    [Fact]
    public void An_expired_token_cannot_be_exchanged()
    {
        var token = RefreshToken.StartFamily(UserId, "hash", Now, Lifetime);
        var afterExpiry = Now + Lifetime + TimeSpan.FromSeconds(1);

        Assert.True(token.IsExpired(afterExpiry));
        Assert.False(token.IsUsable(afterExpiry));
        Assert.Throws<DomainRuleViolationException>(() => token.MarkUsed(afterExpiry));
    }

    /// <summary>
    /// Revoking a family walks every token in it, and some may already be
    /// revoked. Keeping the first timestamp makes that walk safe to repeat.
    /// </summary>
    [Fact]
    public void Revoking_twice_keeps_the_first_timestamp()
    {
        var token = RefreshToken.StartFamily(UserId, "hash", Now, Lifetime);

        token.Revoke(Now);
        token.Revoke(Now.AddHours(1));

        Assert.Equal(Now, token.RevokedAt);
    }

    [Fact]
    public void A_token_must_belong_to_a_user()
    {
        Assert.Throws<DomainRuleViolationException>(
            () => RefreshToken.StartFamily(Guid.Empty, "hash", Now, Lifetime));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_token_must_carry_a_hash(string tokenHash)
    {
        Assert.Throws<DomainRuleViolationException>(
            () => RefreshToken.StartFamily(UserId, tokenHash, Now, Lifetime));
    }

    [Fact]
    public void A_token_lifetime_must_be_positive()
    {
        Assert.Throws<DomainRuleViolationException>(
            () => RefreshToken.StartFamily(UserId, "hash", Now, TimeSpan.Zero));
    }
}
