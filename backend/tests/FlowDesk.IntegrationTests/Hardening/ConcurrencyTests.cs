using System.Net;
using FlowDesk.Domain.Activity;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.IntegrationTests.Hardening;

/// <summary>
/// Requests that race each other end in a state one of them could have
/// produced alone.
/// </summary>
/// <remarks>
/// Every use case reads, decides and then writes. Run one at a time, that is
/// correct; run two at once, both can decide on the same stale read. These
/// tests fire the same request several times at the same instant and check
/// the result against what sequential execution would allow.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class ConcurrencyTests
{
    private const int Contenders = 6;

    private readonly PostgresContainerFixture _postgres;

    public ConcurrencyTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// A double-clicked invitation link joins the workspace once.
    /// </summary>
    [Fact]
    public async Task Simultaneous_acceptances_of_one_invitation_grant_one_membership()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var invitee = await AuthTestClient.SignInNewUserAsync(factory, Cancellation, "Elif Şahin");

        var invitation = await TeamTestClient.InviteAndReadAsync(
            workspace.Client, workspace.Slug, invitee.Email, MembershipRole.Agent, Cancellation);

        /*
          Gated on the workspace lookup, which comes after the invitation is
          read and before anything is written: when the gate opens, every
          contender has already seen the invitation as unused.
        */
        await using var gate = await TableGate.CloseAsync(_postgres.ConnectionString, "Tenants", Cancellation);

        var outcomes = await RaceAsync(
            async () =>
            {
                using var response = await TeamTestClient.AcceptAsync(
                    invitee.Client, invitation.Token, Cancellation);
                return response.StatusCode;
            },
            gate);

        Assert.Equal(1, outcomes.Count(status => status == HttpStatusCode.OK));

        // The rest see a spent invitation, and are told so the way any
        // unusable link is — never with a server error.
        Assert.All(
            outcomes.Where(status => status != HttpStatusCode.OK),
            status => Assert.Equal(HttpStatusCode.NotFound, status));

        await using var dbContext = _postgres.CreateDbContext();

        var tenantId = await dbContext.Tenants
            .Where(tenant => tenant.Slug == workspace.Slug)
            .Select(tenant => tenant.Id)
            .SingleAsync(Cancellation);

        Assert.Equal(1, await dbContext.Memberships.IgnoreQueryFilters().CountAsync(
            membership => membership.TenantId == tenantId && membership.UserId == invitee.Id,
            Cancellation));

        Assert.Equal(1, await dbContext.ActivityEvents.IgnoreQueryFilters().CountAsync(
            activity => activity.TenantId == tenantId
                && activity.Type == ActivityType.MemberJoined
                && activity.SubjectId == invitee.Id,
            Cancellation));
    }

    /// <summary>
    /// Several people assigning the same ticket at once: one change lands
    /// completely, the rest are reported as conflicts and leave no trace.
    /// </summary>
    /// <remarks>
    /// An assignment writes three things — the ticket, a history entry and an
    /// outbox message that becomes a notification and an email. A conflict
    /// that rolled back the ticket but kept the message would notify someone
    /// of work that is not theirs.
    /// </remarks>
    [Fact]
    public async Task Simultaneous_assignments_each_land_whole_or_not_at_all()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var first = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation, "Burak Çelik");
        using var second = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation, "Selin Aydın");

        var ticket = await TicketTestClient.CreateAndReadAsync(
            workspace.Client, workspace.Slug, workspace.CustomerId, Cancellation);

        var assignees = new[] { first.Id, second.Id };
        var turn = -1;

        // Gated on the history table, which is only written: every contender
        // has read the ticket at the same version before any of them saves.
        await using var gate = await TableGate.CloseAsync(
            _postgres.ConnectionString, "ActivityEvents", Cancellation);

        var outcomes = await RaceAsync(
            async () =>
            {
                var assignee = assignees[Interlocked.Increment(ref turn) % assignees.Length];

                using var response = await TicketTestClient.AssignAsync(
                    workspace.Client, workspace.Slug, ticket.Id, assignee, Cancellation);

                return (Assignee: assignee, response.StatusCode);
            },
            gate);

        Assert.All(outcomes, outcome => Assert.Contains(
            outcome.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));

        // All read the same version, so the row version lets exactly one land.
        var landed = Assert.Single(outcomes, outcome => outcome.StatusCode == HttpStatusCode.OK);

        var final = await TicketTestClient.ReadDetailAsync(
            await TicketTestClient.GetAsync(workspace.Client, workspace.Slug, ticket.Id, Cancellation),
            Cancellation);

        Assert.Equal(landed.Assignee, final.AssignedUserId);

        await using var dbContext = _postgres.CreateDbContext();

        var recorded = await dbContext.ActivityEvents.IgnoreQueryFilters().CountAsync(
            activity => activity.SubjectId == ticket.Id && activity.Type == ActivityType.TicketAssigned,
            Cancellation);

        // The payload is jsonb, so it is matched here rather than in SQL.
        var ticketIdText = ticket.Id.ToString();
        var queued = (await dbContext.OutboxMessages
                .Where(message => message.Type == "TicketAssigned")
                .Select(message => message.Payload)
                .ToListAsync(Cancellation))
            .Count(payload => payload.Contains(ticketIdText, StringComparison.OrdinalIgnoreCase));

        // The conflicts rolled back whole: one history entry, one message.
        Assert.Equal(1, recorded);
        Assert.Equal(1, queued);
    }

    /// <summary>
    /// Two owners leave at the same moment. Each sees the other still there,
    /// so each believes they are not the last — and sequentially, each would
    /// be right.
    /// </summary>
    [Fact]
    public async Task Two_owners_leaving_at_once_leave_one_behind()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var coOwner = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Owner, Cancellation, "Kerem Aksoy");

        await using var gate = await TableGate.CloseAsync(
            _postgres.ConnectionString, "ActivityEvents", Cancellation);

        var outcomes = await RaceAsync(
            [
                () => LeaveAsync(workspace.Client, workspace.Slug, workspace.User.Id),
                () => LeaveAsync(coOwner.Client, workspace.Slug, coOwner.Id),
            ],
            gate);

        Assert.Single(outcomes, status => status == HttpStatusCode.NoContent);
        Assert.Single(outcomes, status => status == HttpStatusCode.Conflict);
        Assert.Equal(1, await CountOwnersAsync(workspace.Slug));
    }

    /// <summary>
    /// Two owners demote each other at the same moment. Both were owners when
    /// their request began, so both are allowed to; only one may succeed.
    /// </summary>
    [Fact]
    public async Task Two_owners_demoting_each_other_leave_one_behind()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var coOwner = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Owner, Cancellation, "Kerem Aksoy");

        await using var gate = await TableGate.CloseAsync(
            _postgres.ConnectionString, "ActivityEvents", Cancellation);

        var outcomes = await RaceAsync(
            [
                () => DemoteAsync(workspace.Client, workspace.Slug, coOwner.Id),
                () => DemoteAsync(coOwner.Client, workspace.Slug, workspace.User.Id),
            ],
            gate);

        Assert.Single(outcomes, status => status == HttpStatusCode.NoContent);
        Assert.Equal(1, await CountOwnersAsync(workspace.Slug));
    }

    /// <summary>
    /// An owner demoted while their own request is in flight finishes it with
    /// the role they have now, not the one they had when it began.
    /// </summary>
    /// <remarks>
    /// The order is forced: the demotion reaches the database first and is
    /// held there; only then is the promotion sent. Without a fresh read of
    /// the caller's role, the promotion would go through on the strength of an
    /// ownership that had already been taken away.
    /// </remarks>
    [Fact]
    public async Task A_demoted_owner_cannot_finish_promoting_someone_to_owner()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var coOwner = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Owner, Cancellation, "Kerem Aksoy");
        using var candidate = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Admin, Cancellation, "Deniz Kurt");

        await using var gate = await TableGate.CloseAsync(
            _postgres.ConnectionString, "ActivityEvents", Cancellation);

        var demotion = DemoteAsync(workspace.Client, workspace.Slug, coOwner.Id);
        await gate.WaitUntilQueuedAsync(1, Cancellation);

        var promotion = PromoteAsync(coOwner.Client, workspace.Slug, candidate.Id);
        await gate.OpenWhenQueuedAsync(2, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, await demotion);
        Assert.Equal(HttpStatusCode.Forbidden, await promotion);
        Assert.Equal(1, await CountOwnersAsync(workspace.Slug));
    }

    private static async Task<HttpStatusCode> PromoteAsync(HttpClient client, string slug, Guid userId)
    {
        using var response = await TeamTestClient.ChangeRoleAsync(
            client, slug, userId, MembershipRole.Owner, Cancellation);
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> LeaveAsync(HttpClient client, string slug, Guid userId)
    {
        using var response = await TeamTestClient.RemoveMemberAsync(client, slug, userId, Cancellation);
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> DemoteAsync(HttpClient client, string slug, Guid userId)
    {
        using var response = await TeamTestClient.ChangeRoleAsync(
            client, slug, userId, MembershipRole.Admin, Cancellation);
        return response.StatusCode;
    }

    private async Task<int> CountOwnersAsync(string slug)
    {
        await using var dbContext = _postgres.CreateDbContext();

        var tenantId = await dbContext.Tenants
            .Where(tenant => tenant.Slug == slug)
            .Select(tenant => tenant.Id)
            .SingleAsync(Cancellation);

        return await dbContext.Memberships.IgnoreQueryFilters().CountAsync(
            membership => membership.TenantId == tenantId && membership.Role == MembershipRole.Owner,
            Cancellation);
    }

    /// <summary>
    /// Starts every contender at once and collects their results. With a gate,
    /// holds them at it until all have arrived.
    /// </summary>
    private static Task<TResult[]> RaceAsync<TResult>(
        Func<Task<TResult>> attempt,
        TableGate? gate = null) =>
        RaceAsync(Enumerable.Repeat(attempt, Contenders).ToArray(), gate);

    private static async Task<TResult[]> RaceAsync<TResult>(
        Func<Task<TResult>>[] attempts,
        TableGate? gate = null)
    {
        using var start = new SemaphoreSlim(0, attempts.Length);

        var contenders = attempts
            .Select(async attempt =>
            {
                await start.WaitAsync(Cancellation);
                return await attempt();
            })
            .ToList();

        start.Release(attempts.Length);

        if (gate is not null)
        {
            await gate.OpenWhenQueuedAsync(attempts.Length, Cancellation);
        }

        return await Task.WhenAll(contenders);
    }
}
