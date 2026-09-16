using FluentValidation;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Tenancy.CreateWorkspace;

public sealed class CreateWorkspaceValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Çalışma alanı adı zorunludur.")
            .MaximumLength(Tenant.MaximumNameLength)
                .WithMessage($"Çalışma alanı adı en fazla {Tenant.MaximumNameLength} karakter olabilir.");

        // Only checked when supplied; an omitted slug is derived from the name.
        When(command => !string.IsNullOrWhiteSpace(command.Slug), () =>
        {
            RuleFor(command => command.Slug!)
                .Must(slug => WorkspaceSlug.TryCreate(slug, out _))
                .WithMessage(
                    "Adres yalnızca küçük harf, rakam ve tek tire içerebilir; "
                    + $"{WorkspaceSlug.MinimumLength}-{WorkspaceSlug.MaximumLength} karakter olmalıdır.");
        });
    }
}
