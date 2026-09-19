using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Team.ListMembers;

/// <summary>
/// The people in the current workspace.
/// </summary>
/// <remarks>
/// Account details live in Identity and memberships live in our own tables, so
/// this is two queries joined in memory rather than one. Both are bounded by
/// the size of a single team, and the alternative — a database-level join
/// across Identity's tables — would tie the query to Identity's schema.
/// </remarks>
public sealed class ListMembersHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;

    public ListMembersHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyList<TeamMember>>> HandleAsync(CancellationToken cancellationToken)
    {
        /*
          Every role holds this today, so the check changes nothing at runtime.
          It is here so that the matrix entry means something: without it,
          raising the entry would silently leave the team list open.
        */
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewMembers))
        {
            return Result.Failure<IReadOnlyList<TeamMember>>(
                TenancyErrors.InsufficientRole("Ekibi görüntülemek"));
        }

        var tenantId = _tenantContext.TenantId;

        var memberships = await _dbContext.Memberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId)
            .OrderByDescending(membership => membership.Role)
            .ThenBy(membership => membership.JoinedAt)
            .ToListAsync(cancellationToken);

        var ownerCount = memberships.Count(membership => membership.Role is MembershipRole.Owner);

        var accounts = await _accountStore.FindByIdsAsync(
            memberships.Select(membership => membership.UserId).ToArray(),
            cancellationToken);

        var members = memberships
            .Select(membership =>
            {
                var account = accounts.GetValueOrDefault(membership.UserId);

                return new TeamMember(
                    membership.UserId,
                    account?.Email ?? string.Empty,
                    // An account that vanished mid-request would otherwise
                    // render as a blank row with no explanation.
                    account?.DisplayName ?? "Bilinmeyen kullanıcı",
                    membership.Role,
                    membership.JoinedAt,
                    membership.Role is MembershipRole.Owner && ownerCount <= 1);
            })
            .ToList();

        return Result.Success<IReadOnlyList<TeamMember>>(members);
    }
}
