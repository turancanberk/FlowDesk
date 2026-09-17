using FlowDesk.Domain.Activity;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class ActivityEventConfiguration : IEntityTypeConfiguration<ActivityEvent>
{
    public void Configure(EntityTypeBuilder<ActivityEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ActivityEvents");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Type).HasConversion<int>().IsRequired();
        builder.Property(entry => entry.SubjectType).HasConversion<int>().IsRequired();

        // jsonb, so someone reading the history can look inside a line's context
        // without shipping the row elsewhere.
        builder.Property(entry => entry.Payload).HasColumnType("jsonb").IsRequired();

        // The feed: a workspace's history, newest first.
        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAt })
            .IsDescending(false, true);

        // One record's own history.
        builder.HasIndex(entry => new { entry.TenantId, entry.SubjectType, entry.SubjectId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entry => entry.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
          No foreign key on ActorUserId, and none on SubjectId either.

          The actor may be deleted and the record of what they did must survive
          them — an audit trail that forgets who did something once they leave
          is not one. The subject is the same, more sharply: the event that
          records a deletion would be deleted by its own cascade.

          The cost is that a name has to be resolved at read time and may come
          back missing. That is the honest answer, and the reader is told so.
        */
    }
}
