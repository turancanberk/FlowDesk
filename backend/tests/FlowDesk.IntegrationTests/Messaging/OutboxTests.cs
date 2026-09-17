using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Messaging;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Time;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FlowDesk.IntegrationTests.Messaging;

/// <summary>
/// The outbox: queueing inside a transaction, and draining it to the broker.
/// </summary>
/// <remarks>
/// The guarantee under test is that a message and the change that caused it
/// land together or not at all. That cannot be checked without a real database,
/// because what makes it true is the transaction — and it cannot be checked
/// without a real broker, because the second half is whether the message
/// actually arrives.
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class OutboxTests
{
    private readonly PostgresContainerFixture _postgres;
    private readonly RabbitMqContainerFixture _broker;

    public OutboxTests(PostgresContainerFixture postgres, RabbitMqContainerFixture broker)
    {
        _postgres = postgres;
        _broker = broker;
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    /// <summary>
    /// Queueing writes a row rather than reaching the broker.
    /// </summary>
    /// <remarks>
    /// The distinction is the whole point of the pattern and is invisible at
    /// the call site.
    /// </remarks>
    [Fact]
    public async Task Publishing_writes_a_row_and_sends_nothing()
    {
        await using var dbContext = _postgres.CreateDbContext();

        var publisher = new OutboxMessagePublisher(dbContext);
        var message = NewMessage("kuyruğa alındı");

        await publisher.PublishAsync(message, Cancellation);

        // Nothing is stored until the caller saves: the row belongs to their
        // transaction, not to the publisher.
        Assert.False(await ExistsAsync(message.MessageId));

        await dbContext.SaveChangesAsync(Cancellation);

        var stored = await ReadAsync(message.MessageId);

        Assert.Equal(nameof(ProbeMessage), stored.Type);
        Assert.Equal(ProbeMessage.Key, stored.RoutingKey);
        Assert.Null(stored.ProcessedAt);
        Assert.Equal(0, stored.AttemptCount);
        Assert.Contains("kuyruğa alındı", stored.Payload, StringComparison.Ordinal);
    }

    /// <summary>
    /// A rolled-back transaction leaves no message behind.
    /// </summary>
    /// <remarks>
    /// This is the failure the outbox exists to remove. Publishing directly,
    /// the broker would already have a message describing something the
    /// database then refused to record.
    /// </remarks>
    [Fact]
    public async Task A_rolled_back_transaction_queues_nothing()
    {
        await using var dbContext = _postgres.CreateDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(Cancellation);

        var publisher = new OutboxMessagePublisher(dbContext);
        var message = NewMessage("geri alındı");

        await publisher.PublishAsync(message, Cancellation);
        await dbContext.SaveChangesAsync(Cancellation);

        await transaction.RollbackAsync(Cancellation);

        Assert.False(await ExistsAsync(message.MessageId));
    }

    /// <summary>
    /// The processor publishes a queued message and marks the row done.
    /// </summary>
    [Fact]
    public async Task The_processor_publishes_pending_messages_and_marks_them_processed()
    {
        await using var harness = await OutboxHarness.StartAsync(_postgres, _broker, Cancellation);

        var queue = await harness.DeclareQueueAsync(ProbeMessage.Key, Cancellation);
        var message = NewMessage("işlendi");

        await harness.QueueAsync(message, Cancellation);

        await harness.RunOnceAsync(Cancellation);

        var stored = await ReadAsync(message.MessageId);

        Assert.NotNull(stored.ProcessedAt);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Null(stored.LastError);
        // Cleared, so the partial index on pending rows stops carrying it.
        Assert.Null(stored.NextAttemptAt);

        var delivered = await harness.ReadOneAsync(queue, Cancellation);

        Assert.Equal(message.MessageId.ToString(), delivered.BasicProperties.MessageId);
        Assert.Equal(nameof(ProbeMessage), delivered.BasicProperties.Type);
    }

    /// <summary>
    /// A processed row is not published again.
    /// </summary>
    [Fact]
    public async Task A_processed_message_is_not_published_twice()
    {
        await using var harness = await OutboxHarness.StartAsync(_postgres, _broker, Cancellation);

        var queue = await harness.DeclareQueueAsync(ProbeMessage.Key, Cancellation);

        await harness.QueueAsync(NewMessage("tek sefer"), Cancellation);

        await harness.RunOnceAsync(Cancellation);
        await harness.RunOnceAsync(Cancellation);

        // One message reached the queue, not two.
        Assert.Equal(1u, await harness.CountAsync(queue, Cancellation));
    }

    /// <summary>
    /// A failed publish records why and schedules a retry instead of losing the
    /// message.
    /// </summary>
    /// <remarks>
    /// The error is kept on the row rather than only in the log, so "why is
    /// this stuck" is answerable from the table an operator is already looking
    /// at.
    /// </remarks>
    [Fact]
    public async Task A_failed_publish_records_the_error_and_backs_off()
    {
        await using var harness = await OutboxHarness.StartAsync(
            _postgres, _broker, Cancellation, brokerFails: true);

        var message = NewMessage("başarısız");

        await harness.QueueAsync(message, Cancellation);

        var before = DateTimeOffset.UtcNow;
        await harness.RunOnceAsync(Cancellation);

        var stored = await ReadAsync(message.MessageId);

        Assert.Null(stored.ProcessedAt);
        Assert.Equal(1, stored.AttemptCount);
        Assert.NotNull(stored.LastError);
        Assert.Contains("broker düştü", stored.LastError, StringComparison.Ordinal);

        // Scheduled forward, so a broker that is down does not become a tight
        // retry loop against it.
        Assert.NotNull(stored.NextAttemptAt);
        Assert.True(stored.NextAttemptAt > before);
    }

    /// <summary>
    /// A message scheduled for later is left alone.
    /// </summary>
    [Fact]
    public async Task A_message_waiting_for_its_retry_is_not_picked_up()
    {
        await using var harness = await OutboxHarness.StartAsync(
            _postgres, _broker, Cancellation, brokerFails: true);

        var message = NewMessage("bekliyor");

        await harness.QueueAsync(message, Cancellation);

        // First pass fails and pushes the next attempt into the future.
        await harness.RunOnceAsync(Cancellation);
        var afterFirst = await ReadAsync(message.MessageId);

        // Second pass finds nothing eligible, so the attempt count is unchanged.
        var claimed = await harness.RunOnceAsync(Cancellation);
        var afterSecond = await ReadAsync(message.MessageId);

        Assert.Equal(0, claimed);
        Assert.Equal(afterFirst.AttemptCount, afterSecond.AttemptCount);
    }

    /// <summary>
    /// One pass takes at most a batch, leaving the rest for the next.
    /// </summary>
    /// <remarks>
    /// The claim holds a row lock for the whole batch, so an unbounded pass
    /// would mean a long transaction and a long window in which a crash
    /// redelivers everything in it.
    /// </remarks>
    [Fact]
    public async Task A_pass_claims_at_most_one_batch()
    {
        await using var harness = await OutboxHarness.StartAsync(
            _postgres, _broker, Cancellation, batchSize: 2);

        await harness.DeclareQueueAsync(ProbeMessage.Key, Cancellation);

        for (var index = 0; index < 5; index++)
        {
            await harness.QueueAsync(NewMessage($"toplu {index}"), Cancellation);
        }

        var first = await harness.RunOnceAsync(Cancellation);
        var second = await harness.RunOnceAsync(Cancellation);

        Assert.Equal(2, first);
        Assert.Equal(2, second);
    }

    private static ProbeMessage NewMessage(string note) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow, note);

    private async Task<bool> ExistsAsync(Guid id)
    {
        await using var dbContext = _postgres.CreateDbContext();

        return await dbContext.OutboxMessages.AsNoTracking()
            .AnyAsync(message => message.Id == id, Cancellation);
    }

    private async Task<OutboxMessage> ReadAsync(Guid id)
    {
        await using var dbContext = _postgres.CreateDbContext();

        var message = await dbContext.OutboxMessages.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == id, Cancellation);

        Assert.NotNull(message);

        return message;
    }

    /// <summary>A broker publisher that always fails, to exercise the retry path.</summary>
    private sealed class FailingBrokerPublisher : IBrokerPublisher
    {
        public Task PublishAsync(
            Guid messageId,
            string messageType,
            string routingKey,
            DateTimeOffset occurredAt,
            ReadOnlyMemory<byte> payload,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("broker düştü");
    }

    /// <summary>
    /// The real processor, wired to the containers, driven one pass at a time.
    /// </summary>
    /// <remarks>
    /// Run pass by pass rather than as a background service: a loop on a timer
    /// would make every assertion a race against it, and the thing worth
    /// checking is what one pass does.
    /// </remarks>
    private sealed class OutboxHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly RabbitMqConnection _connection;
        private readonly string _exchange;

        private OutboxHarness(ServiceProvider services, RabbitMqConnection connection, string exchange)
        {
            _services = services;
            _connection = connection;
            _exchange = exchange;
        }

        public static async Task<OutboxHarness> StartAsync(
            PostgresContainerFixture postgres,
            RabbitMqContainerFixture broker,
            CancellationToken cancellationToken,
            bool brokerFails = false,
            int batchSize = 20)
        {
            /*
              Clears whatever is still pending from earlier tests.

              A per-test exchange isolates the broker side, but the outbox table
              is shared and the drain is global by design — it claims every row
              that is due, whoever wrote it. Without this, one test's assertions
              would count another test's leftovers, which is exactly how these
              tests first failed.
            */
            await using (var cleanup = postgres.CreateDbContext())
            {
                await cleanup.Database.ExecuteSqlRawAsync(
                    "DELETE FROM \"OutboxMessages\"", cancellationToken);
            }

            // A per-test exchange, so one test's messages cannot land on
            // another's queue while they share a broker.
            var exchange = $"flowdesk.tests.{Guid.CreateVersion7():N}";

            var services = new ServiceCollection();

            services.AddLogging(logging => logging.ClearProviders());

            services.AddSingleton(Options.Create(new MessagingOptions
            {
                Host = broker.Host,
                Port = broker.Port,
                VirtualHost = "/",
                UserName = RabbitMqContainerFixture.UserName,
                Password = RabbitMqContainerFixture.Password,
                ExchangeName = exchange,
                PublishTimeoutSeconds = 10,
            }));

            services.AddSingleton(Options.Create(new OutboxOptions
            {
                PollIntervalSeconds = 1,
                BatchSize = batchSize,
                RetryBaseSeconds = 10,
                RetryMaximumSeconds = 900,
            }));

            services.AddSingleton<RabbitMqConnection>();

            if (brokerFails)
            {
                services.AddSingleton<IBrokerPublisher, FailingBrokerPublisher>();
            }
            else
            {
                services.AddSingleton<IBrokerPublisher, RabbitMqBrokerPublisher>();
            }

            // No ITenantContext, which is how the worker runs: the workspace
            // filter stays inert because the outbox spans every workspace.
            services.AddDbContext<FlowDeskDbContext>(options =>
                options.UseNpgsql(postgres.ConnectionString));
            services.AddScoped<IFlowDeskDbContext>(provider =>
                provider.GetRequiredService<FlowDeskDbContext>());
            services.AddSingleton<IClock, SystemClock>();

            var provider = services.BuildServiceProvider();

            return new OutboxHarness(
                provider, provider.GetRequiredService<RabbitMqConnection>(), exchange);
        }

        /// <summary>Queues a message the way a use case would, and commits it.</summary>
        public async Task QueueAsync(ProbeMessage message, CancellationToken cancellationToken)
        {
            await using var scope = _services.CreateAsyncScope();

            var dbContext = scope.ServiceProvider.GetRequiredService<IFlowDeskDbContext>();

            await new OutboxMessagePublisher(dbContext).PublishAsync(message, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Runs one pass and returns how many rows it claimed.</summary>
        public Task<int> RunOnceAsync(CancellationToken cancellationToken)
        {
            var drain = new OutboxDrain(
                _services.GetRequiredService<IServiceScopeFactory>(),
                _services.GetRequiredService<IOptions<OutboxOptions>>(),
                NullLogger<OutboxDrain>.Instance);

            return drain.DrainOnceAsync(cancellationToken);
        }

        public async Task<string> DeclareQueueAsync(
            string routingPattern,
            CancellationToken cancellationToken)
        {
            await using var channel = await _connection.OpenChannelAsync(cancellationToken);

            var queue = $"test-{Guid.CreateVersion7():N}";

            // Durable, because RabbitMQ 4 refuses a transient non-exclusive
            // queue outright; "temporary" is expressed with autoDelete.
            await channel.QueueDeclareAsync(
                queue, durable: true, exclusive: false, autoDelete: true,
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(
                queue, _exchange, routingPattern, cancellationToken: cancellationToken);

            return queue;
        }

        public async Task<RabbitMQ.Client.BasicGetResult> ReadOneAsync(
            string queue,
            CancellationToken cancellationToken)
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

            // Publishing is asynchronous, so a count taken immediately can miss
            // a message that is on its way.
            await Task.Delay(500, cancellationToken);

            return await channel.MessageCountAsync(queue, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
            await _services.DisposeAsync();
        }
    }
}
