using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tenancy.GetWorkspace;

/// <summary>
/// The workspace the request is scoped to.
/// </summary>
/// <remarks>
/// Membership was already verified while resolving the workspace, so this
/// handler does not re-check it. Reaching here without a resolved context would
/// be a wiring mistake, not an authorisation question.
/// </remarks>
public sealed class GetWorkspaceHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetWorkspaceHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<WorkspaceSummary>> HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        var role = _tenantContext.Role;

        var workspace = await _dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new WorkspaceSummary(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                role,
                tenant.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return workspace is null
            ? Result.Failure<WorkspaceSummary>(TenancyErrors.WorkspaceNotFound)
            : Result.Success(workspace);
    }
}
