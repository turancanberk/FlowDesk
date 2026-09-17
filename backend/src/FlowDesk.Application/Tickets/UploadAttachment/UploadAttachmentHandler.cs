using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;
using FlowDesk.Application.Tenancy;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Application.Tickets.UploadAttachment;

/// <summary>
/// Attaches a file to a ticket.
/// </summary>
/// <remarks>
/// The bytes are written before the row, and that order is deliberate. If the
/// write to storage fails, nothing is saved and there is no record pointing at
/// content that does not exist. The opposite order would leave a row whose
/// download always fails.
///
/// <para>
/// The cost is that a failure after the upload but before the commit leaves
/// bytes nobody references. That is the better leak: wasted storage is cheap to
/// find and clean, a broken download is visible to a customer (ADR-0035).
/// </para>
/// </remarks>
public sealed class UploadAttachmentHandler
{
    private readonly IFlowDeskDbContext _dbContext;
    private readonly IFileStorage _fileStorage;
    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;
    private readonly long _maximumFileSizeBytes;

    public UploadAttachmentHandler(
        IFlowDeskDbContext dbContext,
        IFileStorage fileStorage,
        IAttachmentLimits limits,
        ITenantContext tenantContext,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(limits);

        _dbContext = dbContext;
        _fileStorage = fileStorage;
        _tenantContext = tenantContext;
        _clock = clock;
        _maximumFileSizeBytes = limits.MaximumFileSizeBytes;
    }

    public async Task<Result<AttachmentItem>> HandleAsync(
        Guid ticketId,
        UploadAttachmentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Uploading changes the ticket, so it takes the same permission as any
        // other change to it.
        if (!WorkspacePermissions.IsGranted(_tenantContext.Role, WorkspaceAction.ManageTickets))
        {
            return Result.Failure<AttachmentItem>(TenancyErrors.InsufficientRole("Dosya eklemek"));
        }

        if (command.SizeInBytes <= 0)
        {
            return Result.Failure<AttachmentItem>(TicketErrors.AttachmentEmpty);
        }

        if (command.SizeInBytes > _maximumFileSizeBytes)
        {
            return Result.Failure<AttachmentItem>(
                TicketErrors.AttachmentTooLarge(_maximumFileSizeBytes));
        }

        if (!AttachmentRules.IsAllowedContentType(command.ContentType))
        {
            return Result.Failure<AttachmentItem>(TicketErrors.AttachmentTypeNotAllowed);
        }

        // The query filter scopes this to the workspace, so a ticket belonging
        // to another organisation is simply not found (ADR-0024).
        var ticketExists = await _dbContext.Tickets
            .AsNoTracking()
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken);

        if (!ticketExists)
        {
            return Result.Failure<AttachmentItem>(TicketErrors.NotFound);
        }

        var tenantId = _tenantContext.TenantId;
        var attachmentId = Guid.CreateVersion7();

        // Generated from values the server controls; the uploaded name
        // contributes nothing (ADR-0035).
        var storageKey = AttachmentRules.BuildStorageKey(tenantId, ticketId, attachmentId);
        var contentType = command.ContentType!.Trim();

        await _fileStorage.SaveAsync(storageKey, command.Content, contentType, cancellationToken);

        var attachment = Attachment.Create(
            tenantId,
            ticketId,
            AttachmentRules.SanitiseFileName(command.FileName),
            contentType,
            command.SizeInBytes,
            storageKey,
            _tenantContext.UserId,
            _clock.UtcNow);

        _dbContext.Attachments.Add(attachment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AttachmentItem(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeInBytes,
            attachment.UploadedByUserId,
            attachment.CreatedAt));
    }
}

/// <summary>
/// The upload size the deployment allows.
/// </summary>
/// <remarks>
/// A contract rather than an options type, so the application layer states the
/// limit it needs without reaching for infrastructure's configuration
/// (ADR-0021).
/// </remarks>
public interface IAttachmentLimits
{
    long MaximumFileSizeBytes { get; }
}
