namespace FlowDesk.Infrastructure.DemoData;

/// <summary>
/// Decides which password the demo accounts are created with.
/// </summary>
/// <remarks>
/// Separated from the seeder because it is the one decision here with a
/// security consequence, and it should be readable and testable on its own.
///
/// <para>
/// The rule: outside development the password must be supplied. A default
/// baked into a public repository is a published credential, and it would be
/// published for every deployment that ever ran the seed — including one where
/// somebody ran it to "see what it does" (CLAUDE.md, docs/SECURITY.md).
/// </para>
/// </remarks>
public static class DemoDataPassword
{
    /// <summary>
    /// Used only when the application is running in development.
    /// </summary>
    /// <remarks>
    /// Not a secret and not treated as one: it exists so a developer can sign
    /// in to the demo on their own machine. It satisfies the account policy's
    /// ten-character minimum.
    /// </remarks>
    public const string DevelopmentDefault = "DemoParola2026";

    /// <summary>The setting that supplies it everywhere else.</summary>
    public const string SettingName = $"{DemoDataOptions.SectionName}:Password";

    public static DemoDataPasswordResolution Resolve(DemoDataOptions options, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.Password))
        {
            return DemoDataPasswordResolution.Configured(options.Password);
        }

        if (isDevelopment)
        {
            return DemoDataPasswordResolution.Configured(DevelopmentDefault);
        }

        return DemoDataPasswordResolution.Missing(
            $"Demo verisi yalnızca geliştirme ortamında varsayılan parola ile çalışır. "
            + $"Bu ortamda {SettingName} (ya da DemoData__Password ortam değişkeni) verilmelidir.");
    }
}

/// <param name="Password">Null when the setting is required and was not given.</param>
public sealed record DemoDataPasswordResolution(string? Password, string? Problem)
{
    public bool IsResolved => Password is not null;

    public static DemoDataPasswordResolution Configured(string password) => new(password, null);

    public static DemoDataPasswordResolution Missing(string problem) => new(null, problem);
}
