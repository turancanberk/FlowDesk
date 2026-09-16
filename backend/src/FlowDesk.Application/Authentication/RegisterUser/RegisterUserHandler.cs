using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;

namespace FlowDesk.Application.Authentication.RegisterUser;

/// <summary>
/// Creates an account and signs the new user straight in.
/// </summary>
/// <remarks>
/// Registration issues a session immediately rather than redirecting to a
/// sign-in form: the user just proved they know the password by choosing it,
/// and asking for it again adds a step without adding safety.
/// </remarks>
public sealed class RegisterUserHandler
{
    private readonly IUserAccountStore _accountStore;
    private readonly SessionIssuer _sessionIssuer;
    private readonly IFlowDeskDbContext _dbContext;

    public RegisterUserHandler(
        IUserAccountStore accountStore,
        SessionIssuer sessionIssuer,
        IFlowDeskDbContext dbContext)
    {
        _accountStore = accountStore;
        _sessionIssuer = sessionIssuer;
        _dbContext = dbContext;
    }

    public async Task<Result<AuthenticatedSession>> HandleAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var accountResult = await _accountStore.CreateAsync(
            command.Email,
            command.DisplayName,
            command.Password,
            cancellationToken);

        if (accountResult.IsFailure)
        {
            return Result.Failure<AuthenticatedSession>(accountResult.Error);
        }

        var session = _sessionIssuer.StartSession(accountResult.Value);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(session);
    }
}
