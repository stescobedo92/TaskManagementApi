namespace TaskManagementApi.Common.Results;

/// <summary>
/// Categorizes domain and application errors for deterministic HTTP status mapping.
/// </summary>
public enum ErrorType : byte
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Failure = 4
}

/// <summary>
/// Zero-allocation stack-allocated error value type for the Result pattern.
/// </summary>
public readonly record struct Error(
    string Code,
    string Description,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public bool IsNone => Type is ErrorType.None;

    public static Error Validation(string code, string description, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(code, description, ErrorType.Validation, errors);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);
}
