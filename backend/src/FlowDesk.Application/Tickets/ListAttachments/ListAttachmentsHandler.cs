using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets.ListAttachments;

/// <summary>A ticket's files, oldest first.</summary>
public sealed class ListAttachmentsHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ListAttachmentsHandler(IFlowDeskDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<Result<IReadOnlyCollection<AttachmentItem>>> HandleAsync(
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewTickets))
        {
            return Result.Failure<IReadOnlyCollection<AttachmentItem>>(
                TenancyErrors.InsufficientRole("Talep görüntülemek"));
        }

        var ticketExists = await _dbContext.Tickets
            .AsNoTracking()
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken);

        if (!ticketExists)
        {
            return Result.Failure<IReadOnlyCollection<AttachmentItem>>(TicketErrors.NotFound);
        }

        IReadOnlyCollection<AttachmentItem> attachments = await _dbContext.Attachments
            .AsNoTracking()
            .Where(attachment => attachment.TicketId == ticketId)
            .OrderBy(attachment => attachment.CreatedAt)
            .Select(attachment => new AttachmentItem(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.SizeInBytes,
                attachment.UploadedByUserId,
                attachment.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(attachments);
    }
}
