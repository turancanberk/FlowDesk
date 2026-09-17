using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Common;

namespace FlowDesk.Application.Tickets.ChangeTicketStatus;

/// <summary>
/// Moves a ticket to another status.
/// </summary>
/// <remarks>
/// No row version is required here, unlike editing a ticket. A status change is
/// a single-field statement of intent, and the domain's transition table is
/// already the guard: if a colleague has moved the ticket in the meantime, the
/// requested move is either still legal, a no-op, or refused outright. There is
/// no text to silently overwrite, so demanding a version would only make a
/// one-click action fail for no benefit (ADR-0013).
/// </remarks>
public sealed class ChangeTicketStatusHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly IUserAccountStore _accountStore;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ChangeTicketStatusHandler(
        IFlowDeskDbContext dbContext,
        IUserAccountStore accountStore,
        ITenantContext tenantContext,
        IClock clock,
        IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _accountStore = accountStore;
        _tenantContext = tenantContext;
        _clock = clock;
        _activity = activity;
    }

    public async Task<Result<TicketDetail>> HandleAsync(
        Guid ticketId,
        ChangeTicketStatusCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageTickets))
        {
            return Result.Failure<TicketDetail>(
                TenancyErrors.InsufficientRole("Talep durumunu değiştirmek"));
        }

        var ticket = await TicketWorkflow.FindAsync(_dbContext, ticketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure<TicketDetail>(TicketErrors.NotFound);
        }

        var previousStatus = ticket.Status;

        try
        {
            ticket.ChangeStatus(command.Status, _clock.UtcNow);
        }
        catch (DomainRuleViolationException)
        {
            // The entity refused the move. Reported as a conflict rather than a
            // validation error: the request was well formed, it is the ticket's
            // current state that makes it impossible.
            return Result.Failure<TicketDetail>(
                TicketErrors.InvalidTransition(previousStatus, command.Status));
        }

        // Only when it actually moved. Pressing the same button twice is a
        // no-op in the domain and should not be a line in the history either.
        if (ticket.Status != previousStatus)
        {
            _activity.Record(
                ActivityType.TicketStatusChanged,
                ActivitySubject.Ticket,
                ticket.Id,
                new TicketStatusActivityPayload(
                    ticket.Number, ticket.Subject, previousStatus, ticket.Status));
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
