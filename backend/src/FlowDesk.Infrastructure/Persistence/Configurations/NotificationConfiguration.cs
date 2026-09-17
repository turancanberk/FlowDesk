using FlowDesk.Domain.Notifications;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Notifications");
        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Type).HasConversion<int>().IsRequired();

        // jsonb, so someone diagnosing a notification can look inside its
        // context without shipping the row elsewhere.
        builder.Property(notification => notification.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        /*
          The one query this table serves: a person's own notices, unread first,
          newest first. Ordered so the index covers the filter and the sort
          together — TenantId and UserId narrow it, ReadAt splits unread from
          read, CreatedAt orders what remains.
        */
        builder.HasIndex(notification => new
        {
            notification.TenantId,
            notification.UserId,
            notification.ReadAt,
            notification.CreatedAt,
        });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(notification => notification.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // A notice has no meaning without the person it is for, so deleting the
        // account takes it with them.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(notification => notification.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
