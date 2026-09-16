using FluentValidation;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Tenancy.UpdateWorkspace;

public sealed class UpdateWorkspaceValidator : AbstractValidator<UpdateWorkspaceCommand>
{
    public UpdateWorkspaceValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Çalışma alanı adı zorunludur.")
            .MaximumLength(Tenant.MaximumNameLength)
                .WithMessage($"Çalışma alanı adı en fazla {Tenant.MaximumNameLength} karakter olabilir.");
    }
}
