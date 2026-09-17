using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets.DeleteTicket;

/// <summary>
/// Removes a ticket and its comments for good.
/// </summary>
/// <remarks>
/// A real delete, not an archive. Customers are archived because a company you
/// stop working with is still part of your history (ADR-0012); a ticket raised
/// by mistake or containing something that should never have been typed is not,
/// and leaving it half-present would defeat the point. Restricted to Admin and
/// Owner for that reason.
/// </remarks>
public sealed class DeleteTicketHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly ITenantContext _tenantContext;

    public DeleteTicketHandler(
        IFlowDeskDbContext dbContext,
        IFileStorage fileStorage,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _tenantContext = tenantContext;
    }

    public async Task<Result> HandleAsync(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.DeleteTickets))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Talep silmek"));
        }

        var ticket = await TicketWorkflow.FindAsync(_dbContext, ticketId, cancellationToken);

        if (ticket is null)
        {
            return Result.Failure(TicketErrors.NotFound);
        }

        /*
          Attachment rows go with the ticket through the cascade, but the bytes
          do not: nothing in the database can reach object storage. Their keys
          are read first and the blobs removed afterwards, otherwise a deleted
          ticket would leave its files paid for and unreachable for ever.
        */
        var storageKeys = await _dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.TicketId == ticketId)
            .Select(attachment => attachment.StorageKey)
            .ToListAsync(cancellationToken);

        // Comments and attachment rows go with it through the cascade
        // configured on the foreign keys.
        _dbContext.Tickets.Remove(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var storageKey in storageKeys)
        {
            await _fileStorage.DeleteAsync(storageKey, cancellationToken);
        }

        return Result.Success();
    }
}
