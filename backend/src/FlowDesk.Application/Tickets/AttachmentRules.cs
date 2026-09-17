using System.Globalization;
using System.Text;
using FlowDesk.Domain.Common;

namespace FlowDesk.Application.Tickets;

/// <summary>
/// What may be uploaded, and under what name.
/// </summary>
/// <remarks>
/// In the application layer rather than the API, because these are product
/// rules rather than transport ones — and because the same rules have to hold
/// whatever calls them. A check that lives only in an endpoint is a check the
/// next endpoint will not have.
/// </remarks>
public static class AttachmentRules
{
    /// <summary>
    /// The content types an upload may claim.
    /// </summary>
    /// <remarks>
    /// A allow-list, not a block-list. A block-list has to be updated for every
    /// newly dangerous type and something is always missed; an allow-list fails
    /// closed, and the cost of adding a type deliberately is one line here.
    ///
    /// <para>
    /// SVG is deliberately absent although it is an image. An SVG is a document
    /// that can contain script, so serving one from our own origin would let an
    /// uploader run code against whoever opened it (docs/SECURITY.md).
    /// </para>
    /// </remarks>
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
            "image/gif",
            "image/webp",
            "application/pdf",
            "text/plain",
            "text/csv",
            "application/zip",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        };

    public static bool IsAllowedContentType(string? contentType) =>
        contentType is not null && AllowedContentTypes.Contains(contentType.Trim());

    /// <summary>
    /// Reduces an uploaded name to something safe to store and show.
    /// </summary>
    /// <remarks>
    /// The name is never used to address the bytes — the storage key is
    /// generated — so this exists to keep the <em>display</em> name honest:
    /// no directory separators, no control characters, no leading dots that
    /// hide it, and a bounded length.
    ///
    /// <para>
    /// Any path component is dropped outright. Browsers normally send the bare
    /// name, but a hand-made request can send anything, and "sanitising" a path
    /// by rewriting it is how traversal bugs survive — taking only the last
    /// segment cannot be tricked by a clever encoding.
    /// </para>
    /// </remarks>
    public static string SanitiseFileName(string? fileName)
    {
        var raw = (fileName ?? string.Empty).Trim();

        // Take the last segment, whichever separator was used. Windows and
        // POSIX separators are both stripped because either can arrive.
        var lastSeparator = raw.LastIndexOfAny(['/', '\\']);

        if (lastSeparator >= 0)
        {
            raw = raw[(lastSeparator + 1)..];
        }

        var builder = new StringBuilder(raw.Length);

        foreach (var character in raw)
        {
            // Control characters can break a Content-Disposition header apart;
            // the reserved set is what a filesystem or a URL would choke on.
            if (char.IsControl(character) || character is ':' or '*' or '?' or '"' or '<' or '>' or '|')
            {
                builder.Append('_');
                continue;
            }

            builder.Append(character);
        }

        var cleaned = builder.ToString().Trim().TrimStart('.');

        if (cleaned.Length == 0)
        {
            // Everything was stripped. A name is required, so one is supplied
            // rather than refusing an otherwise valid file.
            return "dosya";
        }

        return cleaned.Length > Domain.Tickets.Attachment.MaximumFileNameLength
            ? cleaned[..Domain.Tickets.Attachment.MaximumFileNameLength]
            : cleaned;
    }

    /// <summary>
    /// Builds the key the bytes are stored under.
    /// </summary>
    /// <remarks>
    /// Generated entirely from values the server controls. The workspace id
    /// leads, so one organisation's files sit under a different prefix from
    /// another's — which means a mistake in the authorisation layer still does
    /// not put them in the same place, and a storage-level listing can be
    /// scoped to one tenant (ADR-0035).
    ///
    /// <para>
    /// The uploaded name contributes nothing. Including even a cleaned version
    /// would put attacker-influenced text into a path.
    /// </para>
    /// </remarks>
    public static string BuildStorageKey(Guid tenantId, Guid ticketId, Guid attachmentId)
    {
        if (tenantId == Guid.Empty || ticketId == Guid.Empty || attachmentId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "A storage key needs a workspace, a ticket and an attachment.");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{tenantId:N}/{ticketId:N}/{attachmentId:N}");
    }
}
