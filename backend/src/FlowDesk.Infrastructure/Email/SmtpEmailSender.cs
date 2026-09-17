using FlowDesk.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace FlowDesk.Infrastructure.Email;

/// <summary>
/// Sends mail over SMTP with MailKit.
/// </summary>
/// <remarks>
/// MailKit rather than <c>System.Net.Mail.SmtpClient</c>, whose own
/// documentation says it is not recommended for new work and does not support
/// the modern protocols a real provider requires.
///
/// <para>
/// A connection per message. Holding one open would mean managing its lifetime
/// across an idle worker, reconnecting after the server drops it, and keeping
/// the client — which is not thread-safe — off concurrent consumers. Messages
/// here are occasional, so the handshake costs far less than that machinery.
/// </para>
/// </remarks>
public sealed partial class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        using var mime = new MimeMessage();

        mime.From.Add(new MailboxAddress(_options.FromDisplayName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToDisplayName ?? string.Empty, message.ToAddress));
        mime.Subject = message.Subject;

        /*
          Both bodies, not one. A client that refuses HTML falls back to the
          text part; sending only HTML would deliver a blank message to it, and
          sending only text would lose the link's shape.
        */
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
        }.ToMessageBody();

        using var client = new SmtpClient
        {
            Timeout = (int)TimeSpan.FromSeconds(_options.TimeoutSeconds).TotalMilliseconds,
        };

        /*
          StartTlsWhenAvailable is not used. It silently falls back to plain
          text when a server does not offer TLS, which is exactly the case
          where credentials would cross the network readable. Either TLS is
          required or it is explicitly off for a local container.
        */
        var security = _options.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);

        if (!string.IsNullOrEmpty(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        LogSent(_logger, message.ToAddress, message.Subject);
    }

    // Source-generated so the arguments are not boxed when the level is off
    // (CA1873).
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "E-posta gönderildi: {ToAddress} — {Subject}")]
    private static partial void LogSent(ILogger logger, string toAddress, string subject);
}
