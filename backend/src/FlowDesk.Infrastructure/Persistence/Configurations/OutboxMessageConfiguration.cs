using FlowDesk.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);

        // Not generated: the id is chosen by the message itself and travels with
        // it to the broker, so a database default would overwrite the value a
        // consumer needs to recognise a redelivery.
        builder.Property(message => message.Id).ValueGeneratedNever();

        builder.Property(message => message.Type).HasMaxLength(200).IsRequired();
        builder.Property(message => message.RoutingKey).HasMaxLength(200).IsRequired();

        /*
          jsonb rather than text. The payload is never queried on today, but it
          is read by people diagnosing a stuck message, and jsonb gives them
          operators to look inside it without shipping the row elsewhere. The
          cost is a parse on write, which is nothing beside a broker round trip.
        */
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();

        builder.Property(message => message.LastError).HasMaxLength(OutboxMessage.MaximumErrorLength);

        builder.Property(message => message.TraceParent).HasMaxLength(OutboxMessage.TraceParentLength);

        /*
          A partial index: only rows still waiting. The table is append-heavy and
          almost entirely processed history, so an index over everything would
          grow without bound while the query only ever asks for the few rows at
          the front. Processed rows leave the index the moment they are marked.
        */
        builder.HasIndex(message => message.NextAttemptAt)
            .HasDatabaseName("IX_OutboxMessages_Pending")
            .HasFilter("\"ProcessedAt\" IS NULL");
    }
}
