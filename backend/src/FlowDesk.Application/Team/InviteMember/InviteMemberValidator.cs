using FluentValidation;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.Application.Team.InviteMember;

public sealed class InviteMemberValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("E-posta adresi zorunludur.")
            .MaximumLength(Invitation.MaximumEmailLength)
                .WithMessage($"E-posta adresi en fazla {Invitation.MaximumEmailLength} karakter olabilir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");

        RuleFor(command => command.Role)
            .IsInEnum().WithMessage("Geçerli bir rol seçin.");
    }
}
