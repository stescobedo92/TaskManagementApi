using System.Buffers;
using TaskManagementApi.Common.Results;
using TaskManagementApi.Contracts;
using TaskManagementApi.Domain;

namespace TaskManagementApi.Validation;

/// <summary>
/// High-performance, zero-allocation (on valid hot path) validator for Task contracts and queries.
/// Uses ReadOnlySpan&lt;char&gt;, SearchValues&lt;char&gt;, C# pattern matching, and modern switch expressions.
/// </summary>
public static class TaskValidator
{
    public const int MinTitleLength = 3;
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MinAssigneeLength = 2;
    public const int MaxAssigneeLength = 150;
    public const int MaxPageSize = 100;

    // Pre-computed SIMD-accelerated character search set for illegal control characters in single-line fields
    private static readonly SearchValues<char> ForbiddenSingleLineChars =
        SearchValues.Create("\0\r\n\b\v\f");

    public static Result<CreateTaskRequest> ValidateCreate(in CreateTaskRequest request)
    {
        Dictionary<string, List<string>>? errors = null;

        ValidateTitle(request.Title, ref errors);
        ValidateDescription(request.Description, ref errors);
        ValidateAssignee(request.Assignee, ref errors);
        ValidateDueDate(request.DueDate, ref errors);

        if (request.Status is { } status && !status.IsDefinedStatus())
        {
            AddError(ref errors, "status", "Status must be one of: Pending, InProgress, Completed.");
        }

        return errors is null
            ? Result<CreateTaskRequest>.Success(request)
            : Result<CreateTaskRequest>.Failure(BuildValidationError(errors));
    }

    public static Result<UpdateTaskRequest> ValidateUpdate(in UpdateTaskRequest request)
    {
        Dictionary<string, List<string>>? errors = null;

        ValidateTitle(request.Title, ref errors);
        ValidateDescription(request.Description, ref errors);
        ValidateAssignee(request.Assignee, ref errors);
        ValidateDueDate(request.DueDate, ref errors);

        string? statusError = request.Status switch
        {
            null => "Status is required when updating a task.",
            { } s when !s.IsDefinedStatus() => "Status must be one of: Pending, InProgress, Completed.",
            _ => null
        };

        if (statusError is not null)
        {
            AddError(ref errors, "status", statusError);
        }

        return errors is null
            ? Result<UpdateTaskRequest>.Success(request)
            : Result<UpdateTaskRequest>.Failure(BuildValidationError(errors));
    }

    public static Result<TaskItemStatus> ValidateStatusUpdate(in UpdateTaskStatusRequest request) =>
        request.Status switch
        {
            null => Error.Validation(
                "Task.Validation",
                "One or more validation errors occurred.",
                new Dictionary<string, string[]> { ["status"] = ["Status is required."] }),
            { } s when !s.IsDefinedStatus() => Error.Validation(
                "Task.Validation",
                "One or more validation errors occurred.",
                new Dictionary<string, string[]> { ["status"] = ["Status must be one of: Pending, InProgress, Completed."] }),
            { } validStatus => Result<TaskItemStatus>.Success(validStatus)
        };

