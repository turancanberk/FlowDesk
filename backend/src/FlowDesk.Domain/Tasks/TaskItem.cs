using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Domain.Tasks;

/// <summary>
/// A piece of internal work, optionally tied to a customer.
/// </summary>
/// <remarks>
/// Named <c>TaskItem</c> rather than <c>Task</c>, which would collide with
/// <see cref="System.Threading.Tasks.Task"/> and force a qualified name in
/// every async signature in the codebase. Shown to people as "Görev".
///
/// Unlike a ticket, a task has no state machine: any of the three statuses may
/// follow any other. Moving something back from Done to Todo is a correction,
/// not a workflow violation, and refusing it would only teach people to delete
/// the task and make a new one.
/// </remarks>
public sealed class TaskItem : ITenantOwned
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 4000;

    private TaskItem()
    {
        Title = string.Empty;
    }

    private TaskItem(
        Guid id,
        Guid tenantId,
        string title,
        string? description,
        Guid? customerId,
        Guid? assignedUserId,
        DateTimeOffset? dueAt,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = id;
        TenantId = tenantId;
        Title = title;
        Description = description;
        Status = TaskItemStatus.Todo;
        CustomerId = customerId;
        AssignedUserId = assignedUserId;
        DueAt = dueAt;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public TaskItemStatus Status { get; private set; }

    /// <summary>
    /// When the work is due, if a date was set.
    /// </summary>
    /// <remarks>
    /// Optional on purpose. Forcing a date on every task makes people invent
    /// one, and an invented deadline is worse than none: it makes the overdue
    /// list untrustworthy, and an untrustworthy list stops being read.
    /// </remarks>
    public DateTimeOffset? DueAt { get; private set; }

    public Guid? AssignedUserId { get; private set; }

    /// <summary>The customer this work is for, when it is about one at all.</summary>
    public Guid? CustomerId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Set when the task first reaches Done, cleared when it is reopened.</summary>
    /// <remarks>
    /// Cleared, unlike a ticket's <c>ResolvedAt</c>. A reopened ticket keeps its
    /// first resolution because how long the original attempt took is a number
    /// the team reports on. A task that is not done simply has no completion
    /// date, and leaving a stale one would make it look finished in every list
    /// that reads this field.
    /// </remarks>
    public DateTimeOffset? CompletedAt { get; private set; }

    public bool IsOverdue(DateTimeOffset now) =>
        Status is not TaskItemStatus.Done && DueAt is { } dueAt && dueAt < now;

    public static TaskItem Create(
        Guid tenantId,
        string title,
        string? description,
        Guid? customerId,
        Guid? assignedUserId,
        DateTimeOffset? dueAt,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A task must belong to a workspace.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new DomainRuleViolationException("A task must record who created it.");
        }

        return new TaskItem(
            Guid.CreateVersion7(),
            tenantId,
            RequireTitle(title),
            NormaliseDescription(description),
            RequireOptionalId(customerId, "customer"),
            RequireOptionalId(assignedUserId, "assignee"),
            dueAt,
            createdByUserId,
            now);
    }

    public void ChangeStatus(TaskItemStatus status, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status))
        {
            throw new DomainRuleViolationException($"'{status}' is not a valid task status.");
        }

        if (Status == status)
        {
            // Pressing the same button twice is not an error.
            return;
        }

        Status = status;
        CompletedAt = status is TaskItemStatus.Done ? now : null;
        UpdatedAt = now;
    }

    public void UpdateDetails(
        string title,
        string? description,
        DateTimeOffset? dueAt,
        DateTimeOffset now)
    {
        Title = RequireTitle(title);
        Description = NormaliseDescription(description);
        DueAt = dueAt;
        UpdatedAt = now;
    }

    public void Assign(Guid? assignedUserId, DateTimeOffset now)
    {
        AssignedUserId = RequireOptionalId(assignedUserId, "assignee");
        UpdatedAt = now;
    }

    /// <summary>
    /// Ties the task to a customer, or to none.
    /// </summary>
    /// <remarks>
    /// The caller must have checked that the customer belongs to the same
    /// workspace. The entity cannot see its siblings, so it guards what it can
    /// (docs/SECURITY.md).
    /// </remarks>
    public void LinkToCustomer(Guid? customerId, DateTimeOffset now)
    {
        CustomerId = RequireOptionalId(customerId, "customer");
        UpdatedAt = now;
    }

    private static string RequireTitle(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRuleViolationException("A task must have a title.");
        }

        if (trimmed.Length > MaximumTitleLength)
        {
            throw new DomainRuleViolationException(
                $"A task title cannot exceed {MaximumTitleLength} characters.");
        }

        return trimmed;
    }

    private static string? NormaliseDescription(string? value)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            // Stored as null rather than "". A blank field means "not provided",
            // and an empty string sorts and filters differently from a missing
            // value.
            return null;
        }

        if (trimmed.Length > MaximumDescriptionLength)
        {
            throw new DomainRuleViolationException(
                $"A task description cannot exceed {MaximumDescriptionLength} characters.");
        }

        return trimmed;
    }

    /// <summary>
    /// Rejects an empty <see cref="Guid"/> where null is the way to say "none".
    /// </summary>
    /// <remarks>
    /// Accepting <c>Guid.Empty</c> would store a reference to a record that
    /// cannot exist, which reads as "assigned to nobody in particular" rather
    /// than "unassigned" in every query that checks for null.
    /// </remarks>
    private static Guid? RequireOptionalId(Guid? value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                $"Use null for no {fieldName} rather than an empty id.");
        }

        return value;
    }
}
