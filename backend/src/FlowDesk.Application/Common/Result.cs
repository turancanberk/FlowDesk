namespace FlowDesk.Application.Common;

/// <summary>
/// Outcome of a use case that can fail in expected ways.
/// </summary>
/// <remarks>
/// Expected failures are returned, not thrown. Wrong credentials and replayed
/// tokens are normal traffic on an authentication endpoint; using exceptions
/// for them would mean paying stack-unwinding costs on the happy path of an
/// attack and would blur the line between "this request was rejected" and
/// "this server is broken".
///
/// Unexpected faults still throw and are handled centrally by the API.
/// </remarks>
public class Result
{
    private readonly ApplicationError? _error;

    private protected Result(bool isSuccess, ApplicationError? error)
    {
        if (isSuccess && error is not null)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error is null)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        _error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>The failure reason. Only valid when <see cref="IsFailure"/>.</summary>
    public ApplicationError Error =>
        _error ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new(true, null);

    public static Result Failure(ApplicationError error) => new(false, error);

    /// <summary>
    /// Factories for the generic result live here rather than on
    /// <see cref="Result{TValue}"/> so that call sites read
    /// <c>Result.Success(value)</c> and infer the type argument instead of
    /// repeating it (CA1000).
    /// </summary>
    public static Result<TValue> Success<TValue>(TValue value) => new(true, value, null);

    public static Result<TValue> Failure<TValue>(ApplicationError error) => new(false, default, error);
}

/// <summary>Outcome that carries a value when it succeeds.</summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(bool isSuccess, TValue? value, ApplicationError? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>The produced value. Only valid when <see cref="Result.IsSuccess"/>.</summary>
    public TValue Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException("A failed result has no value.");
}
