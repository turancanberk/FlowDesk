using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;

namespace FlowDesk.Application.Tickets.UpdateTicket;

public sealed class UpdateTicketHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public UpdateTicketHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Result<TicketDetail>> HandleAsync(
        Guid ticketId,
        UpdateTicketCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageTickets))
        {
            return Result.Failure<TicketDetail>(TenancyErrors.InsufficientRole("Talep düzenlemek"));
        }

        var ticket = await TicketWorkflow.FindAsync(_dbContext, ticketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<TicketDetail>(TicketErrors.NotFound);
        }

        if (ticket.CustomerId != command.CustomerId)
        {
            var customerCheck = await TicketGuards.EnsureCustomerBelongsToWorkspaceAsync(
                _dbContext, _tenantContext.TenantId, command.CustomerId, cancellationToken);

            if (customerCheck.IsFailure)
            {
                return Result.Failure<TicketDetail>(customerCheck.Error);
            }

            ticket.ReassignCustomer(command.CustomerId, _clock.UtcNow);
        }

        ticket.UpdateDetails(command.Subject, command.Description, _clock.UtcNow);
        ticket.ChangePriority(command.Priority, _clock.UtcNow);

        /*
          Tells EF which version this edit was based on, rather than the one it
          happens to have just read. Without this line the UPDATE would carry the
          version from a fresh read and always match — the conflict would be
          detected only in the split second between our own read and write,
          which is not the collision that matters. The one that matters is a
          colleague saving while this form sat open.
        */
        _dbContext.Tickets.Entry(ticket).Property(entity => entity.Version).OriginalValue =
            command.Version;

        var save = await TicketWorkflow.SaveAsync(_dbContext, cancellationToken);

        if (save.IsFailure)
        {
            return Result.Failure<TicketDetail>(save.Error);
        }

        return Result.Success(
            await TicketWorkflow.ToDetailAsync(_dbContext, _accountStore, ticket, cancellationToken));
    }
}
