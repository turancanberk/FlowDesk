using FlowDesk.Domain.Customers;
using FlowDesk.Domain.Tasks;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tasks");
        builder.HasKey(task => task.Id);

        builder.Property(task => task.Title)
            .HasMaxLength(TaskItem.MaximumTitleLength)
            .IsRequired();

        builder.Property(task => task.Description)
            .HasMaxLength(TaskItem.MaximumDescriptionLength);

        builder.Property(task => task.Status).HasConversion<int>().IsRequired();

        /*
          No concurrency token here, unlike Ticket (ADR-0013). A task is a short
          record with one owner, edited by the person it belongs to; the
          simultaneous editing that makes lost updates likely on a ticket does
          not happen here. Adding the token anyway would mean carrying a version
          through every form for a collision that does not occur.
        */

        // The default list: this workspace's tasks, filtered by status.
        builder.HasIndex(task => new { task.TenantId, task.Status });

        // Backs "due soon" and "overdue", which the dashboard asks for by date
        // across the whole workspace.
        builder.HasIndex(task => new { task.TenantId, task.DueAt });

        // "My tasks", and the customer detail page.
        builder.HasIndex(task => new { task.TenantId, task.AssignedUserId });
        builder.HasIndex(task => new { task.TenantId, task.CustomerId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(task => task.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
          SetNull on both optional references, so removing the person or the
          customer leaves the work itself standing. A task that says "call them
          back about the invoice" still has to be done after the account manager
          leaves; it just has nobody on it until someone picks it up.
        */
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(task => task.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(task => task.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
