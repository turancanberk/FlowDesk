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
    private readonly IMessagePublisher _publisher;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public AssignTicketHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        IMessagePublisher publisher,
        ITenantContext tenantContext,
        IClock clock)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _publisher = publisher;
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

        var now = _clock.UtcNow;
        var previousAssignee = ticket.AssignedUserId;

        ticket.Assign(command.AssignedUserId, now);

        /*
          Queued before saving, so the message and the assignment commit
          together (ADR-0033). Nothing reaches the broker here — this writes an
          outbox row through the same DbContext.

          Only when the ticket actually changed hands. Re-saving the same
          assignee, which the interface allows, is not news and should not
          produce a second notification.
        */
        if (command.AssignedUserId is { } assignedUserId && assignedUserId != previousAssignee)
        {
            await _publisher.PublishAsync(
                new TicketAssigned(
                    Guid.CreateVersion7(),
                    _tenantContext.TenantId,
                    now,
                    ticket.Id,
                    ticket.Number,
                    ticket.Subject,
                    assignedUserId,
                    _tenantContext.UserId,
                    _tenantContext.Slug),
                cancellationToken);
        }

        var save = await TicketWorkflow.SaveAsync(_dbContext, cancellationToken);

        if (save.IsFailure)
        {
            return Result.Failure<TicketDetail>(save.Error);
        }

        return Result.Success(
            await TicketWorkflow.ToDetailAsync(_dbContext, _accountStore, ticket, cancellationToken));
    }
}
