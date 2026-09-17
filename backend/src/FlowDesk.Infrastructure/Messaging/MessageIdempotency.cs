using FlowDesk.Application.Abstractions;
using FlowDesk.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// Records that a consumer has handled a message, and refuses the second time.
/// </summary>
/// <remarks>
/// Delivery is at-least-once, so a consumer <em>will</em> see the same message
/// twice — after a redelivery, or after the worker died between doing the work
/// and acknowledging it. Acting twice is what this prevents.
///
/// <para>
/// The read is an optimisation, not the guarantee. Two deliveries racing can
/// both find nothing and both run; what stops them both taking effect is the
/// primary key, which fails the second save and rolls its whole transaction
/// back — the consumer's writes with it. So exactly one set of database changes
/// commits, and the loser is requeued and skipped cleanly on redelivery. The
/// read simply means the common case, a redelivery seconds or minutes later,
/// never does the work at all.
/// </para>
///
/// <para>
/// This protects database changes. Effects outside the database — an e-mail
/// already handed to a mail server — cannot be rolled back, so a consumer with
/// external side effects has to record them itself; the transaction cannot do
/// it for them.
/// </para>
/// </remarks>
public static class MessageIdempotency
{
    /// <summary>
    /// Tries to claim the message for this consumer.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the claim is new and the work should run;
    /// <see langword="false"/> when this consumer has already handled it.
    /// </returns>
    /// <remarks>
    /// The claim is only a claim until the caller saves, and it must be
    /// committed in the same transaction as the work it describes. Otherwise
    /// "handled" and "recorded as handled" can come apart, and whichever is
    /// lost, the result is either work done twice or work never done.
    /// </remarks>
    public static async Task<bool> TryClaimAsync(
        IFlowDeskDbContext dbContext,
        Guid messageId,
        string consumer,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var alreadyHandled = await dbContext.ProcessedMessages
            .AsNoTracking()
            .AnyAsync(
                entry => entry.MessageId == messageId && entry.Consumer == consumer,
                cancellationToken);

        if (alreadyHandled)
        {
            return false;
        }

        dbContext.ProcessedMessages.Add(ProcessedMessage.Create(messageId, consumer, now));

        return true;
    }
}
