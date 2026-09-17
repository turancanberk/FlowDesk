using System.Collections.Concurrent;
using System.Text;
using FlowDesk.Application.Abstractions;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.IntegrationTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FlowDesk.IntegrationTests.Messaging;

/// <summary>
/// The consumer host, running against a real broker.
/// </summary>
/// <remarks>
/// What is being checked here is not the handler — it is everything around it:
/// queue declaration, binding, dispatch into a fresh scope, acknowledgement,
/// and what happens to a message whose handler fails. Those are the parts that
/// decide whether work is lost, repeated, or stuck, and none of them can be
/// observed without a broker that actually redelivers.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class MessageConsumerTests
{
    private readonly RabbitMqContainerFixture _broker;

    public MessageConsumerTests(RabbitMqContainerFixture broker) => _broker = broker;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_registered_consumer_receives_the_message_it_subscribed_to()
    {
        var received = new ConcurrentQueue<ProbeMessage>();

        await using var host = await ConsumerHost.StartAsync(
            _broker, services => services.AddSingleton(new ProbeRecorder(received)), Cancellation);

        var sent = new ProbeMessage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, "tüketildi");

        await host.Publisher.PublishAsync(sent, Cancellation);

        var handled = await WaitForAsync(received, Cancellation);

        Assert.Equal(sent.MessageId, handled.MessageId);
        Assert.Equal(sent.TenantId, handled.TenantId);
        Assert.Equal("tüketildi", handled.Note);
    }

    /// <summary>
    /// A handler is resolved from a fresh scope for every message.
    /// </summary>
    /// <remarks>
    /// Without this a scoped <c>DbContext</c> would be shared across
    /// deliveries, and its change tracker would carry one message's entities
    /// into the next — which shows up much later as a save that writes rows
    /// nobody asked for.
    /// </remarks>
    [Fact]
    public async Task Each_message_is_handled_in_its_own_scope()
    {
        var received = new ConcurrentQueue<ProbeMessage>();
        var scopes = new ConcurrentQueue<Guid>();

        await using var host = await ConsumerHost.StartAsync(
            _broker,
            services =>
            {
                services.AddSingleton(new ProbeRecorder(received));
                services.AddScoped<ScopeMarker>();
                services.AddSingleton(new ScopeRecorder(scopes));
            },
            Cancellation);

        for (var index = 0; index < 3; index++)
        {
            await host.Publisher.PublishAsync(
                new ProbeMessage(
                    Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, $"{index}"),
                Cancellation);
        }

        await WaitUntilAsync(() => received.Count >= 3, Cancellation);

        Assert.Equal(3, scopes.Distinct().Count());
    }

    /// <summary>
    /// A handler that fails gets its message back.
    /// </summary>
    /// <remarks>
    /// The failure may be transient — a database briefly unreachable, a
    /// deadlock — so the message is requeued rather than dropped. Delivery is
    /// at-least-once by design; what a consumer must not do is act twice, which
    /// is what the message id is for.
    /// </remarks>
    [Fact]
    public async Task A_failing_handler_gets_the_message_redelivered()
    {
        var attempts = new ConcurrentQueue<ProbeMessage>();

        await using var host = await ConsumerHost.StartAsync(
            _broker,
            // Fails the first time and succeeds afterwards, so the test proves
            // redelivery rather than an endless loop.
            services => services.AddSingleton(new ProbeRecorder(attempts, failFirst: true)),
            Cancellation);

        await host.Publisher.PublishAsync(
            new ProbeMessage(
                Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, "yeniden"),
            Cancellation);

        await WaitUntilAsync(() => attempts.Count >= 2, Cancellation);

        // The same message, twice.
        var all = attempts.ToArray();
        Assert.Equal(all[0].MessageId, all[1].MessageId);
    }

    /// <summary>
    /// A body that cannot be read is dropped rather than redelivered.
    /// </summary>
    /// <remarks>
    /// It would fail the same way for ever, and with a prefetch of one it would
    /// block every message behind it in the queue. The consumer is never
    /// invoked and the queue drains.
    /// </remarks>
    [Fact]
    public async Task An_unreadable_message_is_not_redelivered_and_does_not_block_the_queue()
    {
        var received = new ConcurrentQueue<ProbeMessage>();

        await using var host = await ConsumerHost.StartAsync(
            _broker, services => services.AddSingleton(new ProbeRecorder(received)), Cancellation);

        await host.PublishRawAsync("{ bu JSON değil", ProbeMessage.Key, Cancellation);

        // A readable message published behind it still gets through.
        var good = new ProbeMessage(
            Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, "arkadan geldi");

        await host.Publisher.PublishAsync(good, Cancellation);

        var handled = await WaitForAsync(received, Cancellation);

        Assert.Equal(good.MessageId, handled.MessageId);
        Assert.Single(received.ToArray().Concat([handled]).Distinct());
        Assert.Equal(0u, await host.CountAsync(Cancellation));
    }

    private static async Task<ProbeMessage> WaitForAsync(
        ConcurrentQueue<ProbeMessage> received,
        CancellationToken cancellationToken)
    {
        await WaitUntilAsync(() => !received.IsEmpty, cancellationToken);

        Assert.True(received.TryDequeue(out var message));

        return message;
    }

    /// <summary>
    /// Polls until the condition holds, with a deadline.
    /// </summary>
    /// <remarks>
    /// Consuming is asynchronous, so nothing has happened the instant a publish
    /// returns. Polling keeps the test fast when the broker is quick and still
    /// gives it room on a loaded machine; a fixed sleep would have to be as long
    /// as the worst case every time.
    /// </remarks>
    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, cancellationToken);
        }

        Assert.Fail("Beklenen tüketim 20 saniyede gerçekleşmedi.");
    }

    /// <summary>Collects what the consumer saw, so a test can assert on it.</summary>
    private sealed class ProbeRecorder
    {
        private int _failuresLeft;

        public ProbeRecorder(ConcurrentQueue<ProbeMessage> received, bool failFirst = false)
        {
            Received = received;
            _failuresLeft = failFirst ? 1 : 0;
        }

        public ConcurrentQueue<ProbeMessage> Received { get; }

        /// <summary>True the first time only, when the recorder was told to fail once.</summary>
        public bool ShouldFail() => Interlocked.Decrement(ref _failuresLeft) >= 0;
    }

    private sealed class ScopeRecorder
    {
        public ScopeRecorder(ConcurrentQueue<Guid> scopeIds) => ScopeIds = scopeIds;

        public ConcurrentQueue<Guid> ScopeIds { get; }
    }

    /// <summary>Scoped, so its id identifies the scope the message was handled in.</summary>
    private sealed class ScopeMarker
    {
        public Guid Id { get; } = Guid.CreateVersion7();
    }

    private sealed class ProbeConsumer : IMessageConsumer<ProbeMessage>
    {
        private readonly ProbeRecorder _recorder;
        private readonly ScopeRecorder? _scopeRecorder;
        private readonly ScopeMarker? _scopeMarker;

        public ProbeConsumer(
            ProbeRecorder recorder,
            ScopeRecorder? scopeRecorder = null,
            ScopeMarker? scopeMarker = null)
        {
            _recorder = recorder;
            _scopeRecorder = scopeRecorder;
            _scopeMarker = scopeMarker;
        }

        public Task HandleAsync(ProbeMessage message, CancellationToken cancellationToken)
        {
            if (_scopeRecorder is not null && _scopeMarker is not null)
            {
                _scopeRecorder.ScopeIds.Enqueue(_scopeMarker.Id);
            }

            _recorder.Received.Enqueue(message);

            if (_recorder.ShouldFail())
            {
                throw new InvalidOperationException("Bilerek başarısız.");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A host running the real consumer service against the container.
    /// </summary>
    private sealed class ConsumerHost : IAsyncDisposable
    {
        private readonly IHost _host;
        private readonly RabbitMqConnection _connection;
        private readonly string _exchange;
        private readonly string _queue;

        private ConsumerHost(IHost host, RabbitMqConnection connection, string exchange, string queue)
        {
            _host = host;
            _connection = connection;
            _exchange = exchange;
            _queue = queue;
        }

        public RabbitMqMessagePublisher Publisher =>
            (RabbitMqMessagePublisher)_host.Services.GetRequiredService<IMessagePublisher>();

        public static async Task<ConsumerHost> StartAsync(
            RabbitMqContainerFixture broker,
            Action<IServiceCollection> configure,
            CancellationToken cancellationToken)
        {
            // Per-test exchange and queue, so one test's consumer cannot eat
            // another's messages while they share a broker.
            var suffix = Guid.CreateVersion7().ToString("N");
            var exchange = $"flowdesk.tests.{suffix}";
            var queue = $"flowdesk.tests.{suffix}.probe";

            var builder = Host.CreateApplicationBuilder();

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:Host"] = broker.Host,
                ["Messaging:Port"] = broker.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Messaging:VirtualHost"] = "/",
                ["Messaging:UserName"] = RabbitMqContainerFixture.UserName,
                ["Messaging:Password"] = RabbitMqContainerFixture.Password,
                ["Messaging:ExchangeName"] = exchange,
                ["Messaging:PublishTimeoutSeconds"] = "10",
            });

            builder.Logging.ClearProviders();

            builder.Services
                .AddOptions<MessagingOptions>()
                .Bind(builder.Configuration.GetSection(MessagingOptions.SectionName))
                .ValidateDataAnnotations();

            builder.Services.AddSingleton<RabbitMqConnection>();
            builder.Services.AddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();

            configure(builder.Services);

            builder.Services.AddMessageConsumer<ProbeMessage, ProbeConsumer>(queue, ProbeMessage.Key);
            builder.Services.AddFlowDeskMessageConsumers();

            var host = builder.Build();
            await host.StartAsync(cancellationToken);

            var connection = host.Services.GetRequiredService<RabbitMqConnection>();

            return new ConsumerHost(host, connection, exchange, queue);
        }

        /// <summary>Publishes a body the consumer cannot parse.</summary>
        public async Task PublishRawAsync(string body, string routingKey, CancellationToken cancellationToken)
        {
            await using var channel = await _connection.OpenChannelAsync(cancellationToken);

            await channel.BasicPublishAsync(
                exchange: _exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: new BasicProperties { Persistent = true },
                body: Encoding.UTF8.GetBytes(body),
                cancellationToken: cancellationToken);
        }

        public async Task<uint> CountAsync(CancellationToken cancellationToken)
        {
            await using var channel = await _connection.OpenChannelAsync(cancellationToken);

            return await channel.MessageCountAsync(_queue, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(CancellationToken.None);
            _host.Dispose();
        }
    }
}
