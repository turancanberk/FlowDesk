using System.Security.Claims;
using System.Text;
using FlowDesk.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FlowDesk.Infrastructure.Authentication;

/// <summary>
/// Issues signed JSON Web Tokens.
/// </summary>
/// <remarks>
/// The token carries only what the API needs to identify the caller: subject,
/// e-mail and display name. Workspace membership and role are deliberately left
/// out. They change while a token is alive, and a token that asserted them
/// would keep asserting them after the change — turning a removed teammate into
/// an authorised one for the rest of the token's lifetime. Membership is read
/// per request instead (docs/SECURITY.md).
/// </remarks>
public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    /// <summary>Non-standard claim carrying the name to show in the UI.</summary>
    public const string DisplayNameClaimType = "display_name";

    private readonly AuthenticationOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public JwtAccessTokenIssuer(IOptions<AuthenticationOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _clock = clock;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessToken Issue(UserAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        var issuedAt = _clock.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [JwtRegisteredClaimNames.Sub] = account.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = account.Email,
                [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
                [DisplayNameClaimType] = account.DisplayName,
            },
        };

        return new AccessToken(_tokenHandler.CreateToken(descriptor), expiresAt);
    }

    /// <summary>
    /// Validation parameters for the API, derived from the same options so the
    /// issuing and validating sides cannot drift apart.
    /// </summary>
    public static TokenValidationParameters BuildValidationParameters(AuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            // The default five-minute grace would keep a "short-lived" ten
            // minute token alive for fifteen, which defeats the point.
            ClockSkew = TimeSpan.Zero,
            NameClaimType = ClaimTypes.NameIdentifier,
        };
    }
}
