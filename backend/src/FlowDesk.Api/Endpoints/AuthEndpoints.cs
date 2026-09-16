using System.Security.Claims;
using FlowDesk.Api.Authentication;
using FlowDesk.Api.Common;
using FlowDesk.Api.Contracts;
using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Authentication;
using FlowDesk.Application.Authentication.GetCurrentUser;
using FlowDesk.Application.Authentication.LoginUser;
using FlowDesk.Application.Authentication.LogoutSession;
using FlowDesk.Application.Authentication.RefreshSession;
using FlowDesk.Application.Authentication.RegisterUser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FlowDesk.Api.Endpoints;

/// <summary>
/// Registration, sign-in, refresh, sign-out and "who am I".
/// </summary>
/// <remarks>
/// Endpoints coordinate HTTP only: read the request, call one use case, turn
/// the result into a response and manage the cookie. No business rule is
/// decided here.
/// </remarks>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterUserCommand>>()
            .RequireRateLimiting(RateLimitingPolicies.Registration)
            .AllowAnonymous()
            .WithName("Register");

        group.MapPost("/login", LoginAsync)
            .AddEndpointFilter<ValidationFilter<LoginUserCommand>>()
            .RequireRateLimiting(RateLimitingPolicies.Login)
            .AllowAnonymous()
            .WithName("Login");

        group.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting(RateLimitingPolicies.Refresh)
            .AllowAnonymous()
            .WithName("Refresh");

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .WithName("Logout");

        endpoints.MapGet("/api/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("CurrentUser")
            .WithTags("Authentication");

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterUserCommand command,
        RegisterUserHandler handler,
        HttpContext httpContext,
        IOptions<CookieOptionsSettings> cookieSettings,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : CompleteSession(httpContext, result.Value, cookieSettings.Value);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginUserCommand command,
        LoginUserHandler handler,
        HttpContext httpContext,
        IOptions<CookieOptionsSettings> cookieSettings,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : CompleteSession(httpContext, result.Value, cookieSettings.Value);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshSessionHandler handler,
        HttpContext httpContext,
        IOptions<CookieOptionsSettings> cookieSettings,
        CancellationToken cancellationToken)
    {
        // Read only from the cookie. Accepting the token from a body or query
        // string would put a long-lived credential somewhere it can be logged
        // or shared.
        var refreshToken = RefreshTokenCookie.Read(httpContext.Request);

        var result = await handler.HandleAsync(
            new RefreshSessionCommand(refreshToken ?? string.Empty),
            cancellationToken);

        if (result.IsFailure)
        {
            // The session is gone, so the cookie is stale. Clearing it stops
            // the client from retrying with a value that can never work.
            RefreshTokenCookie.Clear(httpContext.Response, cookieSettings.Value.SecurePolicy);
            return result.Error.ToProblem(httpContext);
        }

        return CompleteSession(httpContext, result.Value, cookieSettings.Value);
    }

    private static async Task<IResult> LogoutAsync(
        LogoutSessionHandler handler,
        HttpContext httpContext,
        IOptions<CookieOptionsSettings> cookieSettings,
        CancellationToken cancellationToken)
    {
        var refreshToken = RefreshTokenCookie.Read(httpContext.Request);

        await handler.HandleAsync(new LogoutSessionCommand(refreshToken), cancellationToken);

        RefreshTokenCookie.Clear(httpContext.Response, cookieSettings.Value.SecurePolicy);

        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        GetCurrentUserHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(httpContext.User, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(userId, cancellationToken);

        return result.IsFailure
            ? result.Error.ToProblem(httpContext)
            : Results.Ok(ToResponse(result.Value));
    }

    /// <summary>
    /// Writes the refresh cookie and returns the access token.
    /// </summary>
    private static IResult CompleteSession(
        HttpContext httpContext,
        AuthenticatedSession session,
        CookieOptionsSettings cookieSettings)
    {
        RefreshTokenCookie.Write(
            httpContext.Response,
            session.RefreshTokenValue,
            session.RefreshTokenExpiresAt,
            cookieSettings.SecurePolicy);

        return Results.Ok(new AuthenticationResponse(
            session.AccessToken.Value,
            session.AccessToken.ExpiresAt,
            ToResponse(session.User)));
    }

    private static CurrentUserResponse ToResponse(UserAccount account) =>
        new(account.Id, account.Email, account.DisplayName);

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var subject =
            principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(subject, out userId);
    }
}
