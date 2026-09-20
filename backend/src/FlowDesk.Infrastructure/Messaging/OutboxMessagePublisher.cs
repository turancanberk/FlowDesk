using System.Text.Json;
using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Messaging;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Queues a message by writing it to the outbox.
/// </summary>
/// <remarks>
/// Adds a row to the caller's unit of work and does nothing else. It
/// deliberately does <b>not</b> save: saving here would commit the message
/// independently of the change that caused it, which is the exact failure the
/// outbox exists to prevent (ADR-0033).
///
/// Scoped, because it writes through the request's own <c>DbContext</c>.
/// </remarks>
public sealed class OutboxMessagePublisher : IMessagePublisher
{
    private readonly IFlowDeskDbContext _dbContext;

    public OutboxMessagePublisher(IFlowDeskDbContext dbContext) => _dbContext = dbContext;

    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : IntegrationMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        /*
          Serialised against the runtime type, not TMessage. A caller holding
          the base type would otherwise write a payload missing every property
          the concrete message added — and the loss would only surface in the
          consumer, long after the row looked fine.
        */
        var payload = JsonSerializer.Serialize(
            message, message.GetType(), FlowDeskMessageJson.Options);

        /*
          The trace of the request being served, stored with the row. The
          worker picks it up minutes later in another process, and without it
          the notice and the e-mail it produces would look like work nobody
          asked for (ADR-0042).
        */
        _dbContext.OutboxMessages.Add(OutboxMessage.Create(
            message.MessageId,
            message.GetType().Name,
            message.RoutingKey,
            payload,
            message.OccurredAt,
            System.Diagnostics.Activity.Current?.Id));

        return Task.CompletedTask;
    }
}
