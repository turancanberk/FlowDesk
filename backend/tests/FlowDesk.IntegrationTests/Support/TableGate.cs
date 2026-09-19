using Npgsql;

namespace FlowDesk.IntegrationTests.Support;

/// <summary>
/// Holds a table shut until a given number of requests are queued at it, then
/// lets them all through at once.
/// </summary>
/// <remarks>
/// A race test that merely fires requests together proves little: whether they
/// actually overlap depends on scheduling, and a race that did not happen passes
/// every time. The gate removes the luck. Requests read what they need, reach
/// the gated table and wait there; by the time the gate opens, every one of
/// them has already made its decision on the same stale read — the worst
/// interleaving, every run.
///
/// ACCESS EXCLUSIVE is the one lock mode a plain SELECT waits for, which is
/// what lets the gate stop readers and not only writers.
///
/// Arrivals are counted as sessions waiting on any lock, not only on the gated
/// table. A gate on a table written after a row update holds the first
/// request there with the row locked, and the rest then queue on that row
/// instead — still held, still counted.
/// </remarks>
internal sealed class TableGate : IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private readonly string _table;

    private TableGate(
        string connectionString,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string table)
    {
        _connectionString = connectionString;
        _connection = connection;
        _transaction = transaction;
        _table = table;
    }

    public static async Task<TableGate> CloseAsync(
        string connectionString,
        string table,
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // The name is a constant supplied by the test, never input.
#pragma warning disable CA2100
        await using (var command = new NpgsqlCommand(
            $"LOCK TABLE \"{table}\" IN ACCESS EXCLUSIVE MODE", connection, transaction))
#pragma warning restore CA2100
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new TableGate(connectionString, connection, transaction, table);
    }

    /// <summary>
    /// Waits until <paramref name="count"/> sessions are blocked, then opens
    /// the table.
    /// </summary>
    public async Task OpenWhenQueuedAsync(int count, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        // A second connection: the gate's own is busy holding the lock. Built
        // from the original string, which unlike an opened connection's still
        // carries the password.
        await using var probe = new NpgsqlConnection(_connectionString);
        await probe.OpenAsync(cancellationToken);

        while (await CountWaitingAsync(probe, cancellationToken) < count)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException($"{count} istek \"{_table}\" tablosunda beklemeye ulaşmadı.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }

        await _transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static async Task<long> CountWaitingAsync(NpgsqlConnection probe, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM pg_stat_activity
            WHERE datname = current_database() AND wait_event_type = 'Lock'
            """,
            probe);

        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }
}
