using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Activity;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets.DeleteAttachment;

/// <summary>
/// Removes an attachment and its content.
/// </summary>
/// <remarks>
/// The row goes first, then the bytes. If the storage delete fails, the record
/// is already gone and the file is unreachable — which is the outcome the
/// person asked for; what is left behind is wasted space, not a visible file.
/// Deleting the bytes first would risk the opposite: content gone, row intact,
/// and a download that fails for everyone who tries.
/// </remarks>
public sealed class DeleteAttachmentHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IActivityRecorder _activity;
    private readonly IFileStorage _fileStorage;
    private readonly ITenantContext _tenantContext;

    public DeleteAttachmentHandler(
        IFlowDeskDbContext dbContext,
        IFileStorage fileStorage,
        ITenantContext tenantContext,
        IActivityRecorder activity)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _tenantContext = tenantContext;
        _activity = activity;
    }

    public async Task<Result> HandleAsync(
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageTickets))
        {
            return Result.Failure(TenancyErrors.InsufficientRole("Dosya silmek"));
        }

        var attachment = await _dbContext.Attachments
            .FirstOrDefaultAsync(
                candidate => candidate.Id == attachmentId && candidate.TicketId == ticketId,
                cancellationToken);

        if (attachment is null)
        {
            return Result.Failure(TicketErrors.AttachmentNotFound);
        }

        var storageKey = attachment.StorageKey;

        // Read for the history line. The ticket is still here — deleting a file
        // does not delete what it was attached to.
        var ticketNumber = await _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.Id == ticketId)
            .Select(ticket => ticket.Number)
            .FirstOrDefaultAsync(cancellationToken);

        _activity.Record(
            ActivityType.AttachmentDeleted,
            ActivitySubject.Attachment,
            attachment.Id,
            new AttachmentActivityPayload(attachment.FileName, ticketId, ticketNumber));

        _dbContext.Attachments.Remove(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _fileStorage.DeleteAsync(storageKey, cancellationToken);

        return Result.Success();
    }
}
