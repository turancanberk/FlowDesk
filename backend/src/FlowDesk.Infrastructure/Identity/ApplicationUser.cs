using Microsoft.AspNetCore.Identity;

namespace FlowDesk.Infrastructure.Identity;

/// <summary>
/// The persisted user account.
/// </summary>
/// <remarks>
/// Lives in infrastructure rather than the domain, and the domain has no
/// <c>User</c> entity of its own (ADR-0022). Identity already owns password
/// hashing, e-mail normalisation and lockout; a parallel domain user would
/// duplicate that state and leave two places to keep in step. Domain entities
/// reference a user by <see cref="Guid"/> instead.
///
/// <c>IdentityRole</c> is not used for application roles. A user's role is a
/// property of their membership in a workspace, not of the account
/// (ADR-0003).
/// </remarks>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Name shown to teammates in lists, assignments and activity entries.</summary>
    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
