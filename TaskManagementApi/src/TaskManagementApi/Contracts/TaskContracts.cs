using TaskManagementApi.Domain;

namespace TaskManagementApi.Contracts;

/// <summary>
/// Stack-allocated request payload for creating a new task.
/// </summary>
public readonly record struct CreateTaskRequest(
    string? Title,
    string? Description,
    TaskItemStatus? Status,
    string? Assignee,
    DateTimeOffset? DueDate);

/// <summary>
/// Stack-allocated request payload for updating an existing task.
/// </summary>
public readonly record struct UpdateTaskRequest(
    string? Title,
    string? Description,
    TaskItemStatus? Status,
    string? Assignee,
    DateTimeOffset? DueDate);

/// <summary>
/// Stack-allocated request payload for transitioning only a task's status.
/// </summary>
public readonly record struct UpdateTaskStatusRequest(
    TaskItemStatus? Status);

/// <summary>
/// Stack-allocated response DTO representing a task resource.
/// </summary>
public readonly record struct TaskResponse(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    string Assignee,
    DateTimeOffset DueDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static TaskResponse FromEntity(TaskItem entity) => new(
        entity.Id,
        entity.Title,
        entity.Description,
        entity.Status,
        entity.Assignee,
        entity.DueDate,
        entity.CreatedAtUtc,
        entity.UpdatedAtUtc);
}

/// <summary>
/// Stack-allocated paginated response envelope.
/// </summary>
public readonly record struct PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage)
{
    public static PagedResponse<T> Create(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        int totalPages = totalCount is 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResponse<T>(
            Items: items,
            Page: page,
            PageSize: pageSize,
            TotalCount: totalCount,
            TotalPages: totalPages,
            HasNextPage: page < totalPages,
            HasPreviousPage: page > 1 && totalPages > 0);
    }
}

/// <summary>
/// Validated stack-allocated filter and pagination query parameters.
/// </summary>
public readonly record struct TaskFilterQuery(
    TaskItemStatus? Status,
    string? Assignee,
    int Page = 1,
    int PageSize = 10);
