using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Customers.ArchiveCustomer;

/// <summary>
/// Takes a customer out of everyday lists without deleting it.
/// </summary>
/// <remarks>
/// Customers are the one entity that is archived rather than deleted
/// (ADR-0012): their tickets and tasks only make sense with the customer
/// record behind them.
/// </remarks>
public sealed class ArchiveCustomerHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ArchiveCustomerHandler(
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
            return Result.Failure(TenancyErrors.InsufficientRole("Müşteri arşivlemek"));
        }

        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(candidate => candidate.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure(CustomerErrors.NotFound);
        }

        customer.Archive(_clock.UtcNow);
        _activity.Record(
            ActivityType.CustomerArchived,
            ActivitySubject.Customer,
            customer.Id,
            new CustomerActivityPayload(customer.Name));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
