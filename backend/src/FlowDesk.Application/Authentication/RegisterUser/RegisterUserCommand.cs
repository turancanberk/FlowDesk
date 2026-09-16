namespace FlowDesk.Application.Authentication.RegisterUser;

/// <param name="Email">Normalised and used as the sign-in identifier.</param>
/// <param name="DisplayName">Shown to teammates in lists and activity entries.</param>
/// <param name="Password">Never logged and never echoed back.</param>
public sealed record RegisterUserCommand(string Email, string DisplayName, string Password);
