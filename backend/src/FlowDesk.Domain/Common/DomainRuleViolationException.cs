namespace FlowDesk.Domain.Common;

/// <summary>
/// Thrown when an operation would leave an entity in a state its own rules
/// forbid.
/// </summary>
/// <remarks>
/// This signals a programming error, not an expected outcome. Expected
/// outcomes — wrong password, expired invitation, replayed token — are returned
/// as results by the application layer and never surface as exceptions.
/// Reaching this exception means a caller bypassed a check it was supposed to
/// make.
/// </remarks>
public sealed class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException(string message)
        : base(message)
    {
    }

    public DomainRuleViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DomainRuleViolationException()
        : base("A domain rule was violated.")
    {
    }
}
