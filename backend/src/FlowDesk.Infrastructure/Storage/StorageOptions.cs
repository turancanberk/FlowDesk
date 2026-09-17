using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Infrastructure.Storage;

/// <summary>Where uploaded content is kept, and how much of it is allowed.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Never has a default and never appears in a committed file with a real
    /// account's key.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string ContainerName { get; set; } = "flowdesk-attachments";

    /// <summary>
    /// The largest file accepted, in bytes.
    /// </summary>
    /// <remarks>
    /// Enforced in the application <em>and</em> at the server's request limit.
    /// Checking only in the application means the whole body is read before it
    /// is refused, which is exactly what a limit is supposed to avoid.
    /// </remarks>
    [Range(1, 1_073_741_824)]
    public long MaximumFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
