using FluentValidation;
using FlowDesk.Domain.Tasks;

namespace FlowDesk.Application.Tasks.UpdateTask;

public sealed class UpdateTaskValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskValidator()
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
