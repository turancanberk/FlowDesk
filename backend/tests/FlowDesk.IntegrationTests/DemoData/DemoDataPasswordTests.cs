using FlowDesk.Infrastructure.DemoData;

namespace FlowDesk.IntegrationTests.DemoData;

/// <summary>
/// Which password the demo accounts get, and when there is none (Faz 21).
/// </summary>
/// <remarks>
/// The repository is public. A default password in the source would be a
/// working credential for every deployment that ever ran the seed, which is
/// why the rule has a test of its own rather than living inside the seeder
/// (docs/SECURITY.md).
///
/// <para>
/// A plain function with no database behind it, but it lives here rather than
/// in the unit tests: that project sees only Domain and Application, and the
/// architecture tests exist to keep it that way.
/// </para>
/// </remarks>
public sealed class DemoDataPasswordTests
{
    /// <summary>What a host is normally started with.</summary>
    private static readonly string[] OrdinaryArguments = ["--urls", "http://localhost:5080"];


    [Fact]
    public void Gelistirmede_varsayilan_parola_kullanilir()
    {
        var resolution = DemoDataPassword.Resolve(new DemoDataOptions(), isDevelopment: true);

        Assert.True(resolution.IsResolved);
        Assert.Equal(DemoDataPassword.DevelopmentDefault, resolution.Password);
    }

    [Fact]
    public void Gelistirme_disinda_parola_verilmezse_reddedilir()
    {
        var resolution = DemoDataPassword.Resolve(new DemoDataOptions(), isDevelopment: false);

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Password);

        // The message has to say what to set; a refusal nobody can act on is
        // only half an answer.
        Assert.Contains(DemoDataPassword.SettingName, resolution.Problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Verilen_parola_her_ortamda_kullanilir(bool isDevelopment)
    {
        var options = new DemoDataOptions { Password = "BaskaBirParola2026" };

        var resolution = DemoDataPassword.Resolve(options, isDevelopment);

        // Including in development: the setting is the answer wherever it is
        // present, so what a developer configures is what they get.
        Assert.Equal("BaskaBirParola2026", resolution.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Bos_parola_verilmemis_sayilir(string configured)
    {
        var options = new DemoDataOptions { Password = configured };

        var resolution = DemoDataPassword.Resolve(options, isDevelopment: false);

        // An empty setting is the shape a missing environment variable takes,
        // and treating it as a password would create accounts nobody can use.
        Assert.False(resolution.IsResolved);
    }

    [Fact]
    public void Seed_komutu_yalnizca_kendi_bagimsiz_degiskeniyle_calisir()
    {
        Assert.True(new[] { DemoDataStartupExtensions.Argument }.WantsDemoDataSeed());
        Assert.False(OrdinaryArguments.WantsDemoDataSeed());
        Assert.False(Array.Empty<string>().WantsDemoDataSeed());
    }
}
