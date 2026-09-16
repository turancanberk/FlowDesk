namespace FlowDesk.Application.Common;

/// <summary>
/// An expected failure: a business or validation outcome the caller should
/// handle, not an unexpected system fault.
/// </summary>
/// <remarks>
/// Named <c>ApplicationError</c> rather than <c>Error</c> because the shorter
/// name collides with a reserved word in other .NET languages (CA1716).
/// </remarks>
/// <param name="Code">
/// Stable machine-readable identifier, e.g. <c>auth.invalid_credentials</c>.
/// Used to select the HTTP status and to look up the message shown to the user.
/// </param>
/// <param name="Message">
/// Turkish text safe to display. It never echoes internal state, and for
/// authentication it never distinguishes "no such user" from "wrong password".
/// </param>
/// <param name="Type">Determines how the API translates this into a status code.</param>
public sealed record ApplicationError(string Code, string Message, ErrorType Type)
{
    public static ApplicationError Validation(string code, string message) =>
        new(code, message, ErrorType.Validation);

    public static ApplicationError NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    public static ApplicationError Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);

    public static ApplicationError Unauthorized(string code, string message) =>
        new(code, message, ErrorType.Unauthorized);

    public static ApplicationError Forbidden(string code, string message) =>
        new(code, message, ErrorType.Forbidden);
}

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
}
