using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;

namespace FlowDesk.Application.Authentication.LoginUser;

public sealed class LoginUserHandler
{
    private readonly IUserAccountStore _accountStore;
    private readonly SessionIssuer _sessionIssuer;
    private readonly IFlowDeskDbContext _dbContext;

    public LoginUserHandler(
        IUserAccountStore accountStore,
        SessionIssuer sessionIssuer,
        IFlowDeskDbContext dbContext)
    {
        _accountStore = accountStore;
        _sessionIssuer = sessionIssuer;
        _dbContext = dbContext;
    }

    public async Task<Result<AuthenticatedSession>> HandleAsync(
        LoginUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await _accountStore.FindByCredentialsAsync(
            command.Email,
            command.Password,
            cancellationToken);

        if (account is null)
        {
            // One error for both "unknown address" and "wrong password"
            // (docs/SECURITY.md).
            return Result.Failure<AuthenticatedSession>(AuthenticationErrors.InvalidCredentials);
        }

        var session = _sessionIssuer.StartSession(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(session);
    }
}
