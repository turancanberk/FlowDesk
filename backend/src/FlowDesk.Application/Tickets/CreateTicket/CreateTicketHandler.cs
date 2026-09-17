using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tickets;

namespace FlowDesk.Application.Tickets.CreateTicket;

/// <summary>
/// Raises a new ticket for a customer.
/// </summary>
/// <remarks>
/// Taking the ticket number and inserting the ticket happen in one transaction.
/// Otherwise a failure between them would burn a number and leave a gap in a
/// sequence that customers and staff both quote — and a rolled-back creation
/// would look like a deleted ticket.
/// </remarks>
public sealed class CreateTicketHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly IMessagePublisher _publisher;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public CreateTicketHandler(
        IFlowDeskDbContext dbContext,
        IMessagePublisher publisher,
        ITenantContext tenantContext,
        IClock clock,
        IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _tenantContext = tenantContext;
        _clock = clock;
        _activity = activity;
    }

    public async Task<Result<Guid>> HandleAsync(
        CreateTicketCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageTickets))
        {
            return Result.Failure<Guid>(TenancyErrors.InsufficientRole("Talep oluşturmak"));
        }

        var tenantId = _tenantContext.TenantId;

        var customerCheck = await TicketGuards.EnsureCustomerBelongsToWorkspaceAsync(
            _dbContext, tenantId, command.CustomerId, cancellationToken);

        if (customerCheck.IsFailure)
        {
            return Result.Failure<Guid>(customerCheck.Error);
        }

        var assigneeCheck = await TicketGuards.EnsureAssigneeIsMemberAsync(
            _dbContext, tenantId, command.AssignedUserId, cancellationToken);

        if (assigneeCheck.IsFailure)
        {
            return Result.Failure<Guid>(assigneeCheck.Error);
        }

        var ticketId = await _dbContext.ExecuteInTransactionAsync(
            async token =>
            {
                // Locks the workspace's counter row for the rest of this
                // transaction, so a simultaneous creation waits rather than
                // taking the same number.
                var number = await _dbContext.TakeNextTicketNumberAsync(tenantId, token);

                var ticket = Ticket.Create(
                    tenantId,
                    number,
                    command.CustomerId,
                    command.Subject,
                    command.Description,
                    command.Priority,
                    _tenantContext.UserId,
                    command.AssignedUserId,
                    _clock.UtcNow);

                _dbContext.Tickets.Add(ticket);

                // A ticket raised straight onto someone is an assignment like
                // any other, and the person it lands on should hear about it.
                if (command.AssignedUserId is { } assignedUserId)
                {
                    await _publisher.PublishAsync(
                        new TicketAssigned(
                            Guid.CreateVersion7(),
                            tenantId,
                            _clock.UtcNow,
                            ticket.Id,
                            ticket.Number,
                            ticket.Subject,
                            assignedUserId,
                            _tenantContext.UserId,
                            _tenantContext.Slug),
                        token);
                }

                _activity.Record(
                    ActivityType.TicketCreated,
                    ActivitySubject.Ticket,
                    ticket.Id,
                    new TicketActivityPayload(ticket.Number, ticket.Subject));

                await _dbContext.SaveChangesAsync(token);

                return ticket.Id;
            },
            cancellationToken);

        return Result.Success(ticketId);
    }
}
