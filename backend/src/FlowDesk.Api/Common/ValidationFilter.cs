using FluentValidation;

namespace FlowDesk.Api.Common;

/// <summary>
/// Runs the FluentValidation validator registered for an endpoint's request
/// type and turns failures into a ProblemDetails response.
/// </summary>
/// <remarks>
/// Attached explicitly per endpoint rather than through the deprecated
/// automatic-validation package (ADR-0016). Being explicit also means an
/// endpoint without validation is visibly without validation, instead of
/// silently relying on a convention.
/// </remarks>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
    where TRequest : class
{
    private readonly IValidator<TRequest> _validator;

    public ValidationFilter(IValidator<TRequest> validator) => _validator = validator;

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();

        if (request is null)
        {
            return await next(context);
        }

        var validationResult = await _validator.ValidateAsync(
            request,
            context.HttpContext.RequestAborted);

        if (validationResult.IsValid)
        {
            return await next(context);
        }

        var errors = validationResult.Errors
            .GroupBy(failure => ToCamelCase(failure.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        return Results.ValidationProblem(
            errors,
            title: "Doğrulama hatası",
            detail: "Gönderilen alanlardan bazıları geçersiz.",
            instance: context.HttpContext.Request.Path,
            // 422 rather than the framework default of 400: the request parsed
            // fine, it is the values that are unacceptable
            // (docs/API_CONVENTIONS.md).
            statusCode: StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = "request.validation_failed",
                ["traceId"] = context.HttpContext.TraceIdentifier,
            });
    }

    /// <summary>Matches the JSON property naming used in requests and responses.</summary>
    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || char.IsLower(propertyName[0]))
        {
            return propertyName;
        }

        return string.Create(
            propertyName.Length,
            propertyName,
            static (destination, source) =>
            {
                source.AsSpan().CopyTo(destination);
                destination[0] = char.ToLowerInvariant(destination[0]);
            });
    }
}
