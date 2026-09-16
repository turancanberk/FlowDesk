using System.Collections.ObjectModel;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Authentication;
using FlowDesk.Application.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Infrastructure.Identity;

/// <summary>
/// Implements the account contract on top of ASP.NET Core Identity.
/// </summary>
public sealed class UserAccountStore : IUserAccountStore
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClock _clock;

    public UserAccountStore(UserManager<ApplicationUser> userManager, IClock clock)
    {
        _userManager = userManager;
        _clock = clock;
    }

    public async Task<Result<UserAccount>> CreateAsync(
        string email,
        string displayName,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            CreatedAt = _clock.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            return Result.Success(ToAccount(user));
        }

        // Identity reports a taken address as a distinct code. Surfacing it as
        // a conflict is safe on registration: the caller already knows the
        // address, so this leaks nothing they did not supply.
        var isDuplicate = result.Errors.Any(error =>
            string.Equals(error.Code, nameof(IdentityErrorDescriber.DuplicateEmail), StringComparison.Ordinal) ||
            string.Equals(error.Code, nameof(IdentityErrorDescriber.DuplicateUserName), StringComparison.Ordinal));

        if (isDuplicate)
        {
            return Result.Failure<UserAccount>(AuthenticationErrors.EmailAlreadyRegistered);
        }

        var reason = TranslatePasswordFailure(result.Errors);

        return Result.Failure<UserAccount>(AuthenticationErrors.WeakPassword(reason));
    }

    public async Task<UserAccount?> FindByCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            /*
              Hash a throwaway password against a dummy account so that a
              request for an unknown address costs roughly what a request for a
              known one does. Returning immediately here would make the two
              cases distinguishable by response time, which is exactly the
              enumeration the shared error message is meant to prevent.
            */
            _ = _userManager.PasswordHasher.HashPassword(
                new ApplicationUser { DisplayName = string.Empty },
                password);

            return null;
        }

        var passwordMatches = await _userManager.CheckPasswordAsync(user, password);

        return passwordMatches ? ToAccount(user) : null;
    }

    public async Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByIdAsync(userId.ToString());

        return user is null ? null : ToAccount(user);
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.FindByEmailAsync(email);

        return user is null ? null : ToAccount(user);
    }

    public async Task<IReadOnlyDictionary<Guid, UserAccount>> FindByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Count == 0)
        {
            return ReadOnlyDictionary<Guid, UserAccount>.Empty;
        }

        // One query for the whole team rather than one per member.
        var users = await _userManager.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(user => user.Id, ToAccount);
    }

    private static UserAccount ToAccount(ApplicationUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName);

    /// <summary>
    /// Turns Identity's English password complaints into one Turkish sentence.
    /// </summary>
    private static string TranslatePasswordFailure(IEnumerable<IdentityError> errors)
    {
        var requirements = new List<string>();

        foreach (var error in errors)
        {
            var requirement = error.Code switch
            {
                "PasswordTooShort" => "yeterli uzunlukta olmalı",
                "PasswordRequiresDigit" => "en az bir rakam içermeli",
                "PasswordRequiresLower" => "en az bir küçük harf içermeli",
                "PasswordRequiresUpper" => "en az bir büyük harf içermeli",
                "PasswordRequiresNonAlphanumeric" => "en az bir noktalama işareti içermeli",
                "PasswordRequiresUniqueChars" => "yeterince farklı karakter içermeli",
                _ => null,
            };

            if (requirement is not null)
            {
                requirements.Add(requirement);
            }
        }

        return requirements.Count == 0
            ? "Parola gereksinimleri karşılamıyor."
            : $"Parola {string.Join(", ", requirements)}.";
    }
}
