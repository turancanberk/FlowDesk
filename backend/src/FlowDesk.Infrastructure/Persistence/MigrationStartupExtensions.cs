using FlowDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure;

/// <summary>
/// Applies migrations at start-up, for environments that are created and
/// thrown away.
/// </summary>
public static class MigrationStartupExtensions
{
    /// <summary>
    /// Migrates when <c>Postgres:ApplyMigrationsOnStart</c> says so, and does
    /// nothing otherwise.
    /// </summary>
    public static async Task ApplyFlowDeskMigrationsIfConfiguredAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.GetRequiredService<IOptions<PostgresOptions>>().Value;

        if (!options.ApplyMigrationsOnStart)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<FlowDeskDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("FlowDesk.Migrations");

        logger.LogInformation("Migration'lar uygulanıyor (Postgres:ApplyMigrationsOnStart).");

        await dbContext.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Migration'lar uygulandı.");
    }
}