    public static Result<TaskFilterQuery> ValidateFilter(
        string? rawStatus,
        string? assignee,
        int page,
        int pageSize)
    {
        Dictionary<string, List<string>>? errors = null;

        TaskItemStatus? parsedStatus = null;
        if (rawStatus is not null)
        {
            ReadOnlySpan<char> statusSpan = rawStatus.AsSpan().Trim();
            if (!statusSpan.IsEmpty)
            {
                if (TaskItemStatusExtensions.TryParseSpan(statusSpan, out TaskItemStatus statusValue))
                {
                    parsedStatus = statusValue;
                }
                else
                {
                    AddError(ref errors, "status", "Invalid status filter. Allowed values: Pending, InProgress, Completed.");
                }
            }
        }

        string? normalizedAssignee = null;
        if (assignee is not null)
        {
            ReadOnlySpan<char> assigneeSpan = assignee.AsSpan().Trim();
            if (!assigneeSpan.IsEmpty)
            {
                if (assigneeSpan.Length > MaxAssigneeLength)
                {
                    AddError(ref errors, "assignee", $"Assignee filter cannot exceed {MaxAssigneeLength} characters.");
                }
                else
                {
                    normalizedAssignee = assigneeSpan.Length == assignee.Length
                        ? assignee
                        : assigneeSpan.ToString();
                }
            }
        }

        string? pageError = page switch
        {
            < 1 => "Page number must be greater than or equal to 1.",
            _ => null
        };
        if (pageError is not null)
        {
            AddError(ref errors, "page", pageError);
        }

        string? pageSizeError = pageSize switch
        {
            < 1 => "PageSize must be at least 1.",
            > MaxPageSize => $"PageSize cannot exceed {MaxPageSize}.",
            _ => null
        };
        if (pageSizeError is not null)
        {
            AddError(ref errors, "pageSize", pageSizeError);
        }

        return errors is null
            ? Result<TaskFilterQuery>.Success(new TaskFilterQuery(parsedStatus, normalizedAssignee, page, pageSize))
            : Result<TaskFilterQuery>.Failure(BuildValidationError(errors));
    }

    private static void ValidateTitle(string? title, ref Dictionary<string, List<string>>? errors)
    {
        ReadOnlySpan<char> span = title.AsSpan().Trim();

        string? error = span.Length switch
        {
            0 => "Title is required and cannot be empty or whitespace.",
            < MinTitleLength => $"Title must be at least {MinTitleLength} characters long.",
            > MaxTitleLength => $"Title cannot exceed {MaxTitleLength} characters.",
            _ when span.ContainsAny(ForbiddenSingleLineChars) => "Title contains invalid control or newline characters.",
            _ => null
        };

        if (error is not null)
        {
            AddError(ref errors, "title", error);
        }
    }

    private static void ValidateDescription(string? description, ref Dictionary<string, List<string>>? errors)
    {
        if (description is null)
        {
            return;
        }

        ReadOnlySpan<char> span = description.AsSpan().Trim();
        string? error = span.Length switch
        {
            > MaxDescriptionLength => $"Description cannot exceed {MaxDescriptionLength} characters.",
            _ when span.IndexOf('\0') >= 0 => "Description cannot contain null characters.",
            _ => null
        };

        if (error is not null)
        {
            AddError(ref errors, "description", error);
        }
    }

    private static void ValidateAssignee(string? assignee, ref Dictionary<string, List<string>>? errors)
    {
        ReadOnlySpan<char> span = assignee.AsSpan().Trim();

        string? error = span.Length switch
        {
            0 => "Assignee is required and cannot be empty or whitespace.",
            < MinAssigneeLength => $"Assignee must be at least {MinAssigneeLength} characters long.",
            > MaxAssigneeLength => $"Assignee cannot exceed {MaxAssigneeLength} characters.",
            _ when span.ContainsAny(ForbiddenSingleLineChars) => "Assignee contains invalid control or newline characters.",
            _ => null
        };

        if (error is not null)
        {
            AddError(ref errors, "assignee", error);
        }
    }

    private static void ValidateDueDate(DateTimeOffset? dueDate, ref Dictionary<string, List<string>>? errors)
    {
        string? error = dueDate switch
        {
            null => "DueDate is required.",
            { Year: < 2000 or > 2100 } => "DueDate must be a valid date between years 2000 and 2100.",
            _ => null
        };

        if (error is not null)
        {
            AddError(ref errors, "dueDate", error);
        }
    }

    private static void AddError(ref Dictionary<string, List<string>>? errors, string field, string message)
    {
        errors ??= new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (!errors.TryGetValue(field, out List<string>? list))
        {
            list = new List<string>(1);
            errors[field] = list;
        }
        list.Add(message);
    }

    private static Error BuildValidationError(Dictionary<string, List<string>> errors)
    {
        var finalized = new Dictionary<string, string[]>(errors.Count, StringComparer.Ordinal);
        foreach ((string key, List<string> messages) in errors)
        {
            finalized[key] = messages.ToArray();
        }

        return Error.Validation(
            "Task.Validation",
            "One or more validation errors occurred.",
            finalized);
    }
}
