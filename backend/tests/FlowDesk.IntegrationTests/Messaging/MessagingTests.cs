using System.Text;
using System.Text.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FlowDesk.IntegrationTests.Messaging;

/// <summary>
/// Handing messages to a real broker.
/// </summary>
/// <remarks>
/// The behaviour being checked belongs to RabbitMQ: topic routing, durable
/// queues and message properties. A substitute would agree with whatever the
/// code did, which is why these run against a container.
///
/// These drive <c>IBrokerPublisher</c>, which is what the outbox processor
/// calls. Use cases never reach the broker directly — they write an outbox row
/// (ADR-0033), which <c>OutboxTests</c> covers.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class MessagingTests
{
    private readonly RabbitMqContainerFixture _broker;

    public MessagingTests(RabbitMqContainerFixture broker) => _broker = broker;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_published_message_arrives_on_a_queue_bound_to_its_routing_key()
    {
        await using var host = new MessagingHost(_broker);

        var queue = await host.DeclareQueueAsync(ProbeMessage.Key, Cancellation);

        var sent = new ProbeMessage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, "merhaba");

        await host.PublishAsync(sent, Cancellation);

        var received = await host.ReadOneAsync<ProbeMessage>(queue, Cancellation);

        Assert.Equal(sent.MessageId, received.MessageId);
        Assert.Equal(sent.TenantId, received.TenantId);
        Assert.Equal("merhaba", received.Note);
    }

    /// <summary>
    /// A queue bound to one key does not receive another's messages.
    /// </summary>
    /// <remarks>
    /// The point of a topic exchange. Without this, every consumer would see
    /// every message and each would have to filter for itself — which is where
    /// a forgotten check turns into work done twice.
    /// </remarks>
    [Fact]
    public async Task Routing_keeps_messages_off_queues_that_did_not_ask_for_them()
    {
        await using var host = new MessagingHost(_broker);

        var probeQueue = await host.DeclareQueueAsync(ProbeMessage.Key, Cancellation);
        var otherQueue = await host.DeclareQueueAsync(OtherProbeMessage.Key, Cancellation);

        await host.PublishAsync(
            new ProbeMessage(
                Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, "yalnızca probe"),
            Cancellation);

        var received = await host.ReadOneAsync<ProbeMessage>(probeQueue, Cancellation);
        Assert.Equal("yalnızca probe", received.Note);

        // The other queue is still empty.
        Assert.Equal(0u, await host.CountAsync(otherQueue, Cancellation));
    }

    /// <summary>
    /// A wildcard binding receives every message under its prefix.
    /// </summary>
    [Fact]
    public async Task A_wildcard_binding_receives_the_whole_family()
    {
        await using var host = new MessagingHost(_broker);

        var queue = await host.DeclareQueueAsync("test.*", Cancellation);

        await host.PublishAsync(
            new ProbeMessage(
                Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, "ilk"),
            Cancellation);
        await host.PublishAsync(
            new OtherProbeMessage(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow),
            Cancellation);

        await host.WaitForCountAsync(queue, 2, Cancellation);
    }

    /// <summary>
    /// The message carries the identity a consumer needs to recognise a
    /// redelivery and to know whose data it is acting on.
    /// </summary>
    [Fact]
    public async Task Every_message_carries_its_id_tenant_and_type()
    {
        await using var host = new MessagingHost(_broker);

        var queue = await host.DeclareQueueAsync(ProbeMessage.Key, Cancellation);

        var sent = new ProbeMessage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, "kimlik");

        await host.PublishAsync(sent, Cancellation);

        var delivery = await host.ReadRawAsync(queue, Cancellation);

        Assert.Equal(sent.MessageId.ToString(), delivery.BasicProperties.MessageId);
        Assert.Equal(nameof(ProbeMessage), delivery.BasicProperties.Type);
        Assert.Equal("application/json", delivery.BasicProperties.ContentType);
        // Persistent, so a broker restart does not lose accepted messages.
        Assert.True(delivery.BasicProperties.Persistent);

        // The tenant travels in the body, because a consumer runs outside a
        // request and has no workspace context of its own.
        var body = Encoding.UTF8.GetString(delivery.Body.Span);
        Assert.Contains(sent.TenantId.ToString(), body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A body that cannot be read is reported as a format failure, not as a
    /// handler failure.
    /// </summary>
    /// <remarks>
    /// The distinction decides what the host does next: a handler failure is
    /// requeued because it may be transient, an unreadable body is not because
    /// it would fail the same way for ever and block the queue behind it.
    /// </remarks>
    [Fact]
    public async Task An_unreadable_body_is_reported_as_a_format_failure()
    {
        var subscription = new MessageSubscription<ProbeMessage>("test-queue", ProbeMessage.Key);
        var services = new ServiceCollection().BuildServiceProvider();

        await Assert.ThrowsAsync<MessageFormatException>(() =>
            subscription.DispatchAsync(
                Encoding.UTF8.GetBytes("{ bu JSON değil"), services, Cancellation));

        await Assert.ThrowsAsync<MessageFormatException>(() =>
            subscription.DispatchAsync(
                Encoding.UTF8.GetBytes("null"), services, Cancellation));
    }

    /// <summary>
    /// What a use case injects writes to the outbox; it does not reach the
    /// broker.
    /// </summary>
    /// <remarks>
    /// The distinction is the whole of ADR-0033, and it is invisible at the
    /// call site — both spellings compile. Asserting the registration is what
    /// would catch a change that quietly restored direct publishing and, with
    /// it, the gap between a commit and a message.
    /// </remarks>
    [Fact]
    public void The_application_contract_resolves_to_the_outbox_writer()
    {
        using var factory = new FlowDeskApiFactory(
            "Host=localhost;Port=1;Database=x;Username=x;Password=x",
            _broker.Host,
            _broker.Port);

        using var scope = factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var broker = factory.Services.GetRequiredService<IBrokerPublisher>();

        Assert.IsType<OutboxMessagePublisher>(publisher);
        Assert.IsType<RabbitMqBrokerPublisher>(broker);
    }

    /// <summary>
    /// Enums travel as names, so a message sitting in a queue across a
    /// deployment cannot change meaning (ADR-0023).
    /// </summary>
    [Fact]
    public void Messages_are_serialised_with_named_enums()
    {
        var json = JsonSerializer.Serialize(
            new { Status = FlowDesk.Domain.Tickets.TicketStatus.InProgress },
            FlowDeskMessageJson.Options);

        Assert.Contains("\"InProgress\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"status\":1", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// A small harness that wires the real publisher to the container and lets
    /// a test declare queues and read what arrives.
    /// </summary>
    private sealed class MessagingHost : IAsyncDisposable
    {
        private readonly RabbitMqConnection _connection;
        private readonly string _exchange;

        public MessagingHost(RabbitMqContainerFixture broker)
        {
            // A per-test exchange, so one test's bindings cannot catch
            // another's messages while they share a broker.
            _exchange = $"flowdesk.tests.{Guid.CreateVersion7():N}";

            var options = Options.Create(new MessagingOptions
            {
                Host = broker.Host,
                Port = broker.Port,
                VirtualHost = "/",
                UserName = RabbitMqContainerFixture.UserName,
                Password = RabbitMqContainerFixture.Password,
                ExchangeName = _exchange,
                PublishTimeoutSeconds = 10,
            });

            _connection = new RabbitMqConnection(options, NullLogger<RabbitMqConnection>.Instance);

            Publisher = new RabbitMqBrokerPublisher(
                _connection, options, NullLogger<RabbitMqBrokerPublisher>.Instance);
        }

        public RabbitMqBrokerPublisher Publisher { get; }

        /// <summary>
        /// Serialises a message and hands it to the broker, the way the outbox
        /// processor does.
        /// </summary>
        public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
            where TMessage : IntegrationMessage =>
            Publisher.PublishAsync(
                message.MessageId,
                message.GetType().Name,
                message.RoutingKey,
                message.OccurredAt,
                JsonSerializer.SerializeToUtf8Bytes(
                    message, message.GetType(), FlowDeskMessageJson.Options),
                traceParent: null,
                cancellationToken);

        /// <summary>Declares a queue bound to the given pattern and returns its name.</summary>
        public async Task<string> DeclareQueueAsync(string routingPattern, CancellationToken cancellationToken)
        {
            await using var channel = await _connection.OpenChannelAsync(cancellationToken);

            var queue = $"test-{Guid.CreateVersion7():N}";

            /*
              Durable, even though the queue lives only for one test. RabbitMQ 4
              refuses a transient non-exclusive queue outright — the feature is
              deprecated — so "temporary" is expressed with autoDelete rather
              than by making the queue non-durable.
            */
            await channel.QueueDeclareAsync(
                queue, durable: true, exclusive: false, autoDelete: true,
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(
                queue, _exchange, routingPattern, cancellationToken: cancellationToken);

            return queue;
        }

        public async Task<TMessage> ReadOneAsync<TMessage>(
            string queue,
            CancellationToken cancellationToken)
            where TMessage : IntegrationMessage
        {
            var delivery = await ReadRawAsync(queue, cancellationToken);

            var message = JsonSerializer.Deserialize<TMessage>(
                delivery.Body.Span, FlowDeskMessageJson.Options);

            Assert.NotNull(message);

            return message;
        }

        /// <summary>
        /// Polls until a message is there.
        /// </summary>
        /// <remarks>
        /// Publishing is asynchronous, so the message is not on the queue the
        /// instant the call returns. Polling with a deadline rather than a fixed
        /// sleep keeps the test fast when the broker is quick and still gives it
        /// room on a loaded machine.
        /// </remarks>
        public async Task<BasicGetResult> ReadRawAsync(string queue, CancellationToken cancellationToken)
        {
            await using var channel = await _connection.OpenChannelAsync(cancellationToken);

            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

            while (DateTimeOffset.UtcNow < deadline)
            {
                var result = await channel.BasicGetAsync(queue, autoAck: true, cancellationToken);

                if (result is not null)
                {
                    return result;
                }

                await Task.Delay(50, cancellationToken);
            }

            throw new TimeoutException($"'{queue}' kuyruğuna 10 saniyede mesaj gelmedi.");
        }

        public async Task<uint> CountAsync(string queue, CancellationToken cancellationToken)
        {
            await using var channel = await _connection.OpenChannelAsync(cancellationToken);

            return await channel.MessageCountAsync(queue, cancellationToken);
        }

        public async Task WaitForCountAsync(string queue, uint expected, CancellationToken cancellationToken)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await CountAsync(queue, cancellationToken) >= expected)
                {
                    return;
                }

                await Task.Delay(50, cancellationToken);
            }

            var actual = await CountAsync(queue, cancellationToken);
            Assert.Fail($"'{queue}' kuyruğunda {expected} mesaj beklendi, {actual} bulundu.");
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
