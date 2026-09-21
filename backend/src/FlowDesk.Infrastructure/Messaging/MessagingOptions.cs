using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>Connection and topology settings for the message broker.</summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    [Required(AllowEmptyStrings = false)]
    public string VirtualHost { get; set; } = "/";

    [Required(AllowEmptyStrings = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Never has a default and never appears in a committed file.
    /// </summary>
    /// <remarks>
    /// Supplied through the environment, user-secrets or GitHub Secrets. A
    /// default here would be a credential in source, and a working one at that
    /// (docs/SECURITY.md).
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// The topic exchange every message is published to.
    /// </summary>
    /// <remarks>
    /// One exchange rather than one per message type: consumers bind with a
    /// routing pattern, so adding a message type needs no change to the
    /// topology and no coordination between publisher and consumer.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string ExchangeName { get; set; } = "flowdesk.events";

    /// <summary>
    /// Prefix for every queue this host declares and consumes, and for the
    /// consumer name idempotency is claimed under.
    /// </summary>
    /// <remarks>
    /// Empty in a deployment, where the broker belongs to one environment. Set
    /// when several environments share a broker — a developer's worker and the
    /// browser test suite on one machine, for instance — because queue names
    /// are fixed and whoever consumes first takes the message (ADR-0032).
    /// </remarks>
    public string QueuePrefix { get; set; } = string.Empty;

    /// <summary>
    /// How long a lost connection keeps being retried before giving up.
    /// </summary>
    /// <remarks>
    /// The client retries on its own; this bounds how long a publish waits for
    /// it rather than hanging a request indefinitely.
    /// </remarks>
    [Range(1, 300)]
    public int PublishTimeoutSeconds { get; set; } = 10;
}
