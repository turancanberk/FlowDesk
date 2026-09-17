using FlowDesk.Application.Abstractions;

namespace FlowDesk.IntegrationTests.Messaging;

/// <summary>
/// A message that exists only for these tests.
/// </summary>
/// <remarks>
/// Deliberately not one of the product's own messages. The first real one
/// arrives with notifications (Faz 12), and writing a message here just to have
/// something to publish would put a type in the domain that nothing sends.
/// </remarks>
public sealed record ProbeMessage(
    Guid MessageId,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    string Note) : IntegrationMessage(MessageId, TenantId, OccurredAt)
{
    public const string Key = "test.probe";

    public override string RoutingKey => Key;
}

/// <summary>A second type, to prove routing actually separates them.</summary>
public sealed record OtherProbeMessage(
    Guid MessageId,
    Guid TenantId,
    DateTimeOffset OccurredAt) : IntegrationMessage(MessageId, TenantId, OccurredAt)
{
    public const string Key = "test.other";

    public override string RoutingKey => Key;
}
