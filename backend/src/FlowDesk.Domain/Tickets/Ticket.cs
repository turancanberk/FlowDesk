using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Domain.Tickets;

/// <summary>
/// A support request raised for a customer.
/// </summary>
/// <remarks>
/// The entity owns its status transitions. A public setter would let any caller
/// move a ticket from Closed straight back to Open, or mark something Resolved
/// that was never worked on — and the rule would then have to be repeated at
/// every call site, where one copy would eventually be forgotten.
/// </remarks>
public sealed class Ticket : ITenantOwned
{
    public const int MaximumSubjectLength = 200;
    public const int MaximumDescriptionLength = 8000;

    /// <summary>
    /// Which statuses each status may move to.
    /// </summary>
    /// <remarks>
    /// Expressed as a table so the whole state machine is readable at once. The
    /// shape is deliberately forgiving in the middle — real support work moves
    /// back and forth between Open, InProgress and Waiting — and strict at the
    /// end: a Closed ticket is finished, and reopening one is a decision that
    /// goes through Open rather than a silent edit.
    /// </remarks>
    private static readonly Dictionary<TicketStatus, TicketStatus[]> AllowedTransitions = new()
    {
        [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.Waiting, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.InProgress] = [TicketStatus.Open, TicketStatus.Waiting, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.Waiting] = [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed],
        // Resolved means "we think this is done". It can be closed, or reopened
        // if the customer disagrees.
        [TicketStatus.Resolved] = [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Closed],
        // Closed is the end. Reopening goes back to Open, which is visible in
        // the history rather than looking like the ticket was never finished.
        [TicketStatus.Closed] = [TicketStatus.Open],
    };

    private Ticket()
    {
        Subject = string.Empty;
        Description = string.Empty;
    }

    private Ticket(
        Guid id,
        Guid tenantId,
        int number,
        Guid customerId,
        string subject,
        string description,
        TicketPriority priority,
        Guid createdByUserId,
        Guid? assignedUserId,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        Number = number;
        CustomerId = customerId;
        Subject = subject;
        Description = description;
        Status = TicketStatus.Open;
        Priority = priority;
        CreatedByUserId = createdByUserId;
        AssignedUserId = assignedUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Sequential within the workspace. Displayed as <c>TLP-1042</c>.</summary>
    public int Number { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Subject { get; private set; }

    public string Description { get; private set; }

    public TicketStatus Status { get; private set; }

    public TicketPriority Priority { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Set the first time the ticket reaches Resolved.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>
    /// PostgreSQL's row version, used as a concurrency token.
    /// </summary>
    /// <remarks>
    /// Tickets are the record several people are most likely to touch at once:
    /// one changes the status while another assigns it. Without this, the
    /// second save would silently overwrite the first (ADR-0013).
    /// </remarks>
    public uint Version { get; private set; }

    public TicketNumber DisplayNumber => new(Number);

    public static Ticket Create(
        Guid tenantId,
        int number,
        Guid customerId,
        string subject,
        string description,
        TicketPriority priority,
        Guid createdByUserId,
        Guid? assignedUserId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A ticket must belong to a workspace.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A ticket must belong to a customer.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A ticket must record who created it.");
        }

        // Validates the range; the value itself is supplied by the caller, which
        // owns the per-workspace counter.
        _ = new TicketNumber(number);

        var trimmedSubject = Require(subject, MaximumSubjectLength, "subject");
        var trimmedDescription = (description ?? string.Empty).Trim();

        if (trimmedDescription.Length > MaximumDescriptionLength)
        {
            throw new DomainRuleViolationException(
                $"A ticket description cannot exceed {MaximumDescriptionLength} characters.");
        }

        if (!Enum.IsDefined(priority))
        {
            throw new DomainRuleViolationException($"'{priority}' is not a valid ticket priority.");
        }

        return new Ticket(
            Guid.CreateVersion7(),
            tenantId,
            number,
            customerId,
            trimmedSubject,
            trimmedDescription,
            priority,
            createdByUserId,
            assignedUserId,
            now);
    }

    /// <summary>Whether the ticket may move from its current status to the given one.</summary>
    public bool CanTransitionTo(TicketStatus status) =>
        Status == status
        || (AllowedTransitions.TryGetValue(Status, out var allowed) && allowed.Contains(status));

    /// <summary>The statuses this ticket may currently move to.</summary>
    public IReadOnlyCollection<TicketStatus> AvailableTransitions =>
        AllowedTransitions.TryGetValue(Status, out var allowed) ? allowed : [];

    /// <summary>
    /// Moves the ticket to a new status.
    /// </summary>
    /// <exception cref="DomainRuleViolationException">The transition is not allowed.</exception>
    public void ChangeStatus(TicketStatus status, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status))
        {
            throw new DomainRuleViolationException($"'{status}' is not a valid ticket status.");
        }

        if (Status == status)
        {
            // Setting the status it already has is a no-op, not an error: two
            // people pressing the same button should not produce a failure.
            return;
        }

        if (!CanTransitionTo(status))
        {
            throw new DomainRuleViolationException(
                $"A ticket cannot move from {Status} to {status}.");
        }

        Status = status;

        // Records when the work was first considered done. Kept from the first
        // resolution so that reopening and resolving again does not erase how
        // long the original took.
        if (status is TicketStatus.Resolved && ResolvedAt is null)
        {
            ResolvedAt = now;
        }

        UpdatedAt = now;
    }

    public void Assign(Guid? assignedUserId, DateTimeOffset now)
    {
        if (assignedUserId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                "Use null to leave a ticket unassigned rather than an empty id.");
        }

        AssignedUserId = assignedUserId;
        UpdatedAt = now;
    }

    public void ChangePriority(TicketPriority priority, DateTimeOffset now)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new DomainRuleViolationException($"'{priority}' is not a valid ticket priority.");
        }

        Priority = priority;
        UpdatedAt = now;
    }

    public void UpdateDetails(string subject, string description, DateTimeOffset now)
    {
        Subject = Require(subject, MaximumSubjectLength, "subject");

        var trimmedDescription = (description ?? string.Empty).Trim();

        if (trimmedDescription.Length > MaximumDescriptionLength)
        {
            throw new DomainRuleViolationException(
                $"A ticket description cannot exceed {MaximumDescriptionLength} characters.");
        }

        Description = trimmedDescription;
        UpdatedAt = now;
    }

    /// <summary>
    /// Moves the ticket to a different customer.
    /// </summary>
    /// <remarks>
    /// The caller must have checked that the customer belongs to the same
    /// workspace. The entity cannot see its siblings, so it guards what it can:
    /// that an id was supplied at all (docs/SECURITY.md).
    /// </remarks>
    public void ReassignCustomer(Guid customerId, DateTimeOffset now)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A ticket must belong to a customer.");
        }

        CustomerId = customerId;
        UpdatedAt = now;
    }

    private static string Require(string? value, int maximumLength, string fieldName)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRuleViolationException($"A ticket must have a {fieldName}.");
        }

        if (trimmed.Length > maximumLength)
        {
            throw new DomainRuleViolationException(
                $"A ticket {fieldName} cannot exceed {maximumLength} characters.");
        }

        return trimmed;
    }
}
