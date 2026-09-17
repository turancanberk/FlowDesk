using System.Text.Json;
using FlowDesk.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>Publishes integration messages to the topic exchange.</summary>
public sealed partial class RabbitMqMessagePublisher : IMessagePublisher
{
    private readonly RabbitMqConnection _connection;
    private readonly MessagingOptions _options;
    private readonly ILogger<RabbitMqMessagePublisher> _logger;

    public RabbitMqMessagePublisher(
        RabbitMqConnection connection,
        IOptions<MessagingOptions> options,
        ILogger<RabbitMqMessagePublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IntegrationMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        /*
          Bounds how long a publish can wait on a broker that is not answering.
          Without it a request thread would sit on a dead socket for as long as
          the client kept retrying, and the caller would see a hung page rather
          than an error.
        */
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.PublishTimeoutSeconds));

        // A channel per publish: channels are cheap and are not thread-safe, so
        // sharing one across concurrent requests would corrupt its framing.
        await using var channel = await _connection.OpenChannelAsync(timeout.Token);

        var body = JsonSerializer.SerializeToUtf8Bytes(
            message,
            message.GetType(),
            FlowDeskMessageJson.Options);

        var properties = new BasicProperties
        {
            // Written to disk by the broker, so a restart does not lose
            // messages that were accepted but not yet consumed.
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.MessageId.ToString(),
            Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
            /*
              The message type travels as a header rather than being inferred
              from the routing key. A consumer bound to "ticket.*" needs to know
              what it received without parsing the key, and the key is a routing
              concern that may change.
            */
            Type = message.GetType().Name,
        };

        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: message.RoutingKey,
            // Waits for the broker to confirm the message was accepted. Without
            // it, publishing is fire-and-forget and a rejected message would
            // look like a successful one.
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: timeout.Token);

        LogPublished(_logger, message.RoutingKey, message.MessageId);
    }

    // Source-generated so the Guid is not boxed on every publish when debug
    // logging is switched off (CA1873).
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Mesaj yayınlandı: {RoutingKey} ({MessageId})")]
    private static partial void LogPublished(ILogger logger, string routingKey, Guid messageId);
}
