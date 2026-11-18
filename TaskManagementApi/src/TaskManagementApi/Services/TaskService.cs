using Microsoft.EntityFrameworkCore;
using TaskManagementApi.Common.Results;
using TaskManagementApi.Contracts;
using TaskManagementApi.Domain;
using TaskManagementApi.Infrastructure.Data;
using TaskManagementApi.Validation;

namespace TaskManagementApi.Services;

/// <summary>
/// High-performance task application service using pre-compiled EF Core queries,
/// zero-allocation span trimming, direct struct projection, and bulk SQL delete.
/// </summary>
public sealed class TaskService(TaskDbContext db) : ITaskService
{
    // Pre-compiled query eliminates LINQ expression tree translation & cache lookup per request.
    private static readonly Func<TaskDbContext, Guid, CancellationToken, Task<TaskResponse>> GetByIdCompiledQuery =
        EF.CompileAsyncQuery((TaskDbContext context, Guid id, CancellationToken ct) =>
            context.Tasks
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => new TaskResponse(
                    t.Id,
                    t.Title,
                    t.Description,
                    t.Status,
                    t.Assignee,
                    t.DueDate,
                    t.CreatedAtUtc,
                    t.UpdatedAtUtc))
                .FirstOrDefault());

    public async Task<Result<TaskResponse>> CreateAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        Result<CreateTaskRequest> validation = TaskValidator.ValidateCreate(in request);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        var entity = new TaskItem
        {
            Id = Guid.CreateVersion7(),
            Title = TrimZeroAlloc(request.Title!),
            Description = NormalizeOptional(request.Description),
            Status = request.Status ?? TaskItemStatus.Pending,
            Assignee = TrimZeroAlloc(request.Assignee!),
            DueDate = request.DueDate!.Value.ToUniversalTime(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Tasks.Add(entity);
        await db.SaveChangesAsync(ct);

        return TaskResponse.FromEntity(entity);
    }

    public async Task<Result<PagedResponse<TaskResponse>>> ListAsync(
        string? status,
        string? assignee,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        Result<TaskFilterQuery> filterResult = TaskValidator.ValidateFilter(status, assignee, page, pageSize);
        if (!filterResult.TryGetValue(out TaskFilterQuery query))
        {
            return filterResult.Error;
        }

        IQueryable<TaskItem> baseQuery = db.Tasks.AsNoTracking();

        // Tuple pattern matching with modern switch expression to compose predicates
        baseQuery = (query.Status, query.Assignee) switch
        {
            ({ } s, { Length: > 0 } a) =>
                baseQuery.Where(t => t.Status == s && t.Assignee == a),
            ({ } s, _) =>
                baseQuery.Where(t => t.Status == s),
            (null, { Length: > 0 } a) =>
                baseQuery.Where(t => t.Assignee == a),
            _ => baseQuery
        };

        int totalCount = await baseQuery.CountAsync(ct);
        if (totalCount is 0)
        {
            return PagedResponse<TaskResponse>.Create(
                Array.Empty<TaskResponse>(),
                query.Page,
                query.PageSize,
                0);
        }

        int skip = (query.Page - 1) * query.PageSize;

        TaskResponse[] items = await baseQuery
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Id)
            .Skip(skip)
            .Take(query.PageSize)
            .Select(t => new TaskResponse(
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Assignee,
                t.DueDate,
                t.CreatedAtUtc,
                t.UpdatedAtUtc))
            .ToArrayAsync(ct);

        return PagedResponse<TaskResponse>.Create(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<Result<TaskResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return CreateNotFoundError(id);
        }

        TaskResponse response = await GetByIdCompiledQuery(db, id, ct);

        return response switch
        {
            { Id: var foundId } when foundId != Guid.Empty => Result<TaskResponse>.Success(response),
            _ => CreateNotFoundError(id)
        };
    }

    public async Task<Result<TaskResponse>> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        Result<UpdateTaskRequest> validation = TaskValidator.ValidateUpdate(in request);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        if (id == Guid.Empty)
        {
            return CreateNotFoundError(id);
        }

        TaskItem? entity = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null)
        {
            return CreateNotFoundError(id);
        }

        entity.Title = TrimZeroAlloc(request.Title!);
        entity.Description = NormalizeOptional(request.Description);
        entity.Status = request.Status!.Value;
        entity.Assignee = TrimZeroAlloc(request.Assignee!);
        entity.DueDate = request.DueDate!.Value.ToUniversalTime();
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return TaskResponse.FromEntity(entity);
    }

    public async Task<Result<TaskResponse>> UpdateStatusAsync(
        Guid id,
        UpdateTaskStatusRequest request,
        CancellationToken ct = default)
    {
        Result<TaskItemStatus> validation = TaskValidator.ValidateStatusUpdate(in request);
        if (!validation.TryGetValue(out TaskItemStatus newStatus))
        {
            return validation.Error;
        }

        if (id == Guid.Empty)
        {
            return CreateNotFoundError(id);
        }

        TaskItem? entity = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null)
        {
            return CreateNotFoundError(id);
        }

        entity.Status = newStatus;
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return TaskResponse.FromEntity(entity);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
        {
            return CreateNotFoundError(id);
        }

        // Single-roundtrip SQL DELETE without materializing or tracking the entity in memory
        int deletedCount = await db.Tasks
            .Where(t => t.Id == id)
            .ExecuteDeleteAsync(ct);

        return deletedCount switch
        {
            > 0 => Result.Success(),
            _ => CreateNotFoundError(id)
        };
    }

    /// <summary>
    /// Returns the original string reference if it has no leading/trailing whitespace,
    /// avoiding a duplicate string allocation on already-clean input.
    /// </summary>
    private static string TrimZeroAlloc(string value)
    {
        ReadOnlySpan<char> span = value.AsSpan().Trim();
        return span.Length == value.Length ? value : span.ToString();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        return span.Length switch
        {
            0 => null,
            var len when len == value.Length => value,
            _ => span.ToString()
        };
    }

    private static Error CreateNotFoundError(Guid id) =>
        Error.NotFound("Task.NotFound", $"Task with ID '{id}' was not found.");
}
