using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tenants");
        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Name)
            .HasMaxLength(Tenant.MaximumNameLength)
            .IsRequired();

        builder.Property(tenant => tenant.Slug)
            .HasMaxLength(WorkspaceSlug.MaximumLength)
            .IsRequired();

        // The slug is the public address of a workspace, so it has to identify
        // exactly one. The database enforces it because two simultaneous
        // creates can both pass an application-level check.
        builder.HasIndex(tenant => tenant.Slug).IsUnique();
    }
}
