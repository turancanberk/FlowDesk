using FlowDesk.Domain.Tenancy;
using FlowDesk.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Memberships");
        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Role)
            .HasConversion<int>()
            .IsRequired();

        // A user belongs to a workspace once. Without this, a second row could
        // give the same person two roles and authorisation would depend on
        // which one a query happened to read first.
        builder.HasIndex(membership => new { membership.UserId, membership.TenantId })
            .IsUnique();

        // Serves member listing and the "is this the last owner?" check.
        builder.HasIndex(membership => new { membership.TenantId, membership.Role });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(membership => membership.TenantId)
            // Deleting a workspace removes the memberships that only described
            // access to it. Nothing is lost that made sense on its own.
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
