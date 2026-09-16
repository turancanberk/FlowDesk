using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Common;

namespace FlowDesk.Application.Authentication.GetCurrentUser;

/// <summary>
/// Returns the signed-in account.
/// </summary>
/// <remarks>
/// Reads the account from storage rather than trusting the claims in the access
/// token. A token can outlive a rename or a deletion by up to its lifetime, and
/// showing a stale display name is a small bug while showing a deleted account
/// as present is a real one.
/// </remarks>
public sealed class GetCurrentUserHandler
{
    private readonly IUserAccountStore _accountStore;

    public GetCurrentUserHandler(IUserAccountStore accountStore) => _accountStore = accountStore;

    public async Task<Result<UserAccount>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var account = await _accountStore.FindByIdAsync(userId, cancellationToken);

        return account is null
            ? Result.Failure<UserAccount>(AuthenticationErrors.AccountNotFound)
            : Result.Success(account);
    }
}
