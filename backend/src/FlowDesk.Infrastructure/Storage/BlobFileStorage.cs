using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FlowDesk.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.Storage;

/// <summary>
/// Keeps uploaded content in Azure Blob Storage.
/// </summary>
/// <remarks>
/// Azurite locally and a real storage account in production; nothing changes
/// but the connection string.
///
/// <para>
/// The container is private. Nothing here hands out a public URL or a shared
/// access signature: every download goes through the API, which is where the
/// membership check lives. A link that worked without one would put tenant
/// isolation in the hands of whoever happened to have the address
/// (ADR-0035).
/// </para>
/// </remarks>
public sealed class BlobFileStorage : IFileStorage, IDisposable
{
    private readonly BlobContainerClient _container;

    /// <summary>
    /// Ensures the container exists, once per process.
    /// </summary>
    /// <remarks>
    /// Lazy rather than at startup: creating it is an idempotent call, but
    /// making it a startup step would stop the API booting when storage is
    /// briefly unreachable — and the API serves every read and most writes
    /// without it.
    /// </remarks>
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _containerReady;

    public BlobFileStorage(BlobServiceClient serviceClient, IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(serviceClient);
        ArgumentNullException.ThrowIfNull(options);

        _container = serviceClient.GetBlobContainerClient(options.Value.ContainerName);
    }

    public async Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        await EnsureContainerAsync(cancellationToken);

        var blob = _container.GetBlobClient(storageKey);

        await blob.UploadAsync(
            content,
            new BlobUploadOptions
            {
                /*
                  The content type is stored so a download can hand it back. It
                  has already been checked against the allow-list; what is
                  written here is what the API will echo, so an unchecked value
                  reaching this point would be served back to a browser.
                */
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            },
            cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var blob = _container.GetBlobClient(storageKey);

        try
        {
            return await blob.OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            // The row outlived its bytes. Null lets the caller answer honestly
            // instead of failing with a storage error nobody can act on.
            return null;
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var blob = _container.GetBlobClient(storageKey);

        // Idempotent: deleting something that is not there is not a failure.
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Releases the semaphore that guards the one-time container check.
    /// </summary>
    /// <remarks>
    /// The instance is a singleton and lives as long as the process, so this
    /// runs at shutdown. It exists because the field is disposable, not because
    /// anything here needs unwinding (CA1001).
    /// </remarks>
    public void Dispose() => _gate.Dispose();

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerReady)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_containerReady)
            {
                return;
            }

            // PublicAccessType.None: the container is private and stays private.
            await _container.CreateIfNotExistsAsync(
                PublicAccessType.None, cancellationToken: cancellationToken);

            _containerReady = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}
