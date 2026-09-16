using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL connection settings bound from the <c>Postgres</c> configuration section.
/// </summary>
public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    /// <summary>
    /// Npgsql connection string. Supplied through environment variables or
    /// user-secrets; never committed to source control.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "PostgreSQL connection string is not configured.")]
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Upper bound for a readiness probe against the database. Kept short so that
    /// a slow or unreachable database fails the probe instead of hanging it.
    /// </summary>
    [Range(1, 30)]
    public int HealthCheckTimeoutSeconds { get; init; } = 3;
}
