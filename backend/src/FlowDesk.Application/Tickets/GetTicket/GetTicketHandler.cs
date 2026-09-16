using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;

namespace FlowDesk.Application.Tickets.GetTicket;

public sealed class GetTicketHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;

    public GetTicketHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
    }

    public async Task<Result<TicketDetail>> HandleAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewTickets))
        {
            return Result.Failure<TicketDetail>(
                TenancyErrors.InsufficientRole("Talep görüntülemek"));
        }

        var ticket = await TicketWorkflow.FindAsync(_dbContext, ticketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<TicketDetail>(TicketErrors.NotFound);
        }

        return Result.Success(
            await TicketWorkflow.ToDetailAsync(_dbContext, _accountStore, ticket, cancellationToken));
    }
}
