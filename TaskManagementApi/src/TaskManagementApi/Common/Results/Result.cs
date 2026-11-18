using System.Diagnostics.CodeAnalysis;

namespace TaskManagementApi.Common.Results;

/// <summary>
/// Zero-allocation stack-allocated Result value type for void/non-value operations.
/// </summary>
public readonly record struct Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    private Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static implicit operator Result(Error error) => Failure(error);

    public TResult Match<TResult>(Func<TResult> onSuccess, Func<Error, TResult> onFailure) =>
        this switch
        {
            { IsSuccess: true } => onSuccess(),
            { Error: var error } => onFailure(error)
        };
}

/// <summary>
/// Zero-allocation stack-allocated generic Result&lt;T&gt; value type.
/// Avoids heap allocations and exception-throwing overhead for expected domain/validation failures.
/// </summary>
public readonly record struct Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public Error Error { get; }

    private Result(bool isSuccess, T? value, Error error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, Error.None);
    public static Result<T> Failure(Error error) => new(false, default, error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);

    [MemberNotNullWhen(true, nameof(Value))]
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = Value;
        return IsSuccess;
    }

    /// <summary>
    /// Exhaustively evaluates the result using modern C# property pattern matching.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        this switch
        {
            { IsSuccess: true, Value: var value } => onSuccess(value!),
            { Error: var error } => onFailure(error)
        };
}
