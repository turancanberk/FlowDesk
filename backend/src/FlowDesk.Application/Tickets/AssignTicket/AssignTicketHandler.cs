using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;

namespace FlowDesk.Application.Tickets.AssignTicket;

/// <summary>
/// Hands a ticket to a team member, or back to the unassigned queue.
/// </summary>
/// <remarks>
/// Last write wins, on purpose. "This is now Ayşe's" is a statement about the
/// present; the most recent person to say who owns a ticket is right, and a
/// conflict dialogue over a dropdown would be noise.
/// </remarks>
public sealed class AssignTicketHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public AssignTicketHandler(
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
        AssignTicketCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageTickets))
        {
            return Result.Failure<TicketDetail>(TenancyErrors.InsufficientRole("Talep atamak"));
        }

        var ticket = await TicketWorkflow.FindAsync(_dbContext, ticketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<TicketDetail>(TicketErrors.NotFound);
        }

        var assigneeCheck = await TicketGuards.EnsureAssigneeIsMemberAsync(
            _dbContext, _tenantContext.TenantId, command.AssignedUserId, cancellationToken);

        if (assigneeCheck.IsFailure)
        {
            return Result.Failure<TicketDetail>(assigneeCheck.Error);
        }

        ticket.Assign(command.AssignedUserId, _clock.UtcNow);

        var save = await TicketWorkflow.SaveAsync(_dbContext, cancellationToken);

        if (save.IsFailure)
        {
            return Result.Failure<TicketDetail>(save.Error);
        }

        return Result.Success(
            await TicketWorkflow.ToDetailAsync(_dbContext, _accountStore, ticket, cancellationToken));
    }
}
