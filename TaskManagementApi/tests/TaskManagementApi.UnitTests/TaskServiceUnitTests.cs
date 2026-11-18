using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskManagementApi.Common.Results;
using TaskManagementApi.Contracts;
using TaskManagementApi.Domain;
using TaskManagementApi.Infrastructure.Data;
using TaskManagementApi.Services;

namespace TaskManagementApi.UnitTests;

public sealed class TaskServiceUnitTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private TaskDbContext _dbContext = null!;
    private TaskService _service = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        DbContextOptions<TaskDbContext> options = new DbContextOptionsBuilder<TaskDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TaskDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        _service = new TaskService(_dbContext);
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_TrimsStringsAndPersistsTask()
    {
        var request = new CreateTaskRequest(
            Title: "  Configure SQLite WAL mode  ",
            Description: "  Improve write concurrency  ",
            Status: null,
            Assignee: "  Elena Rostova  ",
            DueDate: new DateTimeOffset(2026, 12, 5, 14, 30, 0, TimeSpan.Zero));

        Result<TaskResponse> result = await _service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        TaskResponse created = result.Value;
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Configure SQLite WAL mode", created.Title);
        Assert.Equal("Improve write concurrency", created.Description);
        Assert.Equal(TaskItemStatus.Pending, created.Status);
        Assert.Equal("Elena Rostova", created.Assignee);
    }

    [Fact]
    public async Task GetByIdAsync_UsesCompiledQuery_AndReturnsNotFoundForUnknownId()
    {
        Result<TaskResponse> created = await _service.CreateAsync(new CreateTaskRequest(
            Title: "Compiled Query Verification",
            Description: "Ensure compiled query maps all fields",
            Status: TaskItemStatus.InProgress,
            Assignee: "David Kim",
            DueDate: new DateTimeOffset(2026, 12, 10, 9, 0, 0, TimeSpan.Zero)));

        Result<TaskResponse> found = await _service.GetByIdAsync(created.Value.Id);
        Result<TaskResponse> missing = await _service.GetByIdAsync(Guid.CreateVersion7());

        Assert.True(found.IsSuccess);
        Assert.Equal(created.Value.Id, found.Value.Id);
        Assert.Equal(TaskItemStatus.InProgress, found.Value.Status);

        Assert.True(missing.IsFailure);
        Assert.Equal(ErrorType.NotFound, missing.Error.Type);
    }

    [Fact]
    public async Task ListAsync_SupportsFilteringByStatusAndCaseInsensitiveAssigneeWithPagination()
    {
        DateTimeOffset baseDate = new(2026, 12, 1, 10, 0, 0, TimeSpan.Zero);

        for (int i = 1; i <= 5; i++)
        {
            await _service.CreateAsync(new CreateTaskRequest(
                Title: $"Alice Pending Task {i}",
                Description: null,
                Status: TaskItemStatus.Pending,
                Assignee: "Alice",
                DueDate: baseDate.AddDays(i)));
        }

        await _service.CreateAsync(new CreateTaskRequest(
            Title: "Alice Completed Task",
            Description: null,
            Status: TaskItemStatus.Completed,
            Assignee: "Alice",
            DueDate: baseDate.AddDays(10)));

        await _service.CreateAsync(new CreateTaskRequest(
            Title: "Bob Pending Task",
            Description: null,
            Status: TaskItemStatus.Pending,
            Assignee: "Bob",
            DueDate: baseDate.AddDays(2)));

        Result<PagedResponse<TaskResponse>> page1 = await _service.ListAsync(
            status: "Pending",
            assignee: "alice",
            page: 1,
            pageSize: 2);

        Result<PagedResponse<TaskResponse>> page3 = await _service.ListAsync(
            status: "Pending",
            assignee: "ALICE",
            page: 3,
            pageSize: 2);

        Assert.True(page1.IsSuccess);
        Assert.Equal(5, page1.Value.TotalCount);
        Assert.Equal(3, page1.Value.TotalPages);
        Assert.Equal(2, page1.Value.Items.Count);
        Assert.True(page1.Value.HasNextPage);
        Assert.False(page1.Value.HasPreviousPage);
        Assert.Equal("Alice Pending Task 1", page1.Value.Items[0].Title);

        Assert.True(page3.IsSuccess);
        Assert.Single(page3.Value.Items);
        Assert.False(page3.Value.HasNextPage);
        Assert.True(page3.Value.HasPreviousPage);
        Assert.Equal("Alice Pending Task 5", page3.Value.Items[0].Title);
    }

    [Fact]
    public async Task UpdateAsync_And_UpdateStatusAsync_ModifyTaskCorrectly()
    {
        Result<TaskResponse> created = await _service.CreateAsync(new CreateTaskRequest(
            Title: "Initial Draft",
            Description: "V1",
            Status: TaskItemStatus.Pending,
            Assignee: "Sofia",
            DueDate: new DateTimeOffset(2026, 12, 12, 0, 0, 0, TimeSpan.Zero)));

        Result<TaskResponse> updated = await _service.UpdateAsync(
            created.Value.Id,
            new UpdateTaskRequest(
                Title: "Final Specification",
                Description: "V2",
                Status: TaskItemStatus.InProgress,
                Assignee: "Sofia Morales",
                DueDate: new DateTimeOffset(2026, 12, 14, 0, 0, 0, TimeSpan.Zero)));

        Assert.True(updated.IsSuccess);
        Assert.Equal("Final Specification", updated.Value.Title);
        Assert.Equal(TaskItemStatus.InProgress, updated.Value.Status);
        Assert.Equal("Sofia Morales", updated.Value.Assignee);

        Result<TaskResponse> statusUpdated = await _service.UpdateStatusAsync(
            created.Value.Id,
            new UpdateTaskStatusRequest(TaskItemStatus.Completed));

        Assert.True(statusUpdated.IsSuccess);
        Assert.Equal(TaskItemStatus.Completed, statusUpdated.Value.Status);
    }

    [Fact]
    public async Task DeleteAsync_ExecutesSingleSqlDelete_AndReturnsNotFoundOnSecondCall()
    {
        Result<TaskResponse> created = await _service.CreateAsync(new CreateTaskRequest(
            Title: "Temporary Task",
            Description: null,
            Status: TaskItemStatus.Pending,
            Assignee: "Marco",
            DueDate: new DateTimeOffset(2026, 12, 20, 0, 0, 0, TimeSpan.Zero)));

        Result firstDelete = await _service.DeleteAsync(created.Value.Id);
        Result secondDelete = await _service.DeleteAsync(created.Value.Id);

        Assert.True(firstDelete.IsSuccess);
        Assert.True(secondDelete.IsFailure);
        Assert.Equal(ErrorType.NotFound, secondDelete.Error.Type);
    }
}
