using FlowDesk.Domain.Authentication;
using FlowDesk.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowDesk.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <summary>SHA-256 rendered as lowercase hex.</summary>
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RefreshTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash)
            .HasMaxLength(TokenHashLength)
            .IsRequired();

        // Every refresh begins with a lookup by hash, and two tokens must never
        // share one.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        // Replay detection and sign-out both revoke a whole family at once.
        builder.HasIndex(token => token.FamilyId);

        // Lets the expired-token cleanup job scan only what it needs.
        builder.HasIndex(token => token.ExpiresAt);

        /*
          The foreign key is declared here rather than as a navigation property
          on the entity: RefreshToken lives in the domain and must not know
          about ApplicationUser, which is an infrastructure type (ADR-0022).

          Cascade delete is correct here. A refresh token has no meaning without
          its account, and leaving orphans behind would leave usable session
          rows pointing at nothing.
        */
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
