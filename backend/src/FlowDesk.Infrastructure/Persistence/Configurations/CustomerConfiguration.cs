using FlowDesk.Domain.Customers;
using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Customers");
        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Name)
            .HasMaxLength(Customer.MaximumNameLength)
            .IsRequired();

        builder.Property(customer => customer.Email).HasMaxLength(Customer.MaximumEmailLength);
        builder.Property(customer => customer.Phone).HasMaxLength(Customer.MaximumPhoneLength);
        builder.Property(customer => customer.Company).HasMaxLength(Customer.MaximumCompanyLength);
        builder.Property(customer => customer.Notes).HasMaxLength(Customer.MaximumNotesLength);

        builder.Property(customer => customer.Status).HasConversion<int>().IsRequired();

        builder.Property(customer => customer.SearchIndex)
            // Name, company and e-mail folded together, plus separators.
            .HasMaxLength(Customer.MaximumNameLength + Customer.MaximumCompanyLength + Customer.MaximumEmailLength + 2)
            .IsRequired();

        // Backs the search filter. Searching is a prefix-and-contains match, so
        // this does not always avoid a scan; it does keep the scan to one
        // narrow column instead of three wide ones.
        builder.HasIndex(customer => new { customer.TenantId, customer.SearchIndex });

        /*
          The default list: one workspace's live customers, most recently
          updated first. A partial index skips archived rows entirely, so the
          index stays proportional to what is actually browsed rather than to
          everything ever created.
        */
        builder.HasIndex(customer => new { customer.TenantId, customer.UpdatedAt })
            .HasFilter("\"ArchivedAt\" IS NULL");

        // Serves name sorting and the archived-customer view, which does not
        // benefit from the filtered index above.
        builder.HasIndex(customer => new { customer.TenantId, customer.Name });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(customer => customer.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
