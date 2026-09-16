using FlowDesk.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tenancy.ListWorkspaces;

/// <summary>
/// The workspaces the caller belongs to.
/// </summary>
/// <remarks>
/// Starts from memberships rather than tenants, which is what makes the result
/// correct by construction: a workspace can only appear if the caller has a
/// membership row for it.
/// </remarks>
public sealed class ListWorkspacesHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ListWorkspacesHandler(IFlowDeskDbContext dbContext, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<WorkspaceSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id;

        /*
          A single projected join: no tracking, and only the five columns the
          switcher renders leave the database.

          The ordering is applied before the projection. Ordering after it makes
          EF try to sort by the constructed record rather than by a column, and
          the query then cannot be translated at all.

          Name is not unique across workspaces, so Id breaks ties; without a
          tiebreaker the order of equally-named rows is undefined.
        */
        return await _dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(
                _dbContext.Tenants,
                membership => membership.TenantId,
                tenant => tenant.Id,
                (membership, tenant) => new { Tenant = tenant, membership.Role })
            .OrderBy(row => row.Tenant.Name)
            .ThenBy(row => row.Tenant.Id)
            .Select(row => new WorkspaceSummary(
                row.Tenant.Id,
                row.Tenant.Name,
                row.Tenant.Slug,
                row.Role,
                row.Tenant.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
