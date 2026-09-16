using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class TenantCounterConfiguration : IEntityTypeConfiguration<TenantCounter>
{
    public void Configure(EntityTypeBuilder<TenantCounter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TenantCounters");

        // One row per workspace, so the workspace id is the key.
        builder.HasKey(counter => counter.TenantId);

        builder.Property(counter => counter.NextTicketNumber).IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(counter => counter.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
