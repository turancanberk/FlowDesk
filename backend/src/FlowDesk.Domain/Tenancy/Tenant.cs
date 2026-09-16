using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Tenancy;

/// <summary>
/// A workspace: one organisation's isolated slice of the product.
/// </summary>
public sealed class Tenant
{
    public const int MaximumNameLength = 120;

    // EF Core materialises entities through this constructor.
    private Tenant()
    {
        Name = string.Empty;
        Slug = string.Empty;
    }

    private Tenant(Guid id, string name, string slug, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Slug = slug;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Display name, shown in the workspace switcher.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// URL identifier. Stored as a string because that is what the database and
    /// the router deal in; every write goes through <see cref="WorkspaceSlug"/>.
    /// </summary>
    public string Slug { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Tenant Create(string name, WorkspaceSlug slug, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var trimmedName = (name ?? string.Empty).Trim();

        if (trimmedName.Length == 0)
        {
            throw new DomainRuleViolationException("A workspace must have a name.");
        }

        if (trimmedName.Length > MaximumNameLength)
        {
            throw new DomainRuleViolationException(
                $"A workspace name cannot exceed {MaximumNameLength} characters.");
        }

        return new Tenant(Guid.CreateVersion7(), trimmedName, slug.Value, createdAt);
    }

    public void Rename(string name)
    {
        var trimmedName = (name ?? string.Empty).Trim();

        if (trimmedName.Length == 0)
        {
            throw new DomainRuleViolationException("A workspace must have a name.");
        }

        if (trimmedName.Length > MaximumNameLength)
        {
            throw new DomainRuleViolationException(
                $"A workspace name cannot exceed {MaximumNameLength} characters.");
        }

        Name = trimmedName;
    }
}
