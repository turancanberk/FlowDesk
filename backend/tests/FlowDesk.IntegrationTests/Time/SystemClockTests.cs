using FlowDesk.Domain.Tenancy;
using FlowDesk.Infrastructure.Persistence;
using FlowDesk.Infrastructure.Time;
using FlowDesk.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.IntegrationTests.Time;

/// <summary>
/// The time the application believes in is the time it can store (Faz 23).
/// </summary>
/// <remarks>
/// PostgreSQL's <c>timestamp with time zone</c> keeps microseconds; .NET keeps
/// 100-nanosecond ticks. When the clock handed out the finer value, a record
/// read straight after a write carried a different timestamp than the same
/// record read a moment later — the same field with two answers, differing
/// below a microsecond. That is what made
/// <c>Resolving_records_the_moment_the_work_was_done</c> fail on CI while
/// passing locally.
///
/// <para>
/// The values here are chosen, not sampled. A test that reads the machine
/// clock proves nothing on a host whose clock is already microsecond-
/// resolution: every reading is whole and the assertion holds whether the
/// truncation happens or not. The first version of this file did exactly that
/// and passed with the fix removed.
/// </para>
/// </remarks>
[Collection(IntegrationTestSuite.Name)]
public sealed class SystemClockTests
{
    /// <summary>A moment with seven ticks more than a whole microsecond.</summary>
    private static readonly DateTimeOffset FinerThanStorage =
        new DateTimeOffset(2026, 9, 21, 11, 24, 37, TimeSpan.Zero).AddTicks(5_589_817);

    private readonly PostgresContainerFixture _postgres;

    public SystemClockTests(PostgresContainerFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void Mikrosaniyeden_ince_kisim_atiliyor()
    {
        var truncated = SystemClock.ToStorageResolution(FinerThanStorage);

        Assert.Equal(FinerThanStorage.AddTicks(-7), truncated);
        Assert.Equal(0, truncated.Ticks % (TimeSpan.TicksPerMillisecond / 1000));

        // Nothing else moves: this drops precision, it does not round or shift.
        Assert.Equal(FinerThanStorage.Offset, truncated.Offset);
        Assert.True(truncated <= FinerThanStorage);
        Assert.True(FinerThanStorage - truncated < TimeSpan.FromTicks(10));
    }

    [Fact]
    public void Zaten_saklanabilir_bir_deger_degismiyor()
    {
        var already = FinerThanStorage.AddTicks(-7);

        Assert.Equal(already, SystemClock.ToStorageResolution(already));
    }

    [Fact]
    public void Saat_saklanabilir_cozunurlukte_uretir()
    {
        // True on every host, however fine its clock is.
        Assert.Equal(0, new SystemClock().UtcNow.Ticks % (TimeSpan.TicksPerMillisecond / 1000));
    }

    [Fact]
    public async Task Veritabani_ince_kismi_gercekten_atiyor()
    {
        var options = new DbContextOptionsBuilder<FlowDeskDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        // The premise, stated as a test: without truncation the value written
        // and the value read back are different.
        var fine = await RoundTripAsync(options, FinerThanStorage);
        Assert.NotEqual(FinerThanStorage, fine);
        Assert.Equal(SystemClock.ToStorageResolution(FinerThanStorage), fine);

        // And with it, they are the same.
        var storable = SystemClock.ToStorageResolution(FinerThanStorage);
        Assert.Equal(storable, await RoundTripAsync(options, storable));
    }

    private static async Task<DateTimeOffset> RoundTripAsync(
        DbContextOptions<FlowDeskDbContext> options,
        DateTimeOffset createdAt)
    {
        await using var writer = new FlowDeskDbContext(options);

        var address = $"saat-{Guid.CreateVersion7():N}"[..24];

        if (!WorkspaceSlug.TryCreate(address, out var slug) || slug is null)
        {
            throw new InvalidOperationException($"Test çalışma alanı adresi üretilemedi: {address}");
        }

        var tenant = Tenant.Create("Saat Testi", slug, createdAt);

        writer.Tenants.Add(tenant);
        await writer.SaveChangesAsync(Cancellation);

        // A context of its own, so the answer comes from the database rather
        // than the change tracker that still holds the written instance.
        await using var reader = new FlowDeskDbContext(options);

        return await reader.Tenants
            .AsNoTracking()
            .Where(candidate => candidate.Id == tenant.Id)
            .Select(candidate => candidate.CreatedAt)
            .SingleAsync(Cancellation);
    }
}
