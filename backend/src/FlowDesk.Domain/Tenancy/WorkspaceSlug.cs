using System.Text;
using FlowDesk.Domain.Common;

namespace FlowDesk.Domain.Tenancy;

/// <summary>
/// The URL-safe identifier for a workspace, as it appears in
/// <c>/app/{workspaceSlug}/…</c>.
/// </summary>
/// <remarks>
/// Modelled as a value object rather than a bare string because the rules are
/// easy to get subtly wrong and a malformed slug produces an unreachable
/// workspace. Having one construction path means every slug in the system —
/// typed by a user, generated from a name, or read back from the database — has
/// passed the same checks.
/// </remarks>
public sealed record WorkspaceSlug
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 40;

    private WorkspaceSlug(string value) => Value = value;

    public string Value { get; }

    public override string ToString() => Value;

    public static implicit operator string(WorkspaceSlug slug) =>
        slug is null ? string.Empty : slug.Value;

    /// <summary>
    /// Validates an existing slug.
    /// </summary>
    /// <exception cref="DomainRuleViolationException">The value is not a usable slug.</exception>
    public static WorkspaceSlug Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleViolationException("A workspace slug cannot be empty.");
        }

        var candidate = value.Trim();

        if (candidate.Length is < MinimumLength or > MaximumLength)
        {
            throw new DomainRuleViolationException(
                $"A workspace slug must be between {MinimumLength} and {MaximumLength} characters.");
        }

        if (!IsWellFormed(candidate))
        {
            throw new DomainRuleViolationException(
                "A workspace slug may contain only lowercase letters, digits and single inner hyphens.");
        }

        return new WorkspaceSlug(candidate);
    }

    /// <summary>Validates without throwing.</summary>
    public static bool TryCreate(string? value, out WorkspaceSlug? slug)
    {
        try
        {
            slug = Create(value ?? string.Empty);
            return true;
        }
        catch (DomainRuleViolationException)
        {
            slug = null;
            return false;
        }
    }

    /// <summary>
    /// Derives a slug from a workspace name.
    /// </summary>
    /// <remarks>
    /// Turkish letters are folded to ASCII by <see cref="TurkishText"/>, so
    /// "Kuzey Yazılım" becomes "kuzey-yazilim" rather than an escaped mess in
    /// the address bar. The same folding backs customer search, which keeps the
    /// two consistent.
    /// </remarks>
    public static string SuggestFrom(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var folded = TurkishText.Fold(name);
        var slug = new StringBuilder(folded.Length);
        var previousWasHyphen = false;

        foreach (var character in folded)
        {
            if (char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character))
            {
                slug.Append(character);
                previousWasHyphen = false;
            }
            else if (!previousWasHyphen && slug.Length > 0)
            {
                slug.Append('-');
                previousWasHyphen = true;
            }
        }

        var result = slug.ToString().Trim('-');

        return result.Length > MaximumLength
            ? result[..MaximumLength].Trim('-')
            : result;
    }

    private static bool IsWellFormed(string candidate)
    {
        if (candidate[0] == '-' || candidate[^1] == '-')
        {
            return false;
        }

        var previousWasHyphen = false;

        foreach (var character in candidate)
        {
            if (char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character))
            {
                previousWasHyphen = false;
                continue;
            }

            if (character != '-' || previousWasHyphen)
            {
                return false;
            }

            previousWasHyphen = true;
        }

        return true;
    }
}
