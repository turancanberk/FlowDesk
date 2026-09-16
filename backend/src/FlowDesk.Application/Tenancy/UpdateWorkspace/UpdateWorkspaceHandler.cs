using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tenancy.UpdateWorkspace;

public sealed class UpdateWorkspaceHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public UpdateWorkspaceHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<WorkspaceSummary>> HandleAsync(
        UpdateWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.Update))
        {
            return Result.Failure<WorkspaceSummary>(
                TenancyErrors.InsufficientRole("Çalışma alanı ayarlarını değiştirmek"));
        }

        var tenantId = _tenantContext.TenantId;

        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(candidate => candidate.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<WorkspaceSummary>(TenancyErrors.WorkspaceNotFound);
        }

        tenant.Rename(command.Name);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new WorkspaceSummary(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            _tenantContext.Role,
            tenant.CreatedAt));
    }
}
