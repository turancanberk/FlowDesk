using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Starts Azurite once for the whole test run.
/// </summary>
/// <remarks>
/// Real object storage rather than an in-memory stand-in, for the same reason
/// the database and the broker are real. What these tests check is that content
/// written under a generated key comes back under that key and nowhere else,
/// and a substitute would agree with whatever the code did.
///
/// <para>
/// Built from the generic container builder rather than a
/// <c>Testcontainers.Azurite</c> module: the module pins its own image and
/// starts blob, queue and table, and this product uses only blob.
/// </para>
/// </remarks>
public sealed class AzuriteContainerFixture : IAsyncLifetime
{
    private const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.37.0";
    private const int BlobPort = 10000;

    /// <summary>
    /// Azurite's documented development account.
    /// </summary>
    /// <remarks>
    /// Public knowledge and not a secret: it is the same key in every Azurite
    /// installation and works only against the emulator.
    /// </remarks>
    private const string AccountName = "devstoreaccount1";

    private const string AccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    // The image goes to the constructor: the parameterless overload is obsolete.
    private readonly IContainer _container = new ContainerBuilder(AzuriteImage)
        /*
          The location matters. The image's own command passes -l /data, and
          without it Azurite writes into its working directory — where it has no
          permission — and exits immediately with status 0, which reads as a
          clean shutdown rather than a failure.
        */
        .WithCommand(
            "azurite-blob", "-l", "/data", "--blobHost", "0.0.0.0", "--skipApiVersionCheck")
        .WithPortBinding(BlobPort, assignRandomHostPort: true)
        /*
          Waits for the line Azurite prints once the blob service is listening.
          A port check would pass as soon as the socket is bound, which is
          before it answers, and the first request would then fail on a race the
          test did nothing to cause.
        */
        .WithWaitStrategy(
            Wait.ForUnixContainer()
                .UntilMessageIsLogged("Azurite Blob service successfully listens"))
        .WithCleanUp(true)
        .Build();

    public string ConnectionString =>
        $"DefaultEndpointsProtocol=http;AccountName={AccountName};AccountKey={AccountKey};" +
        $"BlobEndpoint=http://{_container.Hostname}:{_container.GetMappedPublicPort(BlobPort)}/{AccountName};";

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
