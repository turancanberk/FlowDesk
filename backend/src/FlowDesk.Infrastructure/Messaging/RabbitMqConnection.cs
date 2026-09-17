using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Owns the single broker connection and the exchange declaration.
/// </summary>
/// <remarks>
/// A connection is a TCP socket and an AMQP handshake; opening one per publish
/// would cost more than the publish. Channels are cheap and are <em>not</em>
/// thread-safe, so they are created per operation while the connection is
/// shared — which is what the RabbitMQ client documents.
///
/// Registered as a singleton and opened lazily. Opening it at startup would
/// mean the API refuses to boot when the broker is down, and the API can serve
/// every read and most writes without it.
/// </remarks>
public sealed partial class RabbitMqConnection : IAsyncDisposable
{
    private readonly MessagingOptions _options;
    private readonly ILogger<RabbitMqConnection> _logger;

    /// <summary>
    /// Serialises the opening, so a burst of first requests opens one
    /// connection rather than one each.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IConnection? _connection;
    private bool _exchangeDeclared;

    public RabbitMqConnection(
        IOptions<MessagingOptions> options,
        ILogger<RabbitMqConnection> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;
    }

    public string ExchangeName => _options.ExchangeName;

    /// <summary>
    /// Opens a channel, connecting and declaring the exchange if needed.
    /// </summary>
    /// <remarks>
    /// The caller disposes the channel. Declaring the exchange is idempotent,
    /// so doing it once per connection is enough — and doing it here rather
    /// than at startup means the topology is restored automatically after the
    /// client reconnects.
    /// </remarks>
    public async Task<IChannel> OpenChannelAsync(CancellationToken cancellationToken)
    {
        var connection = await EnsureConnectedAsync(cancellationToken);

        var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        if (!_exchangeDeclared)
        {
            await channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                // Survives a broker restart. An exchange that vanished would
                // silently drop every publish until something redeclared it.
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            _exchangeDeclared = true;
        }

        return channel;
    }

    private async Task<IConnection> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            // A connection that closed and did not recover is disposed rather
            // than left to leak its socket.
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
                _exchangeDeclared = false;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                VirtualHost = _options.VirtualHost,
                UserName = _options.UserName,
                Password = _options.Password,
                /*
                  The client reconnects and restores topology by itself. Writing
                  that loop by hand is where retry code usually goes wrong:
                  it either hammers the broker or gives up on the first blip.
                */
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                ClientProvidedName = "flowdesk",
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            _connection.ConnectionShutdownAsync += (_, args) =>
            {
                // Logged at warning, not error: a shutdown is expected during a
                // broker restart and the client recovers on its own.
                LogConnectionClosed(_logger, args.ReplyText);

                // The topology goes with the connection, so it is declared again
                // on the next channel.
                _exchangeDeclared = false;

                return Task.CompletedTask;
            };

            LogConnected(_logger, _options.Host, _options.Port, _options.VirtualHost);

            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    /*
      Source-generated log methods rather than the ILogger extension methods.
      The extensions take their arguments as object, so an int or a Guid is
      boxed on every call even when the level is switched off. These are
      generated strongly typed and check the level first (CA1873).
    */
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Mesaj kuyruğu bağlantısı kapandı: {Reason}. İstemci yeniden bağlanmayı deneyecek.")]
    private static partial void LogConnectionClosed(ILogger logger, string reason);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Mesaj kuyruğuna bağlanıldı: {Host}:{Port}{VirtualHost}")]
    private static partial void LogConnected(
        ILogger logger,
        string host,
        int port,
        string virtualHost);

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _gate.Dispose();
    }
}
