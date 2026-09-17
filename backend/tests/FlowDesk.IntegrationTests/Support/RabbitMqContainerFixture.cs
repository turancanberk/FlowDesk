using Testcontainers.RabbitMq;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Starts a real RabbitMQ broker once for the whole test run.
/// </summary>
/// <remarks>
/// A real broker rather than an in-memory stand-in, for the same reason the
/// database is real: the behaviour under test is the broker's. Exchange and
/// queue declaration, topic routing, acknowledgement and redelivery are what
/// the messaging code exists to get right, and a substitute would agree with
/// whatever the code did.
/// </remarks>
public sealed class RabbitMqContainerFixture : IAsyncLifetime
{
    private const string RabbitMqImage = "rabbitmq:4-management-alpine";

    public const string UserName = "flowdesk";

    /// <summary>
    /// A password that exists only inside the test process. It is not a secret
    /// and is not the credential any deployment uses.
    /// </summary>
    public const string Password = "flowdesk_test_password";

    // The image is passed to the constructor: the parameterless overload is
    // obsolete and pins an image version the caller cannot see.
    private readonly RabbitMqContainer _container = new RabbitMqBuilder(RabbitMqImage)
        .WithUsername(UserName)
        .WithPassword(Password)
        .WithCleanUp(true)
        .Build();

    public string Host => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(5672);

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
