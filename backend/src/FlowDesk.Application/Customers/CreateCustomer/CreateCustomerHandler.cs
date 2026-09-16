using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Customers;

namespace FlowDesk.Application.Customers.CreateCustomer;

public sealed class CreateCustomerHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public CreateCustomerHandler(
        IFlowDeskDbContext dbContext,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result<CustomerDetail>> HandleAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageCustomers))
        {
            return Result.Failure<CustomerDetail>(
                TenancyErrors.InsufficientRole("Müşteri oluşturmak"));
        }

        // The workspace comes from the resolved request context, never from the
        // request body: a client cannot create a record in someone else's
        // workspace by supplying its id (docs/SECURITY.md).
        var customer = Customer.Create(
            _tenantContext.TenantId,
            new CustomerDetails(
                command.Name,
                command.Email,
                command.Phone,
                command.Company,
                command.Status,
                command.Notes),
            _clock.UtcNow);

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(CustomerMapper.ToDetail(customer));
    }
}
