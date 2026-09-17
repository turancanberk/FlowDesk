namespace FlowDesk.Infrastructure.Messaging;

/// <summary>
/// A message body could not be read as the type its queue expects.
/// </summary>
/// <remarks>
/// Distinct from a handler failing, and the distinction decides what happens
/// next. A handler may fail because the database was briefly unreachable, so
/// its message is worth redelivering. A body that cannot be parsed will fail
/// the same way for ever and would block the queue behind it, so it is rejected
/// rather than requeued.
///
/// A dedicated type rather than catching <see cref="System.Text.Json.JsonException"/>
/// at the call site, because the empty-body case is not a JSON error and both
/// need the same treatment.
/// </remarks>
public sealed class MessageFormatException : Exception
{
    public MessageFormatException()
        : base("Mesaj gövdesi çözümlenemedi.")
    {
    }

    public MessageFormatException(string message)
        : base(message)
    {
    }

    public MessageFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
