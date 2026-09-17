namespace FlowDesk.Application.Abstractions;

/// <summary>
/// Sends one message to one recipient.
/// </summary>
/// <remarks>
/// The application says what to send and to whom; SMTP, retries and transport
/// belong to infrastructure (ADR-0001).
///
/// <para>
/// <b>A sent e-mail cannot be unsent.</b> Unlike a database write, it is not
/// covered by the transaction around it, so a caller that sends and then fails
/// will send again on redelivery. Callers make the send the last thing they do,
/// so anything that can fail has already failed by then (ADR-0034).
/// </para>
/// </remarks>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>An outgoing message.</summary>
/// <param name="HtmlBody">
/// The message as the recipient will normally see it.
/// </param>
/// <param name="TextBody">
/// The same message as plain text. Sent alongside rather than instead: some
/// clients refuse HTML, and a message that arrives blank is worse than a plain
/// one.
/// </param>
public sealed record EmailMessage(
    string ToAddress,
    string? ToDisplayName,
    string Subject,
    string HtmlBody,
    string TextBody);
