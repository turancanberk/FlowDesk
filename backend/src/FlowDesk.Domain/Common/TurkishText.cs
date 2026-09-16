using System.Globalization;
using System.Text;

namespace FlowDesk.Domain.Common;

/// <summary>
/// Folds Turkish text into a plain ASCII form for slugs and search.
/// </summary>
/// <remarks>
/// Turkish has two distinct letter i — dotted (i/İ) and dotless (ı/I) — and
/// neither culture-aware nor invariant lowercasing gives a form that behaves
/// the way a searching user expects. Invariant lowercasing turns "YAZILIM" into
/// "yazilim" with a dotted i, while the stored "Yazılım" lowercases to "yazılım"
/// with a dotless one; the two never match. Culture-aware lowercasing swaps
/// which pair breaks and makes the result depend on the server's locale.
///
/// Folding both sides to ASCII sidesteps the question: "yazilim", "YAZILIM" and
/// "Yazılım" all become "yazilim", so someone typing on a keyboard without
/// Turkish characters still finds the record.
/// </remarks>
public static class TurkishText
{
    /// <summary>
    /// Lowercases and reduces Turkish and accented letters to ASCII.
    /// </summary>
    /// <remarks>
    /// Lossy by design. The result is only ever compared against another folded
    /// value; it is never shown to a user or stored in place of the original.
    /// </remarks>
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var transliterated = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            transliterated.Append(character switch
            {
                'ı' or 'I' or 'İ' or 'i' => "i",
                'ş' or 'Ş' => "s",
                'ğ' or 'Ğ' => "g",
                'ü' or 'Ü' => "u",
                'ö' or 'Ö' => "o",
                'ç' or 'Ç' => "c",
                _ => character.ToString(),
            });
        }

        // Removes accents left on any other Latin letters, e.g. "é" to "e".
        var decomposed = transliterated.ToString().Normalize(NormalizationForm.FormD);
        var ascii = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                ascii.Append(character);
            }
        }

        return ascii.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Folds several values into one searchable string.
    /// </summary>
    /// <remarks>
    /// Lets a single column back search across a record's name, company and
    /// e-mail with one comparison rather than three.
    /// </remarks>
    public static string FoldAll(params string?[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var parts = values
            .Select(Fold)
            .Where(part => part.Length > 0);

        return string.Join(' ', parts);
    }
}
