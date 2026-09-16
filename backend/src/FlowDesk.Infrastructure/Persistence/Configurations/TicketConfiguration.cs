using FlowDesk.Domain.Customers;
using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tickets");
        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.Subject)
            .HasMaxLength(Ticket.MaximumSubjectLength)
            .IsRequired();

        builder.Property(ticket => ticket.Description)
            .HasMaxLength(Ticket.MaximumDescriptionLength)
            .IsRequired();

        builder.Property(ticket => ticket.Status).HasConversion<int>().IsRequired();
        builder.Property(ticket => ticket.Priority).HasConversion<int>().IsRequired();

        /*
          PostgreSQL keeps a row version in the system column xmin. Mapping it as
          the concurrency token gives optimistic concurrency without adding a
          column of our own or having to remember to bump it on every write
          (ADR-0013).
        */
        builder.Property(ticket => ticket.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // The public identifier of a ticket within its workspace. Unique because
        // two tickets sharing a number would make "TLP-1042" ambiguous, and
        // because it is the last defence if the counter lock ever fails.
        builder.HasIndex(ticket => new { ticket.TenantId, ticket.Number }).IsUnique();

        // The default list: a workspace's tickets, most recently updated first.
        builder.HasIndex(ticket => new { ticket.TenantId, ticket.UpdatedAt });

        // Status is the filter teams reach for constantly.
        builder.HasIndex(ticket => new { ticket.TenantId, ticket.Status });

        // "My tickets" and the customer detail page.
        builder.HasIndex(ticket => new { ticket.TenantId, ticket.AssignedUserId });
        builder.HasIndex(ticket => new { ticket.TenantId, ticket.CustomerId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(ticket => ticket.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
          Restrict, not cascade. A customer is archived rather than deleted
          (ADR-0012) precisely so its tickets keep their context; letting a
          delete cascade here would destroy the history archiving exists to
          protect.
        */
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(ticket => ticket.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
