using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Customers.RestoreCustomer;

/// <summary>Brings an archived customer back into everyday lists.</summary>
public sealed class RestoreCustomerHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public RestoreCustomerHandler(
        IFlowDeskDbContext dbContext,
        ITenantContext tenantContext,
        IClock clock,
        IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _clock = clock;
        _activity = activity;
    }

    public async Task<Result> HandleAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ArchiveCustomers))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Müşteriyi arşivden çıkarmak"));
        }

        var tenantId = _tenantContext.TenantId;

        /*
          IgnoreQueryFilters is required here: the row being restored is archived,
          and the filter hides exactly those. The workspace condition is then
          re-applied by hand — without it this query would reach across every
          workspace, which is the one thing the filter was protecting.
        */
        var customer = await _dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == customerId && candidate.TenantId == tenantId,
                cancellationToken);

        if (customer is null)
        {
            return Result.Failure(CustomerErrors.NotFound);
        }

        customer.Restore(_clock.UtcNow);
        _activity.Record(
            ActivityType.CustomerRestored,
            ActivitySubject.Customer,
            customer.Id,
            new CustomerActivityPayload(customer.Name));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
