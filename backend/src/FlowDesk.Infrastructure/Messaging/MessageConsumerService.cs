using System.Diagnostics;

// FlowDesk.Infrastructure.Activity (the audit recorder) shadows the type name
// inside this namespace, so the tracing type is aliased rather than renamed.
using TraceActivity = System.Diagnostics.Activity;
using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Runs the registered subscriptions for as long as the host is up.
/// </summary>
/// <remarks>
/// One background service for all subscriptions, sharing the process's single
/// connection. Each subscription gets its own channel, because a channel is not
/// thread-safe and consumers deliver on their own threads.
/// </remarks>
public sealed partial class MessageConsumerService : BackgroundService
{
    /// <summary>
    /// How many messages the broker may have in flight per consumer before it
    /// waits for an acknowledgement.
    /// </summary>
    /// <remarks>
    /// Without a limit the broker pushes the whole queue at once, which turns a
    /// backlog into a memory problem and makes a second worker useless because
    /// the first already holds everything. One at a time is the safe default for
    /// work that touches a database.
    /// </remarks>
    private const ushort PrefetchCount = 1;

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyCollection<IMessageSubscription> _subscriptions;
    private readonly ILogger<MessageConsumerService> _logger;

    private readonly List<IChannel> _channels = [];

    public MessageConsumerService(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IEnumerable<IMessageSubscription> subscriptions,
        ILogger<MessageConsumerService> logger)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        _connection = connection;
        _scopeFactory = scopeFactory;
        _subscriptions = [.. subscriptions];
        _logger = logger;
    }

    /// <summary>
    /// Declares every queue and starts consuming before the host is considered
    /// started.
    /// </summary>
    /// <remarks>
    /// The work is here rather than in <see cref="ExecuteAsync"/> on purpose.
    /// <c>ExecuteAsync</c> runs in the background: the host reports itself
    /// started at the first await inside it, so a publisher that came up at the
    /// same moment could send a message before any queue was bound — and a
    /// topic exchange silently drops what nothing is listening for. Declaring
    /// here means the host is not started until the topology exists.
    ///
    /// A broker that is unreachable at startup therefore stops the worker from
    /// starting at all. That is the right failure for a process whose only job
    /// is consuming: crashing loudly gets it restarted, where starting
    /// successfully and consuming nothing would look healthy for as long as
    /// anyone cared to look.
    /// </remarks>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_subscriptions.Count == 0)
        {
            // Nothing registered yet. Said out loud rather than passed over, so
            // a worker that is quietly doing nothing is visible in the log.
            LogNoSubscriptions(_logger);
        }
        else
        {
            foreach (var subscription in _subscriptions)
            {
                await SubscribeAsync(subscription, cancellationToken);
            }
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        /*
          The consumers run on the client's own threads, so there is nothing to
          do here. This waits for shutdown rather than returning, because
          returning would complete the BackgroundService and, on a host
          configured to stop on completion, take the process down with it.
        */
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task SubscribeAsync(IMessageSubscription subscription, CancellationToken cancellationToken)
    {
        var channel = await _connection.OpenChannelAsync(cancellationToken);
        _channels.Add(channel);

        await channel.QueueDeclareAsync(
            queue: subscription.QueueName,
            // Survives a broker restart, as do the messages in it. A transient
            // queue would silently drop work that was already accepted.
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: subscription.QueueName,
            exchange: _connection.ExchangeName,
            routingKey: subscription.RoutingPattern,
            cancellationToken: cancellationToken);

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: PrefetchCount,
            global: false,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += (_, delivery) =>
            HandleAsync(channel, subscription, delivery, cancellationToken);

        await channel.BasicConsumeAsync(
            queue: subscription.QueueName,
            // Acknowledged by hand once the work has actually been done.
            // Auto-acknowledging would drop a message the moment it is
            // delivered, whether or not it was handled.
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        LogSubscribed(_logger, subscription.QueueName, subscription.RoutingPattern);
    }

    private async Task HandleAsync(
        IChannel channel,
        IMessageSubscription subscription,
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        /*
          The trace the message was queued in, continued here. Everything this
          consumer writes — the notice, the mail, a failure — then belongs to
          the request a person made, in another process, minutes earlier
          (ADR-0042).
        */
        using var activity = StartConsumeActivity(subscription, delivery);

        // A scope per message, so a DbContext is never shared between two
        // deliveries and its change tracker cannot carry one message's entities
        // into the next.
        await using var scope = _scopeFactory.CreateAsyncScope();

        try
        {
            var messageId = ReadMessageId(delivery);

            var dbContext = scope.ServiceProvider.GetRequiredService<IFlowDeskDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            /*
              Claiming and handling commit together, or neither does. Running the
              consumer outside this transaction would let the work land while the
              claim was lost — and the next delivery would do it all again.

              Idempotency is applied here rather than left to each consumer. A
              rule every consumer must remember is a rule one of them will
              eventually forget, and the symptom is a duplicated side effect
              nobody notices until a customer does.
            */
            var handled = await dbContext.ExecuteInTransactionAsync(
                async token =>
                {
                    var claimed = await MessageIdempotency.TryClaimAsync(
                        dbContext, messageId, subscription.ConsumerName, clock.UtcNow, token);

                    if (!claimed)
                    {
                        return false;
                    }

                    await subscription.DispatchAsync(delivery.Body, scope.ServiceProvider, token);
                    await dbContext.SaveChangesAsync(token);

                    return true;
                },
                cancellationToken);

            if (!handled)
            {
                // Acknowledged rather than requeued: it was handled the first
                // time, so the broker should stop offering it.
                LogAlreadyHandled(_logger, subscription.QueueName, messageId);
            }

            CountConsumed(
                subscription,
                handled ? FlowDeskTelemetry.Outcomes.Handled : FlowDeskTelemetry.Outcomes.Duplicate);

            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (MessageFormatException exception)
        {
            /*
              The message cannot be read, so redelivering it would fail the same
              way for ever and block the queue behind it. Rejected without
              requeueing; a dead-letter queue is Faz 11's job, together with the
              retry policy.
            */
            LogUnreadable(_logger, subscription.QueueName, exception);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            CountConsumed(subscription, FlowDeskTelemetry.Outcomes.Unreadable);

            await channel.BasicNackAsync(
                delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
#pragma warning disable CA1031 // A consumer must not let an exception escape onto
        // the client's delivery thread: an unobserved failure there kills the
        // consumer silently and the queue stops being read at all.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            /*
              Requeued, because the failure may be transient — a database that
              was briefly unreachable, a deadlock, a timeout. Delivery is
              at-least-once by design, so a consumer is expected to see the same
              message again; what it must not do is act on it twice.
            */
            LogHandlerFailed(_logger, subscription.QueueName, exception);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            CountConsumed(subscription, FlowDeskTelemetry.Outcomes.Failed);

            await channel.BasicNackAsync(
                delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }

    private static TraceActivity? StartConsumeActivity(
        IMessageSubscription subscription,
        BasicDeliverEventArgs delivery)
    {
        var name = $"consume {subscription.QueueName}";
        var traceParent = ReadTraceParent(delivery);

        return ActivityContext.TryParse(traceParent, traceState: null, out var parent)
            ? FlowDeskTelemetry.Source.StartActivity(name, ActivityKind.Consumer, parent)
            : FlowDeskTelemetry.Source.StartActivity(name, ActivityKind.Consumer);
    }

    /// <summary>
    /// The W3C trace context the publisher put on the message, if any.
    /// </summary>
    /// <remarks>
    /// A missing or unreadable header is not a failure: a message published
    /// before this existed, or by something else on the same broker, is still
    /// a perfectly good message.
    /// </remarks>
    private static string? ReadTraceParent(BasicDeliverEventArgs delivery)
    {
        if (delivery.BasicProperties.Headers?.TryGetValue("traceparent", out var header) != true)
        {
            return null;
        }

        return header switch
        {
            byte[] utf8 => System.Text.Encoding.UTF8.GetString(utf8),
            string text => text,
            _ => null,
        };
    }

    private static void CountConsumed(IMessageSubscription subscription, string outcome) =>
        FlowDeskTelemetry.MessagesConsumed.Add(
            1,
            new KeyValuePair<string, object?>("consumer", subscription.ConsumerName),
            new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>
    /// Reads the id the publisher stamped on the message.
    /// </summary>
    /// <remarks>
    /// Treated as a format failure when it is missing or unreadable, because
    /// without it there is no way to tell a redelivery from a new message — and
    /// handling it anyway would mean acting an unknown number of times.
    /// </remarks>
    private static Guid ReadMessageId(BasicDeliverEventArgs delivery)
    {
        var raw = delivery.BasicProperties.MessageId;

        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var messageId))
        {
            throw new MessageFormatException(
                "Mesaj kimliği okunamadı; tekrar teslimat ayırt edilemeyeceği için mesaj işlenmedi.");
        }

        return messageId;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Channels are closed before the base class completes, so a message in
        // flight is not cut off mid-handling.
        foreach (var channel in _channels)
        {
            await channel.CloseAsync(cancellationToken);
            await channel.DisposeAsync();
        }

        _channels.Clear();

        await base.StopAsync(cancellationToken);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Mesaj tüketicisi kayıtlı abonelik bulamadı; hiçbir kuyruk dinlenmiyor.")]
    private static partial void LogNoSubscriptions(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Kuyruk dinleniyor: {QueueName} ({RoutingPattern})")]
    private static partial void LogSubscribed(ILogger logger, string queueName, string routingPattern);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "{QueueName} kuyruğundaki mesaj çözümlenemedi; yeniden kuyruğa alınmadı.")]
    private static partial void LogUnreadable(ILogger logger, string queueName, Exception exception);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "{QueueName} kuyruğundaki mesaj işlenemedi; yeniden kuyruğa alındı.")]
    private static partial void LogHandlerFailed(ILogger logger, string queueName, Exception exception);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "{QueueName} kuyruğundaki mesaj zaten işlenmişti, atlandı: {MessageId}")]
    private static partial void LogAlreadyHandled(ILogger logger, string queueName, Guid messageId);
}
