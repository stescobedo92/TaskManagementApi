using Microsoft.AspNetCore.Mvc;
using TaskManagementApi.Common.Results;
using TaskManagementApi.Contracts;
using TaskManagementApi.Services;

namespace TaskManagementApi.Endpoints;

/// <summary>
/// Minimal API endpoint definitions for the /api/tasks resource group.
/// </summary>
public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/tasks")
            .WithTags("Tasks");

        group.MapPost("/", CreateTaskAsync)
            .WithName("CreateTask")
            .WithSummary("Create a new task")
            .WithDescription("Creates a new task with title, description, status (Pending, InProgress, Completed), assignee, and due date.")
            .Produces<TaskResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", ListTasksAsync)
            .WithName("ListTasks")
            .WithSummary("List tasks with optional filtering and pagination")
            .WithDescription("Returns a paginated list of tasks filtered optionally by status (Pending, InProgress, Completed) and assignee.")
            .Produces<PagedResponse<TaskResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetTaskByIdAsync)
            .WithName("GetTaskById")
            .WithSummary("Get a single task by ID")
            .WithDescription("Retrieves a task by its unique UUIDv7 identifier.")
            .Produces<TaskResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateTaskAsync)
            .WithName("UpdateTask")
            .WithSummary("Update an existing task")
            .WithDescription("Updates all mutable properties (title, description, status, assignee, due date) of an existing task.")
            .Produces<TaskResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/status", UpdateTaskStatusAsync)
            .WithName("UpdateTaskStatus")
            .WithSummary("Update only the status of an existing task")
            .WithDescription("Transitions a task's status between Pending, InProgress, and Completed.")
            .Produces<TaskResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteTaskAsync)
            .WithName("DeleteTask")
            .WithSummary("Delete a task by ID")
            .WithDescription("Permanently removes a task by its unique identifier.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> CreateTaskAsync(
        [FromBody] CreateTaskRequest request,
        ITaskService taskService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        Result<TaskResponse> result = await taskService.CreateAsync(request, ct);

        return result.Match(
            onSuccess: task => TypedResults.Created($"/api/tasks/{task.Id}", task),
            onFailure: error => error.ToProblemDetails(httpContext));
    }

    private static async Task<IResult> ListTasksAsync(
        [FromQuery] string? status,
        [FromQuery] string? assignee,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ITaskService taskService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        Result<PagedResponse<TaskResponse>> result = await taskService.ListAsync(
            status: status,
            assignee: assignee,
            page: page ?? 1,
            pageSize: pageSize ?? 10,
            ct: ct);

        return result.Match(
            onSuccess: paged => TypedResults.Ok(paged),
            onFailure: error => error.ToProblemDetails(httpContext));
    }

    private static async Task<IResult> GetTaskByIdAsync(
        Guid id,
        ITaskService taskService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        Result<TaskResponse> result = await taskService.GetByIdAsync(id, ct);

        return result.Match(
            onSuccess: task => TypedResults.Ok(task),
            onFailure: error => error.ToProblemDetails(httpContext));
    }

    private static async Task<IResult> UpdateTaskAsync(
        Guid id,
        [FromBody] UpdateTaskRequest request,
        ITaskService taskService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        Result<TaskResponse> result = await taskService.UpdateAsync(id, request, ct);

        return result.Match(
            onSuccess: task => TypedResults.Ok(task),
            onFailure: error => error.ToProblemDetails(httpContext));
    }

    private static async Task<IResult> UpdateTaskStatusAsync(
        Guid id,
        [FromBody] UpdateTaskStatusRequest request,
        ITaskService taskService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        Result<TaskResponse> result = await taskService.UpdateStatusAsync(id, request, ct);

        return result.Match(
            onSuccess: task => TypedResults.Ok(task),
            onFailure: error => error.ToProblemDetails(httpContext));
    }

    private static async Task<IResult> DeleteTaskAsync(
        Guid id,
        ITaskService taskService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        Result result = await taskService.DeleteAsync(id, ct);

        return result.Match(
            onSuccess: static () => TypedResults.NoContent(),
            onFailure: error => error.ToProblemDetails(httpContext));
    }
}
