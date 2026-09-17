using System.Net;
using System.Net.Http.Json;
using FlowDesk.Api.Contracts;
using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Tenancy;
using FlowDesk.IntegrationTests.Support;

namespace FlowDesk.IntegrationTests.Notifications;

/// <summary>
/// Reading and clearing one's own notifications.
/// </summary>
/// <remarks>
/// Notifications are written by a consumer in the worker, which does not run in
/// these tests. The rows are created directly so the endpoint is exercised on
/// its own — what the consumer decides to write is covered by
/// <c>NotificationConsumerTests</c>.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class NotificationApiTests
{
    private readonly PostgresContainerFixture _postgres;

    public NotificationApiTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_empty_feed_reports_no_unread()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var feed = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Empty(feed.Items);
        Assert.Equal(0, feed.UnreadCount);
    }

    /// <summary>
    /// Unread notices come first, then the newest.
    /// </summary>
    /// <remarks>
    /// The two together are what makes the feed useful: what needs attention,
    /// in the order it arrived.
    /// </remarks>
    [Fact]
    public async Task Unread_notices_come_first_then_the_newest()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var tenantId = await ReadTenantIdAsync(workspace.Slug);

        var older = await WriteAsync(tenantId, workspace.User.Id, "eski", DateTimeOffset.UtcNow.AddHours(-2));
        var newer = await WriteAsync(tenantId, workspace.User.Id, "yeni", DateTimeOffset.UtcNow.AddHours(-1));

        // The newest one is read, so it should fall below the older unread one.
        await MarkReadAsync(workspace.Client, workspace.Slug, [newer]);

        var feed = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal([older, newer], feed.Items.Select(item => item.Id));
        Assert.Equal(1, feed.UnreadCount);
    }

    [Fact]
    public async Task Marking_without_ids_clears_everything()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        var tenantId = await ReadTenantIdAsync(workspace.Slug);

        await WriteAsync(tenantId, workspace.User.Id, "bir", DateTimeOffset.UtcNow);
        await WriteAsync(tenantId, workspace.User.Id, "iki", DateTimeOffset.UtcNow);

        await MarkReadAsync(workspace.Client, workspace.Slug, []);

        var feed = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal(0, feed.UnreadCount);
        // Cleared, not deleted: the record of what happened stays.
        Assert.Equal(2, feed.Items.Count);
        Assert.All(feed.Items, item => Assert.True(item.IsRead));
    }

    /// <summary>
    /// A person sees only their own notices, even from a colleague in the same
    /// workspace.
    /// </summary>
    /// <remarks>
    /// The workspace filter does not cover this: colleagues share a workspace.
    /// What keeps one person's notices out of another's feed is the recipient
    /// condition, and it is the kind of check that gets left out precisely
    /// because the filter looks like it is already doing the work
    /// (docs/SECURITY.md).
    /// </remarks>
    [Fact]
    public async Task A_colleague_never_sees_another_persons_notices()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);
        using var workspace = await TestWorkspace.CreateAsync(factory, Cancellation);

        using var colleague = await TeamTestClient.AddMemberAsync(
            factory, workspace.Client, workspace.Slug, MembershipRole.Agent, Cancellation);

        var tenantId = await ReadTenantIdAsync(workspace.Slug);
        var ownersNotice = await WriteAsync(
            tenantId, workspace.User.Id, "sahibe ait", DateTimeOffset.UtcNow);

        var colleagueFeed = await ReadAsync(colleague.Client, workspace.Slug);

        Assert.Empty(colleagueFeed.Items);

        // And naming the id outright changes nothing.
        await MarkReadAsync(colleague.Client, workspace.Slug, [ownersNotice]);

        var ownerFeed = await ReadAsync(workspace.Client, workspace.Slug);

        Assert.Equal(1, ownerFeed.UnreadCount);
    }

    /// <summary>
    /// Notices do not cross workspaces.
    /// </summary>
    /// <remarks>
    /// A security boundary, not a convenience check (docs/SECURITY.md). A person
    /// who belongs to two organisations sees each one's notices only while they
    /// are in it.
    /// </remarks>
    [Fact]
    public async Task Notices_are_scoped_to_the_workspace_they_belong_to()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var user = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);
        var first = await WorkspaceTestClient.CreateOwnedAsync(user.Client, Cancellation);
        var second = await WorkspaceTestClient.CreateOwnedAsync(
            user.Client, Cancellation, "Kuzey Yazılım");

        var firstTenantId = await ReadTenantIdAsync(first.Slug);
        await WriteAsync(firstTenantId, user.Id, "ilk alan", DateTimeOffset.UtcNow);

        var inFirst = await ReadAsync(user.Client, first.Slug);
        var inSecond = await ReadAsync(user.Client, second.Slug);

        Assert.Single(inFirst.Items);
        Assert.Empty(inSecond.Items);
    }

    [Fact]
    public async Task A_stranger_cannot_read_a_workspaces_notifications()
    {
        await using var factory = new FlowDeskApiFactory(_postgres.ConnectionString);

        using var owner = await TestWorkspace.CreateAsync(factory, Cancellation);
        using var stranger = await AuthTestClient.SignInNewUserAsync(factory, Cancellation);

        using var response = await stranger.Client.GetAsync(
            new Uri($"/api/workspaces/{owner.Slug}/notifications", UriKind.Relative), Cancellation);

        // 404, not 403: a 403 would confirm the workspace exists (ADR-0007).
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<NotificationFeedResponse> ReadAsync(HttpClient client, string slug)
    {
        using var response = await client.GetAsync(
            new Uri($"/api/workspaces/{slug}/notifications", UriKind.Relative), Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var feed = await response.Content.ReadFromJsonAsync<NotificationFeedResponse>(
            FlowDeskJson.Options, Cancellation);

        Assert.NotNull(feed);

        return feed;
    }

    private static async Task MarkReadAsync(HttpClient client, string slug, Guid[] ids)
    {
        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/workspaces/{slug}/notifications/read", UriKind.Relative),
            new MarkNotificationsReadRequest(ids),
            FlowDeskJson.Options,
            Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<Guid> ReadTenantIdAsync(string slug)
    {
        await using var dbContext = _postgres.CreateDbContext();

        var tenant = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(
                dbContext.Tenants.Where(candidate => candidate.Slug == slug), Cancellation);

        Assert.NotNull(tenant);

        return tenant.Id;
    }

    /// <summary>Writes a notification the way a consumer would.</summary>
    private async Task<Guid> WriteAsync(
        Guid tenantId,
        Guid userId,
        string subject,
        DateTimeOffset createdAt)
    {
        await using var dbContext = _postgres.CreateDbContext();

        var notification = Notification.Create(
            tenantId,
            userId,
            NotificationType.TicketAssigned,
            $$"""{"subject":"{{subject}}"}""",
            createdAt);

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(Cancellation);

        return notification.Id;
    }
}
