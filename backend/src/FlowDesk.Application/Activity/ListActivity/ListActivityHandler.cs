using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Activity.ListActivity;

/// <summary>
/// The workspace's history, newest first.
/// </summary>
/// <remarks>
/// Readable by every member, including a Viewer. The feed says what the team
/// did, and hiding a team's own history from part of that team would make the
/// record less trustworthy without making anything safer — the payload already
/// carries nothing a member could not see by opening the record itself
/// (ADR-0036).
/// </remarks>
public sealed class ListActivityHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;

    public ListActivityHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<PagedResult<ActivityItem>>> HandleAsync(
        ListActivityQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.View))
        {
            return Result.Failure<PagedResult<ActivityItem>>(
                TenancyErrors.InsufficientRole("Etkinlik geçmişini görüntülemek"));
        }

        var page = new PageRequest(query.Page, query.PageSize);

        // Scoped to the workspace by the global query filter (ADR-0024).
        var events = _dbContext.ActivityEvents.AsNoTracking();

        if (query.SubjectType is { } subjectType)
        {
            events = events.Where(entry => entry.SubjectType == subjectType);
        }

        if (query.SubjectId is { } subjectId)
        {
            events = events.Where(entry => entry.SubjectId == subjectId);
        }

        if (query.Type is { } type)
        {
            events = events.Where(entry => entry.Type == type);
        }

        if (query.ActorUserId is { } actorUserId)
        {
            events = events.Where(entry => entry.ActorUserId == actorUserId);
        }

        var totalCount = await events.CountAsync(cancellationToken);

        /*
          Newest first, with the id as a tiebreaker. Version 7 ids are
          time-ordered, so within one instant they order the same way the clock
          would — and without a tiebreaker two events written in the same
          transaction could appear on two pages or on neither
          (docs/API_CONVENTIONS.md).
        */
        var rows = await events
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(entry => new
            {
                entry.Id,
                entry.ActorUserId,
                entry.Type,
                entry.SubjectType,
                entry.SubjectId,
                entry.Payload,
                entry.OccurredAt,
            })
            .ToListAsync(cancellationToken);

        var actorNames = await ResolveActorNamesAsync(rows.Select(row => row.ActorUserId), cancellationToken);

        var items = rows
            .Select(row => new ActivityItem(
                row.Id,
                row.ActorUserId,
                row.ActorUserId is null ? null : actorNames.GetValueOrDefault(row.ActorUserId.Value),
                row.Type,
                row.SubjectType,
                row.SubjectId,
                row.Payload,
                row.OccurredAt))
            .ToList();

        return Result.Success(new PagedResult<ActivityItem>(
            items, page.Page, page.PageSize, totalCount));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveActorNamesAsync(
        IEnumerable<Guid?> actorUserIds,
        CancellationToken cancellationToken)
    {
        var ids = actorUserIds
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var accounts = await _accountStore.FindByIdsAsync(ids, cancellationToken);

        return accounts.ToDictionary(entry => entry.Key, entry => entry.Value.DisplayName);
    }
}
