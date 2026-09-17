using FlowDesk.Application.Tickets;
using FlowDesk.Domain.Common;

namespace FlowDesk.UnitTests.Tickets;

/// <summary>
/// What may be uploaded, and under what name.
/// </summary>
/// <remarks>
/// These are security rules, not formatting preferences. They must never be
/// deleted, skipped or weakened: each one closes a path by which an uploader
/// could reach somewhere they should not (docs/SECURITY.md).
/// </remarks>
public sealed class AttachmentRulesTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("application/pdf")]
    [InlineData("text/csv")]
    // Case is not the uploader's to decide.
    [InlineData("IMAGE/PNG")]
    public void An_allowed_type_is_accepted(string contentType)
    {
        Assert.True(AttachmentRules.IsAllowedContentType(contentType));
    }

    /// <summary>
    /// Anything not named is refused, including things that look harmless.
    /// </summary>
    /// <remarks>
    /// SVG is the one worth calling out. It is an image by name and a document
    /// in fact: it can carry script, so serving one from our own origin would
    /// let whoever uploaded it run code against whoever opened it.
    /// </remarks>
    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("application/x-msdownload")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_not_on_the_list_is_refused(string? contentType)
    {
        Assert.False(AttachmentRules.IsAllowedContentType(contentType));
    }

    /// <summary>
    /// A name carrying a path keeps only its last segment.
    /// </summary>
    /// <remarks>
    /// The name never addresses the bytes — the storage key does — but it is
    /// written into a download header and shown to people, and taking only the
    /// last segment cannot be tricked the way rewriting a path can.
    /// </remarks>
    [Theory]
    [InlineData("../../../etc/passwd", "passwd")]
    [InlineData("..\\..\\windows\\system32\\config", "config")]
    [InlineData("/mutlak/yol/rapor.pdf", "rapor.pdf")]
    [InlineData("C:\\Kullanicilar\\ayse\\fatura.pdf", "fatura.pdf")]
    public void A_name_carrying_a_path_keeps_only_its_last_segment(string given, string expected)
    {
        Assert.Equal(expected, AttachmentRules.SanitiseFileName(given));
    }

    [Fact]
    public void Control_characters_are_replaced()
    {
        // A newline in a name can split a Content-Disposition header apart.
        var cleaned = AttachmentRules.SanitiseFileName("rapor\r\nX-Injected: 1.pdf");

        Assert.DoesNotContain('\r', cleaned);
        Assert.DoesNotContain('\n', cleaned);
    }

    [Fact]
    public void A_leading_dot_is_dropped()
    {
        // A dotfile hides itself on the reader's machine.
        Assert.Equal("gizli.txt", AttachmentRules.SanitiseFileName(".gizli.txt"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("/")]
    public void A_name_that_cleans_away_to_nothing_gets_a_placeholder(string given)
    {
        // Supplied rather than refused: an otherwise valid file should not be
        // lost over its own name.
        Assert.Equal("dosya", AttachmentRules.SanitiseFileName(given));
    }

    [Fact]
    public void A_long_name_is_cut_to_the_column_length()
    {
        var cleaned = AttachmentRules.SanitiseFileName(new string('a', 400) + ".pdf");

        Assert.Equal(FlowDesk.Domain.Tickets.Attachment.MaximumFileNameLength, cleaned.Length);
    }

    [Fact]
    public void Turkish_characters_survive_cleaning()
    {
        // The name is display text, not a path, so there is no reason to fold
        // it — and a Turkish file name showing as "sozlesme" would look broken.
        Assert.Equal("sözleşme-şubat.pdf", AttachmentRules.SanitiseFileName("sözleşme-şubat.pdf"));
    }

    /// <summary>
    /// The key is built only from ids the server controls.
    /// </summary>
    /// <remarks>
    /// The workspace leads, so one organisation's files sit under a different
    /// prefix from another's — which means a mistake in the authorisation layer
    /// still does not put them in the same place.
    /// </remarks>
    [Fact]
    public void A_storage_key_leads_with_the_workspace()
    {
        var tenantId = Guid.CreateVersion7();
        var ticketId = Guid.CreateVersion7();
        var attachmentId = Guid.CreateVersion7();

        var key = AttachmentRules.BuildStorageKey(tenantId, ticketId, attachmentId);

        Assert.Equal($"{tenantId:N}/{ticketId:N}/{attachmentId:N}", key);
        Assert.StartsWith($"{tenantId:N}/", key, StringComparison.Ordinal);
    }

    [Fact]
    public void A_storage_key_needs_all_three_ids()
    {
        var id = Guid.CreateVersion7();

        Assert.Throws<DomainRuleViolationException>(
            () => AttachmentRules.BuildStorageKey(Guid.Empty, id, id));
        Assert.Throws<DomainRuleViolationException>(
            () => AttachmentRules.BuildStorageKey(id, Guid.Empty, id));
        Assert.Throws<DomainRuleViolationException>(
            () => AttachmentRules.BuildStorageKey(id, id, Guid.Empty));
    }
}
