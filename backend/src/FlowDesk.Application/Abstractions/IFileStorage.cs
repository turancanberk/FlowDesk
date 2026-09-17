namespace FlowDesk.Application.Abstractions;

/// <summary>
/// Stores and retrieves uploaded file content.
/// </summary>
/// <remarks>
/// Content only. What a file <em>is</em> — which ticket it belongs to, who
/// uploaded it, what it is called — lives in the database; this holds the bytes
/// and nothing else. Keeping them apart means the record can be authorised,
/// queried and deleted without the storage service being involved.
///
/// <para>
/// The key is supplied by the caller and is generated server-side. It is never
/// derived from a name the uploader chose: a name containing <c>../</c> would
/// otherwise write into another workspace's prefix (ADR-0035).
/// </para>
/// </remarks>
public interface IFileStorage
{
    /// <summary>Writes the content under <paramref name="storageKey"/>.</summary>
    Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Opens the content for reading, or null when nothing is stored there.
    /// </summary>
    /// <remarks>
    /// Null rather than an exception: a record whose blob has gone missing is a
    /// state the caller has to handle, not an error it can do nothing about.
    /// The caller disposes the stream.
    /// </remarks>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the content.
    /// </summary>
    /// <remarks>
    /// Idempotent: deleting something that is not there is not a failure.
    /// </remarks>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
