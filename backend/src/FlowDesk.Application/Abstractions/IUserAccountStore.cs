using FlowDesk.Application.Common;

namespace FlowDesk.Application.Abstractions;

/// <summary>
/// Credential store for user accounts.
/// </summary>
/// <remarks>
/// ASP.NET Core Identity owns password hashing, normalisation and lockout, and
/// those are infrastructure concerns. This contract is the narrow slice the
/// application actually uses, which keeps <c>UserManager</c> and its EF Core
/// dependencies out of the use cases and makes them testable without a
/// database.
/// </remarks>
public interface IUserAccountStore
{
    Task<Result<UserAccount>> CreateAsync(
        string email,
        string displayName,
        string password,
        CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the credentials and returns the account when they match.
    /// </summary>
    /// <remarks>
    /// Deliberately returns one result for both "no such account" and "wrong
    /// password". Splitting them would let an attacker enumerate registered
    /// e-mail addresses (docs/SECURITY.md).
    /// </remarks>
    Task<UserAccount?> FindByCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken);

    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Looks up an account by address, without checking a password.</summary>
    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Loads several accounts at once.
    /// </summary>
    /// <remarks>
    /// Exists so that listing a team is one query rather than one per member.
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, UserAccount>> FindByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}

/// <summary>A user account as the application layer sees it.</summary>
public sealed record UserAccount(Guid Id, string Email, string DisplayName);
