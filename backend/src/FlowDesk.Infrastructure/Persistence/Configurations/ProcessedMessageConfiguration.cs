using FlowDesk.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProcessedMessages");

        /*
          The key is the message and the consumer together, not the message
          alone. Two consumers may legitimately act on the same message — one
          sends an e-mail, another writes a notification — and a key on the
          message alone would let whichever finished first silently suppress the
          other.

          As the primary key it is also the uniqueness constraint, so a second
          delivery collides on insert rather than needing a read first. That
          matters: a read-then-insert has a window in which two deliveries both
          see nothing and both act.
        */
        builder.HasKey(message => new { message.MessageId, message.Consumer });

        builder.Property(message => message.Consumer)
            .HasMaxLength(ProcessedMessage.MaximumConsumerLength)
            .IsRequired();
    }
}
