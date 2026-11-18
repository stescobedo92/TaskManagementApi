using TaskManagementApi.Common.Results;
using TaskManagementApi.Contracts;

namespace TaskManagementApi.Services;

public interface ITaskService
{
    Task<Result<TaskResponse>> CreateAsync(CreateTaskRequest request, CancellationToken ct = default);
    Task<Result<PagedResponse<TaskResponse>>> ListAsync(string? status, string? assignee, int page, int pageSize, CancellationToken ct = default);
    Task<Result<TaskResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<TaskResponse>> UpdateAsync(Guid id, UpdateTaskRequest request, CancellationToken ct = default);
    Task<Result<TaskResponse>> UpdateStatusAsync(Guid id, UpdateTaskStatusRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
