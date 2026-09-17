namespace FlowDesk.Application.Tickets.UploadAttachment;

/// <param name="Content">
/// Read once and not rewound. The caller owns the stream and disposes it.
/// </param>
/// <param name="SizeInBytes">
/// What the transport reported. Checked before the stream is read, so an
/// oversized upload is refused rather than absorbed.
/// </param>
public sealed record UploadAttachmentCommand(
    string FileName,
    string? ContentType,
    long SizeInBytes,
    Stream Content);
