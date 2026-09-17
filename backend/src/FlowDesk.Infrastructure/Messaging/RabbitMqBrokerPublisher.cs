using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>Hands messages to the topic exchange.</summary>
/// <remarks>
/// Only the outbox processor uses this. Use cases queue through
/// <c>IMessagePublisher</c>, which writes a row instead — publishing straight
/// from a use case cannot be made atomic with its own commit (ADR-0033).
/// </remarks>
public sealed partial class RabbitMqBrokerPublisher : IBrokerPublisher
{
    private readonly RabbitMqConnection _connection;
    private readonly MessagingOptions _options;
    private readonly ILogger<RabbitMqBrokerPublisher> _logger;

    public RabbitMqBrokerPublisher(
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger<RabbitMqBrokerPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(
        Guid messageId,
        string messageType,
        string routingKey,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        /*
          Bounds how long a publish can wait on a broker that is not answering.
          Without it the processor would sit on a dead socket for as long as the
          client kept retrying, holding its row lock the whole time.
        */
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.PublishTimeoutSeconds));

        // A channel per publish: channels are cheap and are not thread-safe, so
        // sharing one across concurrent publishes would corrupt its framing.
        await using var channel = await _connection.OpenChannelAsync(timeout.Token);

        var properties = new BasicProperties
        {
            // Written to disk by the broker, so a restart does not lose
            // messages that were accepted but not yet consumed.
            Persistent = true,
            ContentType = "application/json",
            MessageId = messageId.ToString(),
            Timestamp = new AmqpTimestamp(occurredAt.ToUnixTimeSeconds()),
            /*
              The type travels as a header rather than being inferred from the
              routing key. A consumer bound to "ticket.*" needs to know what it
              received without parsing the key, and the key is a routing concern
              that may change.
            */
            Type = messageType,
        };

        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: payload,
            cancellationToken: timeout.Token);

        LogPublished(_logger, routingKey, messageId);
    }

    // Source-generated so the Guid is not boxed on every publish when debug
    // logging is switched off (CA1873).
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Mesaj yayınlandı: {RoutingKey} ({MessageId})")]
    private static partial void LogPublished(ILogger logger, string routingKey, Guid messageId);
}
