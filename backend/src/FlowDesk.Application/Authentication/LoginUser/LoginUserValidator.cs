using FluentValidation;

namespace FlowDesk.Application.Authentication.LoginUser;

/// <summary>
/// Presence checks only.
/// </summary>
/// <remarks>
/// Sign-in deliberately does not validate password shape. Policy changes over
/// time, and rejecting an old-but-correct password at the validation layer
/// would lock out existing users while also revealing the current policy to
/// anyone probing the endpoint.
/// </remarks>
public sealed class LoginUserValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("E-posta adresi zorunludur.");

        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Parola zorunludur.");
    }
}
