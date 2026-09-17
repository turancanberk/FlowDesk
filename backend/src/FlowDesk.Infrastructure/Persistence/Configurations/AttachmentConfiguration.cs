using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Attachments");
        builder.HasKey(attachment => attachment.Id);

        builder.Property(attachment => attachment.FileName)
            .HasMaxLength(Attachment.MaximumFileNameLength)
            .IsRequired();

        builder.Property(attachment => attachment.ContentType)
            .HasMaxLength(Attachment.MaximumContentTypeLength)
            .IsRequired();

        builder.Property(attachment => attachment.StorageKey).HasMaxLength(512).IsRequired();

        /*
          Unique. Two rows pointing at the same bytes would mean deleting one
          silently empties the other, and the key is generated from the
          attachment's own id, so a duplicate can only be a bug.
        */
        builder.HasIndex(attachment => attachment.StorageKey).IsUnique();

        // The one query this table serves: a ticket's files, oldest first.
        builder.HasIndex(attachment => new { attachment.TicketId, attachment.CreatedAt });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(attachment => attachment.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
          Cascade, like comments. An attachment has no meaning without its
          ticket. The blob is not removed by the cascade — nothing in the
          database can reach object storage — so deleting a ticket leaves
          orphaned bytes behind; DeleteTicketHandler removes them explicitly.
        */
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(attachment => attachment.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
