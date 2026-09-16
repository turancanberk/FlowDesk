using FlowDesk.Domain.Tenancy;
using FlowDesk.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TicketComments");
        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Body)
            .HasMaxLength(TicketComment.MaximumBodyLength)
            .IsRequired();

        // Comments are always read as one ticket's thread, oldest first.
        builder.HasIndex(comment => new { comment.TicketId, comment.CreatedAt });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(comment => comment.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unlike the customer relationship, a comment has no meaning without its
        // ticket, so deleting the ticket takes the thread with it.
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(comment => comment.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
