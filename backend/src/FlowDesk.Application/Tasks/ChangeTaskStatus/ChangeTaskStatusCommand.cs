using FlowDesk.Domain.Tasks;

namespace FlowDesk.Application.Tasks.ChangeTaskStatus;

public sealed record ChangeTaskStatusCommand(TaskItemStatus Status);
