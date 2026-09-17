using System.Net;

namespace FlowDesk.Infrastructure.Notifications;

/// <summary>
/// The shared shape of FlowDesk's e-mails.
/// </summary>
/// <remarks>
/// Built as strings rather than through a templating engine. There are three
/// messages, they share one layout, and a template engine would add a package,
/// a file format and a place for logic to hide for no gain at this size. If the
/// count grows past what fits in one file, that trade changes.
///
/// <para>
/// Inline styles only. Mail clients strip stylesheets and most ignore
/// <c>&lt;style&gt;</c> blocks, so anything not written on the element itself is
/// a suggestion the reader may never see.
/// </para>
/// </remarks>
internal static class EmailBodies
{
    /// <summary>
    /// Escapes text for inclusion in HTML.
    /// </summary>
    /// <remarks>
    /// Every value in these messages comes from a person — a ticket subject, a
    /// comment, a display name — so none of it can be trusted as markup. An
    /// unescaped subject would let whoever typed it write HTML into a colleague's
    /// inbox (docs/SECURITY.md).
    /// </remarks>
    public static string Escape(string value) => WebUtility.HtmlEncode(value);

    public static string Html(string heading, string bodyHtml, string actionLabel, string actionUrl)
    {
        var safeUrl = Escape(actionUrl);

        return $"""
            <div style="font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;font-size:15px;line-height:1.5;color:#1a2b32;max-width:520px">
              <h1 style="font-size:17px;font-weight:600;margin:0 0 16px">{Escape(heading)}</h1>
              <p style="margin:0 0 20px">{bodyHtml}</p>
              <p style="margin:0 0 24px">
                <a href="{safeUrl}" style="display:inline-block;background:#12545e;color:#ffffff;text-decoration:none;padding:9px 16px;border-radius:6px;font-weight:500">{Escape(actionLabel)}</a>
              </p>
              <p style="margin:0;font-size:13px;color:#5b6b72">
                Bağlantı çalışmazsa bu adresi tarayıcınıza yapıştırın:<br>
                <span style="word-break:break-all">{safeUrl}</span>
              </p>
            </div>
            """;
    }

    /// <summary>
    /// The same message as plain text.
    /// </summary>
    /// <remarks>
    /// Not escaped, because plain text is not markup — escaping here would show
    /// the reader <c>&amp;amp;</c> where an ampersand belongs.
    /// </remarks>
    public static string Text(string body, string actionUrl) =>
        $"""
        {body}

        {actionUrl}
        """;
}
