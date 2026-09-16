using FlowDesk.Domain.Common;

namespace FlowDesk.UnitTests.Common;

/// <summary>
/// Turkish case folding for slugs and search.
/// </summary>
/// <remarks>
/// These assertions exist because the obvious implementations are wrong.
/// Invariant lowercasing maps "YAZILIM" to a dotted "yazilim" while "Yazılım"
/// lowercases to a dotless "yazılım", so the two never match. Culture-aware
/// lowercasing swaps which pair breaks and ties the answer to the server's
/// locale. Folding to ASCII is what makes a search behave the way the person
/// typing it expects.
/// </remarks>
public sealed class TurkishTextTests
{
    /// <summary>
    /// The case that motivated the whole helper: three spellings of the same
    /// word must fold to one value.
    /// </summary>
    [Theory]
    [InlineData("Yazılım")]
    [InlineData("YAZILIM")]
    [InlineData("yazilim")]
    [InlineData("YAZILIM ")]
    public void The_dotted_and_dotless_i_fold_to_the_same_value(string value)
    {
        Assert.Equal("yazilim", TurkishText.Fold(value).Trim());
    }

    [Theory]
    [InlineData("Teknoloji", "teknoloji")]
    [InlineData("TEKNOLOJİ", "teknoloji")]
    [InlineData("İstanbul", "istanbul")]
    [InlineData("ISTANBUL", "istanbul")]
    [InlineData("Şahin", "sahin")]
    [InlineData("ÇAĞRI", "cagri")]
    [InlineData("Öztürk", "ozturk")]
    [InlineData("Güneş", "gunes")]
    public void Turkish_letters_fold_to_ascii(string value, string expected)
    {
        Assert.Equal(expected, TurkishText.Fold(value));
    }

    [Theory]
    [InlineData("café", "cafe")]
    [InlineData("naïve", "naive")]
    public void Accents_on_other_latin_letters_are_removed(string value, string expected)
    {
        Assert.Equal(expected, TurkishText.Fold(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_input_folds_to_an_empty_string(string? value)
    {
        Assert.Equal(string.Empty, TurkishText.Fold(value));
    }

    [Fact]
    public void Spacing_and_punctuation_are_preserved()
    {
        // Only letters are folded; the helper is not a slug generator.
        Assert.Equal("kuzey yazilim a.s.", TurkishText.Fold("Kuzey Yazılım A.Ş."));
    }

    [Fact]
    public void Several_values_fold_into_one_searchable_string()
    {
        var folded = TurkishText.FoldAll("Ahmet Yılmaz", "Nova Lojistik", "AHMET@NOVA.TEST");

        Assert.Equal("ahmet yilmaz nova lojistik ahmet@nova.test", folded);
    }

    [Fact]
    public void Missing_values_are_skipped_rather_than_leaving_gaps()
    {
        var folded = TurkishText.FoldAll("Acme", null, "   ", "acme@ornek.test");

        Assert.Equal("acme acme@ornek.test", folded);
    }
}
