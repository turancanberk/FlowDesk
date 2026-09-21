using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Infrastructure.DemoData;

/// <summary>
/// Settings for the demo data seed.
/// </summary>
/// <remarks>
/// The password is configuration rather than a constant in the source. This
/// repository is public, and the accounts the seed creates are ordinary
/// accounts: anything written here would be a working credential for every
/// deployment that ever ran the seed (CLAUDE.md, docs/SECURITY.md).
///
/// <para>
/// Development is the exception, and a deliberate one. A demo on a developer's
/// own machine is worth nothing if the person cannot sign in to it, and the
/// database it runs against is reachable only from that machine. Outside
/// development the seed refuses to run without a password of its own.
/// </para>
/// </remarks>
public sealed class DemoDataOptions
{
    public const string SectionName = "DemoData";

    /// <summary>The password every demo account is created with.</summary>
    [MinLength(10)]
    public string? Password { get; set; }
}
