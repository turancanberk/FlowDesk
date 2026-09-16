using FlowDesk.Api.Authentication;
using FlowDesk.Application.Authentication.GetCurrentUser;
using FlowDesk.Application.Authentication.LoginUser;
using FlowDesk.Application.Authentication.LogoutSession;
using FlowDesk.Application.Authentication.RefreshSession;
using FlowDesk.Application.Authentication.RegisterUser;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using FlowDesk.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace FlowDesk.Api.Common;

/// <summary>Wires the application layer and HTTP-facing services.</summary>
public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicyName = "FlowDeskWeb";

    public static IServiceCollection AddFlowDeskApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Use cases are injected directly. No mediator sits in between: it
        // would hide the call chain without removing any coupling (ADR-0004).
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<RefreshSessionHandler>();
        services.AddScoped<LogoutSessionHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>(ServiceLifetime.Singleton);

        return services;
    }

    public static IServiceCollection AddFlowDeskApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<CookieOptionsSettings>(configuration.GetSection(CookieOptionsSettings.SectionName));
        services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));

        services.AddCors(options =>
        {
            var allowedOrigins = configuration
                .GetSection($"{CorsSettings.SectionName}:AllowedOrigins")
                .Get<string[]>() ?? [];

            options.AddPolicy(CorsPolicyName, policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    // Production serves the frontend and the API from one
                    // origin behind Caddy, so there is nothing to allow.
                    return;
                }

                policy
                    .WithOrigins(allowedOrigins)
                    // Never AllowAnyOrigin: it is invalid together with
                    // credentials and gives a false sense of configuration.
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    // The refresh cookie has to ride along on /api/auth calls.
                    .AllowCredentials();
            });
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        /*
          Validation parameters are configured through the options pipeline
          rather than inside AddJwtBearer, because reading AuthenticationOptions
          there would require building a second service provider — which creates
          a parallel container and a second copy of every singleton.

          Deriving the parameters from the same options the issuer uses means
          the signing and validating sides cannot drift apart.
        */
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<FlowDesk.Infrastructure.Authentication.AuthenticationOptions>>(
                (jwtOptions, authOptions) =>
                {
                    jwtOptions.TokenValidationParameters =
                        JwtAccessTokenIssuer.BuildValidationParameters(authOptions.Value);

                    // Tokens arrive in the Authorization header only. Reading
                    // them from a cookie would reintroduce the CSRF surface that
                    // keeping the access token in memory removes (ADR-0006).
                    jwtOptions.MapInboundClaims = false;
                });

        services.AddAuthorization();

        return services;
    }
}
