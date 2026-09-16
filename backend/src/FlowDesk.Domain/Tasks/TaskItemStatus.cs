namespace FlowDesk.Domain.Tasks;

/// <summary>Where a piece of internal work stands.</summary>
/// <remarks>
/// Three states, not five. A task is a reminder with an owner and a date; the
/// finer workflow belongs to tickets, which is where customer-facing work
/// lives. Adding Waiting or Blocked here would duplicate that distinction
/// without adding a decision anyone makes.
///
/// Named <c>TaskItemStatus</c> for the same reason the entity is
/// <c>TaskItem</c>: <c>TaskStatus</c> collides with
/// <see cref="System.Threading.Tasks.TaskStatus"/>, which the implicit usings
/// bring into every file. The collision is ambiguous rather than shadowed, so
/// it would break compilation wherever both were in scope. The wire format is
/// unaffected — enums travel as their member names (ADR-0023).
/// </remarks>
public enum TaskItemStatus
{
    Todo = 0,
    InProgress = 1,
    Done = 2,
}
