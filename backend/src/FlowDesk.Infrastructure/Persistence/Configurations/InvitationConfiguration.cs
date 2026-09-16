using FlowDesk.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    /// <summary>SHA-256 rendered as lowercase hex.</summary>
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Invitations");
        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Email)
            .HasMaxLength(Invitation.MaximumEmailLength)
            .IsRequired();

        builder.Property(invitation => invitation.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(invitation => invitation.TokenHash)
            .HasMaxLength(TokenHashLength)
            .IsRequired();

        // Acceptance is a single lookup by hash, and two invitations must never
        // share one.
        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();

        // Serves the pending-invitation list and the duplicate check performed
        // before a new invitation is created.
        builder.HasIndex(invitation => new { invitation.TenantId, invitation.Email });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(invitation => invitation.TenantId)
            // An invitation only means something in the workspace that issued
            // it; nothing is lost that made sense on its own.
            .OnDelete(DeleteBehavior.Cascade);
    }
}
