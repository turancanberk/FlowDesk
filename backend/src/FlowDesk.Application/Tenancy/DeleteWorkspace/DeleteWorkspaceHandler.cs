using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tenancy.DeleteWorkspace;

/// <summary>
/// Deletes a workspace and everything scoped to it.
/// </summary>
/// <remarks>
/// Owner only, and permanent. Unlike a customer — which is archived because its
/// history gives context to tickets and tasks (ADR-0012) — a deleted workspace
/// leaves nothing behind that would still make sense.
/// </remarks>
public sealed class DeleteWorkspaceHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public DeleteWorkspaceHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result> HandleAsync(CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.Delete))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Çalışma alanını silmek"));
        }

        var tenantId = _tenantContext.TenantId;

        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(candidate => candidate.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure(TenancyErrors.WorkspaceNotFound);
        }

        // Memberships and every tenant-owned record follow through the cascade
        // configured on their foreign keys.
        _dbContext.Tenants.Remove(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
