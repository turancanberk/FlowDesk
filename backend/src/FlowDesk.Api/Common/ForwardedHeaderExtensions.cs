using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace FlowDesk.Api.Common;

/// <summary>
/// Who the caller is, when there is a proxy in between.
/// </summary>
/// <remarks>
/// The rate limiter partitions by client address, and the address the socket
/// reports is the proxy's once one is in front. Believing
/// <c>X-Forwarded-For</c> from anyone would be worse than ignoring it: the
/// header is trivial to set, and a caller choosing their own value chooses
/// their own bucket. So it is believed only from addresses named in
/// configuration, and ignored entirely when none are (docs/SECURITY.md §10).
/// </remarks>
public static class ForwardedHeaderExtensions
{
    public static IServiceCollection AddFlowDeskForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<NetworkSettings>()
            .Bind(configuration.GetSection(NetworkSettings.SectionName))
            .ValidateOnStart();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            var settings = configuration.GetSection(NetworkSettings.SectionName)
                .Get<NetworkSettings>() ?? new NetworkSettings();

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            /*
              One hop. The deployment has exactly one proxy in front, so a
              longer chain means someone appended to the header on the way —
              and the entries beyond the first hop are theirs, not the
              proxy's.
            */
            options.ForwardLimit = 1;

            // The defaults trust loopback. Nothing is trusted here unless it
            // was named.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var entry in settings.TrustedProxies)
            {
                Trust(options, entry);
            }
        });

        return services;
    }

    /// <summary>
    /// Enables the forwarded headers, but only when a proxy has been named.
    /// </summary>
    public static WebApplication UseFlowDeskForwardedHeaders(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var settings = app.Services.GetRequiredService<IOptions<NetworkSettings>>().Value;

        if (settings.TrustedProxies.Count > 0)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }

    private static void Trust(ForwardedHeadersOptions options, string entry)
    {
        // System.Net.IPNetwork; the HttpOverrides type of the same name is
        // the deprecated one.
        if (System.Net.IPNetwork.TryParse(entry, out var network))
        {
            options.KnownIPNetworks.Add(network);
            return;
        }

        if (IPAddress.TryParse(entry, out var address))
        {
            options.KnownProxies.Add(address);
            return;
        }

        // A typo here would silently stop the header being honoured, and the
        // symptom — everyone in one rate-limit bucket — looks nothing like its
        // cause. Better to refuse to start.
        throw new InvalidOperationException(
            $"Network:TrustedProxies içindeki '{entry}' bir IP adresi ya da CIDR aralığı değil.");
    }
}
