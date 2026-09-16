using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Domain.Customers;

/// <summary>
/// An organisation or person the workspace provides service to.
/// </summary>
/// <remarks>
/// The only entity in the product that is archived rather than deleted
/// (ADR-0012). A customer record is what gives its tickets and tasks their
/// meaning; removing it would leave that history pointing at nothing.
/// </remarks>
public sealed class Customer : ITenantOwned
{
    public const int MaximumNameLength = 160;
    public const int MaximumEmailLength = 256;
    public const int MaximumPhoneLength = 40;
    public const int MaximumCompanyLength = 160;
    public const int MaximumNotesLength = 4000;

    private Customer()
    {
        Name = string.Empty;
        SearchIndex = string.Empty;
    }

    private Customer(Guid id, Guid tenantId, string name, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        SearchIndex = string.Empty;
        Status = CustomerStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public string? Company { get; private set; }

    public CustomerStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Name, company and e-mail folded to ASCII lowercase, kept in one column
    /// so that search is a single comparison.
    /// </summary>
    /// <remarks>
    /// Maintained here rather than computed at query time for two reasons.
    /// Turkish case folding cannot be done correctly in SQL — the dotted and
    /// dotless i make <c>LOWER()</c> give an answer that depends on collation
    /// and never matches an ASCII-typed search (see <see cref="TurkishText"/>).
    /// And a stored column can be indexed, where a per-row expression could
    /// not.
    ///
    /// Never shown to a user; it exists only to be compared against a folded
    /// search term.
    /// </remarks>
    public string SearchIndex { get; private set; }

    /// <summary>Set when the customer is archived. Archived customers are hidden by default.</summary>
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public static Customer Create(
        Guid tenantId,
        CustomerDetails details,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A customer must belong to a workspace.");
        }

        var customer = new Customer(Guid.CreateVersion7(), tenantId, string.Empty, now);
        customer.Apply(details, now);

        return customer;
    }

    /// <summary>Applies a full set of editable details.</summary>
    public void Update(CustomerDetails details, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (IsArchived)
        {
            throw new DomainRuleViolationException(
                "An archived customer cannot be edited. Restore it first.");
        }

        Apply(details, now);
    }

    /// <summary>
    /// Removes the customer from everyday lists without destroying it.
    /// </summary>
    /// <remarks>
    /// Repeatable on purpose: archiving an already-archived customer is what
    /// the caller wanted, and failing would turn a double-click into an error.
    /// </remarks>
    public void Archive(DateTimeOffset now)
    {
        if (ArchivedAt is not null)
        {
            return;
        }

        ArchivedAt = now;
        UpdatedAt = now;
    }

    public void Restore(DateTimeOffset now)
    {
        if (ArchivedAt is null)
        {
            return;
        }

        ArchivedAt = null;
        UpdatedAt = now;
    }

    private void Apply(CustomerDetails details, DateTimeOffset now)
    {
        var name = Normalise(details.Name);

        if (name is null)
        {
            throw new DomainRuleViolationException("A customer must have a name.");
        }

        Guard(name, MaximumNameLength, "name");

        var email = Normalise(details.Email)?.ToLowerInvariant();
        var phone = Normalise(details.Phone);
        var company = Normalise(details.Company);
        var notes = Normalise(details.Notes);

        Guard(email, MaximumEmailLength, "e-mail address");
        Guard(phone, MaximumPhoneLength, "phone number");
        Guard(company, MaximumCompanyLength, "company");
        Guard(notes, MaximumNotesLength, "notes");

        if (!Enum.IsDefined(details.Status))
        {
            throw new DomainRuleViolationException($"'{details.Status}' is not a valid customer status.");
        }

        Name = name;
        Email = email;
        Phone = phone;
        Company = company;
        Notes = notes;
        Status = details.Status;
        SearchIndex = TurkishText.FoldAll(name, company, email);
        UpdatedAt = now;
    }

    /// <summary>
    /// Trims, and turns blank input into <c>null</c>.
    /// </summary>
    /// <remarks>
    /// An empty string and a missing value mean the same thing to a user but
    /// sort and filter differently in the database. Collapsing them here keeps
    /// "no phone number" a single representation.
    /// </remarks>
    private static string? Normalise(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static void Guard(string? value, int maximumLength, string fieldName)
    {
        if (value is not null && value.Length > maximumLength)
        {
            throw new DomainRuleViolationException(
                $"A customer {fieldName} cannot exceed {maximumLength} characters.");
        }
    }
}

/// <summary>
/// The editable fields of a customer, passed as one unit.
/// </summary>
/// <remarks>
/// Grouped so that creating and updating run through the same validation.
/// Separate setters would let one path gain a rule the other quietly lacks.
/// </remarks>
public sealed record CustomerDetails(
    string Name,
    string? Email,
    string? Phone,
    string? Company,
    CustomerStatus Status,
    string? Notes);
