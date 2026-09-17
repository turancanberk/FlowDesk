using FlowDesk.Application.Abstractions;
using FlowDesk.Application.Activity;
using FlowDesk.Application.Authentication;
using FlowDesk.Application.Tickets.UploadAttachment;
using FlowDesk.Infrastructure.Activity;
using FlowDesk.Infrastructure.Authentication;
using FlowDesk.Infrastructure.Email;
using FlowDesk.Infrastructure.HealthChecks;
using FlowDesk.Infrastructure.Messaging;
using FlowDesk.Infrastructure.Identity;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Storage;
using FlowDesk.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Azure.Storage.Blobs;
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

    public const string RabbitMqHealthCheckName = "rabbitmq";

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
        services.AddFlowDeskMessaging(configuration);
        services.AddFlowDeskEmail(configuration);
        services.AddFlowDeskStorage(configuration);
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

        /*
          Replaces EF's own registration so the context is built with the
          request's ITenantContext. AddDbContext resolves the type through the
          container, and the two-argument constructor is what enables the
          workspace query filter; without it the context falls back to the
          options-only constructor and the filter stays inert.

          The worker has no ITenantContext, and must not: it drains the outbox
          and runs consumers across every workspace, so a filter scoped to "the
          current one" would leave it finding nothing at all. Resolving the
          context optionally is what lets one registration serve both hosts —
          filtered in the API, inert in the worker — instead of each host
          wiring its own persistence.

          An inert filter is safe here precisely because nothing in the worker
          answers a user's request. Anything it writes is scoped by the TenantId
          carried in the message it is acting on (ADR-0032).
        */
        services.AddScoped(provider =>
        {
            var options = provider.GetRequiredService<DbContextOptions<FlowDeskDbContext>>();
            var tenantContext = provider.GetService<ITenantContext>();

            return tenantContext is null
                ? new FlowDeskDbContext(options)
                : new FlowDeskDbContext(options, tenantContext);
        });

        // The application layer depends on the contract, never on the concrete
        // context (ADR-0021). Resolving through the registered context keeps a
        // single instance per request, so both views share one change tracker.
        services.AddScoped<IFlowDeskDbContext>(provider =>
            provider.GetRequiredService<FlowDeskDbContext>());

        /*
          Scoped, because it writes through the request's own DbContext and
          reads the workspace and actor from the request's tenant context. A
          singleton could not know either (ADR-0036).
        */
        services.AddScoped<IActivityRecorder, ActivityRecorder>();

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

    private static IServiceCollection AddFlowDeskMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<MessagingOptions>()
            .Bind(configuration.GetSection(MessagingOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup rather than on the first publish: missing broker
            // credentials are a deployment error, not a runtime condition.
            .ValidateOnStart();

        /*
          One connection for the process, opened lazily on first use. Opening it
          at startup would mean the API refuses to boot when the broker is down,
          and the API serves every read and most writes without it.
        */
        services.AddSingleton<RabbitMqConnection>();

        // Talks to the broker. Only the outbox processor uses it.
        services.AddSingleton<IBrokerPublisher, RabbitMqBrokerPublisher>();

        /*
          What use cases inject. It writes a row rather than sending anything,
          so the message and the change that caused it commit together
          (ADR-0033). Scoped, because it writes through the request's own
          DbContext.
        */
        services.AddScoped<IMessagePublisher, OutboxMessagePublisher>();

        return services;
    }

    private static IServiceCollection AddFlowDeskEmail(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup rather than on the first send: a missing sender
            // address is a deployment error, not a runtime condition.
            .ValidateOnStart();

        /*
          Singleton, because it holds no state between sends — a connection is
          opened and closed per message. Sending is not a readiness dependency:
          the API never sends, and the worker's failure to send is a message
          that retries rather than an instance that should leave rotation.
        */
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        return services;
    }

    private static IServiceCollection AddFlowDeskStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup rather than on the first upload: a missing
            // connection string is a deployment error, not a runtime condition.
            .ValidateOnStart();

        // One client per process. It is thread-safe and pools its connections;
        // building one per request would discard that pool every time.
        services.AddSingleton(provider =>
            new BlobServiceClient(
                provider.GetRequiredService<IOptions<StorageOptions>>().Value.ConnectionString));

        services.AddSingleton<IFileStorage, BlobFileStorage>();

        /*
          The application layer states the limit it needs without reaching for
          infrastructure's configuration type (ADR-0021). The same number also
          bounds the request body, which is set on the endpoint.
        */
        services.AddSingleton<IAttachmentLimits, AttachmentLimits>();

        return services;
    }

    private static IServiceCollection AddFlowDeskInfrastructureHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>(
                PostgresHealthCheckName,
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadinessTag])
            /*
              Degraded, not unhealthy. The API works without the broker; only the
              messages that would have followed are delayed. Failing readiness
              here would take every healthy instance out of rotation at once over
              a dependency none of them needs to answer a request.
            */
            .AddCheck<RabbitMqHealthCheck>(
                RabbitMqHealthCheckName,
                failureStatus: HealthStatus.Degraded,
                tags: [ReadinessTag]);

        return services;
    }
}
