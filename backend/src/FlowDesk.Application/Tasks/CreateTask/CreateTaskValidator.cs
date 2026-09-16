using FluentValidation;
using FlowDesk.Domain.Tasks;

namespace FlowDesk.Application.Tasks.CreateTask;

public sealed class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("Görev başlığı zorunludur.")
            .MaximumLength(TaskItem.MaximumTitleLength)
                .WithMessage($"Başlık en fazla {TaskItem.MaximumTitleLength} karakter olabilir.");

        RuleFor(command => command.Description)
            .MaximumLength(TaskItem.MaximumDescriptionLength)
                .WithMessage($"Açıklama en fazla {TaskItem.MaximumDescriptionLength} karakter olabilir.");
    }
}
