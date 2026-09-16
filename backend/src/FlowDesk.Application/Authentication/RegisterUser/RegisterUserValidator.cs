using FluentValidation;

namespace FlowDesk.Application.Authentication.RegisterUser;

/// <summary>
/// Input validation for registration.
/// </summary>
/// <remarks>
/// Shape only. Whether the address is already taken and whether the password
/// meets policy are decided by the account store, which owns that state;
/// duplicating those rules here would let the two drift apart.
/// </remarks>
public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public const int MinimumPasswordLength = 10;
    public const int MaximumDisplayNameLength = 120;
    public const int MaximumEmailLength = 256;

    public RegisterUserValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("E-posta adresi zorunludur.")
            .MaximumLength(MaximumEmailLength)
                .WithMessage($"E-posta adresi en fazla {MaximumEmailLength} karakter olabilir.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");

        RuleFor(command => command.DisplayName)
            .NotEmpty().WithMessage("Ad soyad zorunludur.")
            .MaximumLength(MaximumDisplayNameLength)
                .WithMessage($"Ad soyad en fazla {MaximumDisplayNameLength} karakter olabilir.");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Parola zorunludur.")
            .MinimumLength(MinimumPasswordLength)
                .WithMessage($"Parola en az {MinimumPasswordLength} karakter olmalıdır.");
    }
}
