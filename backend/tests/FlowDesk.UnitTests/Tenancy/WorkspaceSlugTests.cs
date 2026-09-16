using FlowDesk.Domain.Common;
using FlowDesk.Domain.Tenancy;

namespace FlowDesk.UnitTests.Tenancy;

public sealed class WorkspaceSlugTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("acme-teknoloji")]
    [InlineData("kuzey-yazilim-2026")]
    [InlineData("a1b")]
    public void Well_formed_values_are_accepted(string value)
    {
        var slug = WorkspaceSlug.Create(value);

        Assert.Equal(value, slug.Value);
    }

    [Theory]
    [InlineData("", "boş")]
    [InlineData("ab", "çok kısa")]
    [InlineData("Acme", "büyük harf")]
    [InlineData("acme teknoloji", "boşluk")]
    [InlineData("acme_teknoloji", "alt çizgi")]
    [InlineData("-acme", "başta tire")]
    [InlineData("acme-", "sonda tire")]
    [InlineData("acme--teknoloji", "art arda tire")]
    [InlineData("acme.teknoloji", "nokta")]
    [InlineData("kuzey-yazılım", "Türkçe karakter")]
    public void Malformed_values_are_rejected(string value, string reason)
    {
        var exception = Record.Exception(() => WorkspaceSlug.Create(value));

        Assert.NotNull(exception);
        Assert.IsType<DomainRuleViolationException>(exception);
        Assert.False(WorkspaceSlug.TryCreate(value, out _), reason);
    }

    [Fact]
    public void Values_longer_than_the_maximum_are_rejected()
    {
        var tooLong = new string('a', WorkspaceSlug.MaximumLength + 1);

        Assert.Throws<DomainRuleViolationException>(() => WorkspaceSlug.Create(tooLong));
    }

    /// <summary>
    /// A Turkish-speaking user should not have to invent an ASCII address for
    /// their own organisation's name.
    /// </summary>
    [Theory]
    [InlineData("Acme Teknoloji", "acme-teknoloji")]
    [InlineData("Kuzey Yazılım", "kuzey-yazilim")]
    [InlineData("Nova Lojistik", "nova-lojistik")]
    [InlineData("Işık Enerji", "isik-enerji")]
    [InlineData("Çağrı Merkezi", "cagri-merkezi")]
    [InlineData("Öztürk Gıda", "ozturk-gida")]
    [InlineData("Şahin & Oğulları", "sahin-ogullari")]
    [InlineData("  Boşluklu   Ad  ", "bosluklu-ad")]
    public void Names_are_transliterated_into_usable_slugs(string name, string expected)
    {
        Assert.Equal(expected, WorkspaceSlug.SuggestFrom(name));
    }

    /// <summary>
    /// Whatever the generator produces must survive validation; otherwise a
    /// user could type a perfectly ordinary name and get an unusable address.
    /// </summary>
    [Theory]
    [InlineData("Acme Teknoloji")]
    [InlineData("Kuzey Yazılım")]
    [InlineData("Işık Enerji A.Ş.")]
    [InlineData("Çağrı Merkezi Hizmetleri Limited Şirketi")]
    public void Generated_slugs_pass_validation(string name)
    {
        var suggestion = WorkspaceSlug.SuggestFrom(name);

        Assert.True(WorkspaceSlug.TryCreate(suggestion, out var slug));
        Assert.NotNull(slug);
    }

    [Fact]
    public void Generated_slugs_are_truncated_without_a_trailing_hyphen()
    {
        var suggestion = WorkspaceSlug.SuggestFrom(new string('a', 30) + " " + new string('b', 30));

        Assert.True(suggestion.Length <= WorkspaceSlug.MaximumLength);
        Assert.DoesNotContain("--", suggestion, StringComparison.Ordinal);
        Assert.False(suggestion.EndsWith('-'));
    }
}
