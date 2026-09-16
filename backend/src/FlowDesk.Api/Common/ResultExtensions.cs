using FlowDesk.Application.Common;

namespace FlowDesk.Api.Common;

/// <summary>
/// Turns application results into HTTP responses.
/// </summary>
/// <remarks>
/// The mapping lives in one place so that the same failure always produces the
/// same status code and the same body shape. Endpoints decide what to do, not
/// how a failure is rendered (docs/API_CONVENTIONS.md).
/// </remarks>
public static class ResultExtensions
{
    public static IResult ToProblem(this ApplicationError error, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(httpContext);

        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        return Results.Problem(
            title: TitleFor(error.Type),
            detail: error.Message,
            statusCode: statusCode,
            instance: httpContext.Request.Path,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                // The stable code lets the client branch on the failure without
                // matching on display text.
                ["code"] = error.Code,
                ["traceId"] = httpContext.TraceIdentifier,
            });
    }

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Doğrulama hatası",
        ErrorType.NotFound => "Kayıt bulunamadı",
        ErrorType.Conflict => "Çakışma",
        ErrorType.Unauthorized => "Kimlik doğrulanamadı",
        ErrorType.Forbidden => "Bu işlem için yetkiniz yok",
        _ => "İstek işlenemedi",
    };
}
