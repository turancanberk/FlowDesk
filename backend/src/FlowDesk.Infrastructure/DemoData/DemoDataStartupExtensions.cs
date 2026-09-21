using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FlowDesk.Infrastructure.DemoData;

/// <summary>
/// Runs the demo seed when the host was started for that and nothing else.
/// </summary>
/// <remarks>
/// A command-line argument rather than a setting or an endpoint, and that is
/// the point: a setting can be switched on by accident and stays on, and an
/// endpoint is a way to write ten thousand rows into a running system from
/// outside it. An argument only happens when somebody typed it, once, and the
/// process exits afterwards without ever serving a request.
/// </remarks>
public static class DemoDataStartupExtensions
{
    public const string Argument = "--seed-demo-data";

    public static bool WantsDemoDataSeed(this string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return args.Contains(Argument, StringComparer.Ordinal);
    }

    /// <summary>
    /// Seeds, reports what happened, and returns the process exit code.
    /// </summary>
    public static async Task<int> RunFlowDeskDemoDataSeedAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("FlowDesk.DemoData");
        var environment = provider.GetRequiredService<IHostEnvironment>();
        var options = provider.GetRequiredService<IOptions<DemoDataOptions>>().Value;

        var password = DemoDataPassword.Resolve(options, environment.IsDevelopment());

        if (!password.IsResolved)
        {
            logger.LogError("Demo verisi yazılmadı. {Problem}", password.Problem);

            return 1;
        }

        /*
          Built here rather than registered in the container: this class is
          needed by one command in one process, and registering it would put a
          seeder in the dependency graph of every API and worker that ever
          starts (CLAUDE.md — soyutlamanın gerekçesi olmalı).
        */
        var seeder = ActivatorUtilities.CreateInstance<DemoDataSeeder>(provider);
        var result = await seeder.SeedAsync(password.Password!, cancellationToken);

        if (!result.WasSeeded)
        {
            logger.LogWarning(
                "Demo verisi zaten var, hiçbir şey yazılmadı. Mevcut çalışma alanları: {Workspaces}",
                result.ExistingWorkspaces);

            return 0;
        }

        /*
          The address, never the password. A credential in a log file is a
          credential in every place that log is copied to (docs/SECURITY.md
          §12); whoever ran this already knows what they typed.
        */
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Demo verisi yazıldı: {Accounts} hesap, {Customers} müşteri, {Tickets} talep, "
                + "{Tasks} görev. Giriş adresi: {SignInEmail}",
                result.Accounts,
                result.Customers,
                result.Tickets,
                result.Tasks,
                result.SignInEmail);
        }

        return 0;
    }
}
