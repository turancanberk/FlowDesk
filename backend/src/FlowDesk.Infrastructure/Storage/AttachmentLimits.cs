using FlowDesk.Application.Tickets.UploadAttachment;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Storage;

/// <summary>
/// Reads the configured upload limit for the application layer.
/// </summary>
/// <remarks>
/// A one-line adapter so the use case depends on a contract it declares rather
/// than on infrastructure's options type. Without it the dependency arrow would
/// point outwards, which the architecture tests refuse (ADR-0021).
/// </remarks>
public sealed class AttachmentLimits : IAttachmentLimits
{
    private readonly StorageOptions _options;

    public AttachmentLimits(IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
    }

    public long MaximumFileSizeBytes => _options.MaximumFileSizeBytes;
}
