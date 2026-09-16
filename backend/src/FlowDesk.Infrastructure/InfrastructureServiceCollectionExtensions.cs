using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Authentication;
using FlowDesk.Infrastructure.Authentication;
using FlowDesk.Infrastructure.HealthChecks;
using FlowDesk.Infrastructure.Identity;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FlowDesk.Infrastructure;

/// <summary>
/// Composition entry point for the infrastructure layer. Hosts (API, Worker)
/// call this instead of wiring individual infrastructure services themselves,
/// which keeps concrete technology choices from leaking into the hosts.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Name of the PostgreSQL readiness health check.</summary>
    public const string PostgresHealthCheckName = "postgres";

    /// <summary>Tag applied to checks that must pass before traffic is accepted.</summary>
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddFlowDeskInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IClock, SystemClock>();

        services.AddFlowDeskPersistence(configuration);
        services.AddFlowDeskIdentity();
        services.AddFlowDeskAuthentication(configuration);
        services.AddFlowDeskInfrastructureHealthChecks();

        return services;
    }

    private static IServiceCollection AddFlowDeskPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup rather than on the first request: a missing
            // connection string is a deployment error, not a runtime condition.
            .ValidateOnStart();

        // A single pooled data source per application. Registering the data
        // source rather than a raw connection string means callers never build
        // their own connections and pooling stays centralised.
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<PostgresOptions>>().Value;

            var builder = new NpgsqlDataSourceBuilder(options.ConnectionString);
            builder.UseLoggerFactory(provider.GetRequiredService<
                Microsoft.Extensions.Logging.ILoggerFactory>());

            return builder.Build();
        });

        services.AddDbContext<FlowDeskDbContext>((provider, optionsBuilder) =>
        {
            optionsBuilder.UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsAssembly(typeof(FlowDeskDbContext).Assembly.FullName));
        });

        // The application layer depends on the contract, never on the concrete
        // context (ADR-0021). Resolving through the registered context keeps a
        // single instance per request, so both views share one change tracker.
        services.AddScoped<IFlowDeskDbContext>(provider =>
            provider.GetRequiredService<FlowDeskDbContext>());

        return services;
    }

    private static IServiceCollection AddFlowDeskIdentity(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                /*
                  Length is the requirement that actually resists guessing.
                  Character-class rules mostly push people towards predictable
                  substitutions, so the minimum is raised instead and the
                  classes are left optional (NIST SP 800-63B).
                */
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<FlowDeskDbContext>();

        // Identity's default token providers are not registered: they exist for
        // e-mail confirmation and password reset, neither of which is in scope
        // yet. Invitation tokens use ISecureTokenGenerator instead (Faz 05).

        services.AddScoped<IUserAccountStore, UserAccountStore>();

        return services;
    }

    private static IServiceCollection AddFlowDeskAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateDataAnnotations()
            // A missing or too-short signing key must stop the process, not
            // produce forgeable tokens.
            .ValidateOnStart();

        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<ISecureTokenGenerator, Sha256SecureTokenGenerator>();

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

            return new AuthenticationSettings(
                TimeSpan.FromMinutes(options.AccessTokenLifetimeMinutes),
                TimeSpan.FromDays(options.RefreshTokenLifetimeDays));
        });

        services.AddScoped<SessionIssuer>();

        return services;
    }

    private static IServiceCollection AddFlowDeskInfrastructureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>(
                PostgresHealthCheckName,
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadinessTag]);

        return services;
    }
}
