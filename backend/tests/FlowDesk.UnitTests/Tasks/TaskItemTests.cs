using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tasks;

namespace FlowDesk.UnitTests.Tasks;

/// <summary>Invariants a task holds.</summary>
public sealed class TaskItemTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_task_starts_as_todo_and_uncompleted()
    {
        var task = NewTask();

        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Null(task.CompletedAt);
        Assert.Equal(Now, task.CreatedAt);
        Assert.Equal(Now, task.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_task_must_have_a_title(string title)
    {
        Assert.Throws<DomainRuleViolationException>(() => NewTask(title: title));
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed_from_the_title()
    {
        var task = NewTask(title: "  Sözleşmeyi gözden geçir  ");

        Assert.Equal("Sözleşmeyi gözden geçir", task.Title);
    }

    [Fact]
    public void A_title_longer_than_the_column_is_rejected()
    {
        var tooLong = new string('a', TaskItem.MaximumTitleLength + 1);

        Assert.Throws<DomainRuleViolationException>(() => NewTask(title: tooLong));
    }

    [Fact]
    public void A_description_longer_than_the_column_is_rejected()
    {
        var tooLong = new string('a', TaskItem.MaximumDescriptionLength + 1);

        Assert.Throws<DomainRuleViolationException>(() => NewTask(description: tooLong));
    }

    /// <summary>
    /// A blank description is stored as null, not as an empty string.
    /// </summary>
    /// <remarks>
    /// The two are not the same to a query: "not provided" and "provided as
    /// nothing" sort and filter differently, and only one of them is true.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_description_becomes_null(string? description)
    {
        var task = NewTask(description: description);

        Assert.Null(task.Description);
    }

    [Fact]
    public void A_task_must_belong_to_a_workspace_and_record_its_author()
    {
        Assert.Throws<DomainRuleViolationException>(() => NewTask(tenantId: Guid.Empty));
        Assert.Throws<DomainRuleViolationException>(() => NewTask(createdByUserId: Guid.Empty));
    }

    /// <summary>
    /// Null is how "nobody" and "no customer" are expressed; an empty id is a
    /// reference to a record that cannot exist.
    /// </summary>
    [Fact]
    public void An_empty_id_is_rejected_where_null_means_none()
    {
        Assert.Throws<DomainRuleViolationException>(() => NewTask(assignedUserId: Guid.Empty));
        Assert.Throws<DomainRuleViolationException>(() => NewTask(customerId: Guid.Empty));

        var task = NewTask();

        Assert.Throws<DomainRuleViolationException>(() => task.Assign(Guid.Empty, Now));
        Assert.Throws<DomainRuleViolationException>(() => task.LinkToCustomer(Guid.Empty, Now));
    }

    [Fact]
    public void A_task_may_have_no_due_date_no_assignee_and_no_customer()
    {
        var task = NewTask();

        Assert.Null(task.DueAt);
        Assert.Null(task.AssignedUserId);
        Assert.Null(task.CustomerId);
    }

    /// <summary>
    /// Any status may follow any other; there is no state machine here.
    /// </summary>
    /// <remarks>
    /// Unlike a ticket. Moving a task back from Done is a correction, and
    /// refusing it would only teach people to delete the task and make a new
    /// one — which loses the history the refusal was meant to protect.
    /// </remarks>
    [Fact]
    public void Every_status_pair_is_allowed()
    {
        foreach (var from in Enum.GetValues<TaskItemStatus>())
        {
            foreach (var to in Enum.GetValues<TaskItemStatus>())
            {
                var task = NewTask();
                task.ChangeStatus(from, Now);
                task.ChangeStatus(to, Now);

                Assert.Equal(to, task.Status);
            }
        }
    }

    [Fact]
    public void An_undefined_status_is_rejected()
    {
        var task = NewTask();

        Assert.Throws<DomainRuleViolationException>(
            () => task.ChangeStatus((TaskItemStatus)42, Now));
    }

    [Fact]
    public void Completing_records_when_it_was_done()
    {
        var task = NewTask();
        var completion = Now.AddHours(4);

        task.ChangeStatus(TaskItemStatus.Done, completion);

        Assert.Equal(completion, task.CompletedAt);
    }

    /// <summary>
    /// Reopening clears the completion date.
    /// </summary>
    /// <remarks>
    /// The opposite of a ticket, which keeps its first resolution. A task that
    /// is not done has no completion date, and a stale one would make it read
    /// as finished in every list that shows the field.
    /// </remarks>
    [Fact]
    public void Reopening_clears_the_completion_date()
    {
        var task = NewTask();

        task.ChangeStatus(TaskItemStatus.Done, Now.AddHours(4));
        task.ChangeStatus(TaskItemStatus.Todo, Now.AddHours(5));

        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Setting_the_status_it_already_has_is_not_an_error()
    {
        var task = NewTask();

        task.ChangeStatus(TaskItemStatus.Todo, Now);

        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Null(task.CompletedAt);
    }

    /// <summary>
    /// Overdue means past the date and not finished. Work completed late is not
    /// overdue — it is done.
    /// </summary>
    [Fact]
    public void Overdue_depends_on_the_date_and_the_status()
    {
        var yesterday = Now.AddDays(-1);
        var tomorrow = Now.AddDays(1);

        Assert.True(NewTask(dueAt: yesterday).IsOverdue(Now));
        Assert.False(NewTask(dueAt: tomorrow).IsOverdue(Now));
        Assert.False(NewTask().IsOverdue(Now));

        var finishedLate = NewTask(dueAt: yesterday);
        finishedLate.ChangeStatus(TaskItemStatus.Done, Now);

        Assert.False(finishedLate.IsOverdue(Now));
    }

    [Fact]
    public void Every_change_moves_the_updated_timestamp()
    {
        var later = Now.AddHours(3);

        var edited = NewTask();
        edited.UpdateDetails("Yeni başlık", "Yeni açıklama", later, later);
        Assert.Equal(later, edited.UpdatedAt);

        var assigned = NewTask();
        assigned.Assign(Guid.CreateVersion7(), later);
        Assert.Equal(later, assigned.UpdatedAt);

        var linked = NewTask();
        linked.LinkToCustomer(Guid.CreateVersion7(), later);
        Assert.Equal(later, linked.UpdatedAt);

        var moved = NewTask();
        moved.ChangeStatus(TaskItemStatus.InProgress, later);
        Assert.Equal(later, moved.UpdatedAt);
    }

    [Fact]
    public void Editing_can_clear_the_due_date()
    {
        var task = NewTask(dueAt: Now.AddDays(3));

        task.UpdateDetails(task.Title, task.Description, dueAt: null, Now);

        Assert.Null(task.DueAt);
    }

    private static TaskItem NewTask(
        Guid? tenantId = null,
        string title = "Sözleşmeyi gözden geçir",
        string? description = "Yenileme öncesi maddeler kontrol edilecek.",
        Guid? customerId = null,
        Guid? assignedUserId = null,
        DateTimeOffset? dueAt = null,
        Guid? createdByUserId = null) =>
        TaskItem.Create(
            tenantId ?? Guid.CreateVersion7(),
            title,
            description,
            customerId,
            assignedUserId,
            dueAt,
            createdByUserId ?? Guid.CreateVersion7(),
            Now);
}
