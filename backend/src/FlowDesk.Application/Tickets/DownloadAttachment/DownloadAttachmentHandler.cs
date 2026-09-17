using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets.DownloadAttachment;

/// <summary>
/// Opens an attachment's content for the caller.
/// </summary>
/// <remarks>
/// Every download passes through here, and that is the point. Nothing hands out
/// a public URL or a signed link, so knowing an address is never enough — the
/// membership check runs on every byte served (ADR-0035).
/// </remarks>
public sealed class DownloadAttachmentHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly ITenantContext _tenantContext;

    public DownloadAttachmentHandler(
        IFlowDeskDbContext dbContext,
        IFileStorage fileStorage,
        ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _tenantContext = tenantContext;
    }

    public async Task<Result<AttachmentDownload>> HandleAsync(
        Guid ticketId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ViewTickets))
        {
            return Result.Failure<AttachmentDownload>(
                TenancyErrors.InsufficientRole("Talep görüntülemek"));
        }

        /*
          Both conditions matter. The query filter scopes to the workspace, but
          within one workspace an attachment id from a different ticket would
          otherwise be served under whichever ticket the caller named — which
          is not a tenant leak, but is still a file reaching someone through a
          route that does not describe it.
        */
        var attachment = await _dbContext.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == attachmentId && candidate.TicketId == ticketId,
                cancellationToken);

        if (attachment is null)
        {
            return Result.Failure<AttachmentDownload>(TicketErrors.AttachmentNotFound);
        }

        var content = await _fileStorage.OpenReadAsync(attachment.StorageKey, cancellationToken);

        if (content is null)
        {
            // The row outlived its bytes. Said plainly rather than as a 404: the
            // record exists and the caller is entitled to it.
            return Result.Failure<AttachmentDownload>(TicketErrors.AttachmentContentMissing);
        }

        return Result.Success(new AttachmentDownload(
            content, attachment.FileName, attachment.ContentType));
    }
}
