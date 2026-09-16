using System.Security.Claims;
using FlowDesk.Application.Abstractions;

namespace FlowDesk.Api.Tenancy;

/// <summary>
/// Reads the authenticated caller from the current request.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public bool IsAuthenticated => TryGetId(out _);

    public Guid Id =>
        TryGetId(out var id)
            ? id
            : throw new InvalidOperationException(
                "No authenticated user on the current request. Endpoints reading the current user must require authorization.");

    private bool TryGetId(out Guid userId)
    {
        userId = Guid.Empty;

        var principal = _httpContextAccessor.HttpContext?.User;

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        // MapInboundClaims is off, so the JWT "sub" claim keeps its own name
        // rather than being rewritten to the WS-Federation NameIdentifier URI.
        var subject =
            principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(subject, out userId);
    }
}
