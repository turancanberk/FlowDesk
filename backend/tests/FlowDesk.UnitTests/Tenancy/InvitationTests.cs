using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.UnitTests.Tenancy;

/// <summary>
/// The rules that make an invitation link safe to send.
/// </summary>
public sealed class InvitationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid CreatedBy = Guid.CreateVersion7();
    private const string Email = "ayse.demir@ornek.test";

    private static Invitation CreateInvitation(string email = Email) =>
        Invitation.Create(TenantId, email, MembershipRole.Agent, "hash", CreatedBy, Now, Lifetime);

    [Fact]
    public void A_new_invitation_is_pending()
    {
        var invitation = CreateInvitation();

        Assert.True(invitation.IsPending(Now));
        Assert.Null(invitation.AcceptedAt);
        Assert.Null(invitation.RevokedAt);
        Assert.Equal(Now + Lifetime, invitation.ExpiresAt);
    }

    /// <summary>
    /// Addresses are normalised with invariant lowercasing. Turkish culture
    /// rules map "I" to the dotless "ı", which would stop an address typed in
    /// capitals from matching the one stored — and the invitation would refuse
    /// the very person it was sent to.
    /// </summary>
    [Theory]
    [InlineData("AYSE.DEMIR@ORNEK.TEST")]
    [InlineData("  ayse.demir@ornek.test  ")]
    [InlineData("Ayse.Demir@Ornek.Test")]
    public void Addresses_are_normalised_before_matching(string candidate)
    {
        var invitation = CreateInvitation();

        Assert.True(invitation.MatchesEmail(candidate));
    }

    [Fact]
    public void An_invitation_is_accepted_by_the_address_it_was_sent_to()
    {
        var invitation = CreateInvitation();

        invitation.Accept("AYSE.DEMIR@ORNEK.TEST", Now);

        Assert.Equal(Now, invitation.AcceptedAt);
        Assert.False(invitation.IsPending(Now));
    }

    /// <summary>
    /// Without this, a forwarded link could be redeemed by whoever received it.
    /// </summary>
    [Fact]
    public void An_invitation_cannot_be_accepted_by_a_different_address()
    {
        var invitation = CreateInvitation();

        Assert.Throws<DomainRuleViolationException>(
            () => invitation.Accept("baskasi@ornek.test", Now));
        Assert.Null(invitation.AcceptedAt);
    }

    [Fact]
    public void An_invitation_cannot_be_accepted_twice()
    {
        var invitation = CreateInvitation();
        invitation.Accept(Email, Now);

        Assert.Throws<DomainRuleViolationException>(
            () => invitation.Accept(Email, Now.AddMinutes(1)));
    }

    [Fact]
    public void An_expired_invitation_cannot_be_accepted()
    {
        var invitation = CreateInvitation();
        var afterExpiry = Now + Lifetime + TimeSpan.FromSeconds(1);

        Assert.True(invitation.IsExpired(afterExpiry));
        Assert.False(invitation.IsPending(afterExpiry));
        Assert.Throws<DomainRuleViolationException>(() => invitation.Accept(Email, afterExpiry));
    }

    [Fact]
    public void A_withdrawn_invitation_cannot_be_accepted()
    {
        var invitation = CreateInvitation();
        invitation.Revoke(Now);

        Assert.Throws<DomainRuleViolationException>(
            () => invitation.Accept(Email, Now.AddMinutes(1)));
    }

    [Fact]
    public void An_accepted_invitation_cannot_be_withdrawn()
    {
        var invitation = CreateInvitation();
        invitation.Accept(Email, Now);

        // The person is a member now; withdrawing the invitation would not undo
        // that, so the operation is refused rather than silently doing nothing.
        Assert.Throws<DomainRuleViolationException>(() => invitation.Revoke(Now.AddMinutes(1)));
    }

    [Fact]
    public void Withdrawing_twice_keeps_the_first_timestamp()
    {
        var invitation = CreateInvitation();

        invitation.Revoke(Now);
        invitation.Revoke(Now.AddHours(1));

        Assert.Equal(Now, invitation.RevokedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_invitation_must_have_an_address(string email)
    {
        Assert.Throws<DomainRuleViolationException>(() => CreateInvitation(email));
    }

    [Fact]
    public void An_invitation_must_carry_a_token_hash()
    {
        Assert.Throws<DomainRuleViolationException>(
            () => Invitation.Create(TenantId, Email, MembershipRole.Agent, "", CreatedBy, Now, Lifetime));
    }

    [Fact]
    public void An_invitation_must_belong_to_a_workspace()
    {
        Assert.Throws<DomainRuleViolationException>(
            () => Invitation.Create(Guid.Empty, Email, MembershipRole.Agent, "hash", CreatedBy, Now, Lifetime));
    }

    [Fact]
    public void An_undefined_role_is_rejected()
    {
        Assert.Throws<DomainRuleViolationException>(
            () => Invitation.Create(TenantId, Email, (MembershipRole)99, "hash", CreatedBy, Now, Lifetime));
    }
}
