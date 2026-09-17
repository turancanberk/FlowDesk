using System.ComponentModel.DataAnnotations;

namespace FlowDesk.Infrastructure.Email;

/// <summary>SMTP settings and the address links point at.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 1025;

    /// <summary>
    /// Whether to upgrade the connection to TLS.
    /// </summary>
    /// <remarks>
    /// False only for the local Mailpit container, which speaks plain SMTP on
    /// the loopback interface. Any real provider must have this on: without it
    /// the credentials below cross the network in the clear
    /// (docs/SECURITY.md).
    /// </remarks>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Empty when the server accepts anonymous submission, as Mailpit does.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Never has a default and never appears in a committed file.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string FromDisplayName { get; set; } = "FlowDesk";

    /// <summary>
    /// Where links in e-mails point.
    /// </summary>
    /// <remarks>
    /// Configured rather than derived from the request, because the message is
    /// built in the worker, where there is no request to derive it from — and
    /// because a link built from an attacker-supplied Host header is how
    /// invitation e-mails get turned into phishing.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    [Url]
    public string WebBaseUrl { get; set; } = "http://localhost:3000";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
