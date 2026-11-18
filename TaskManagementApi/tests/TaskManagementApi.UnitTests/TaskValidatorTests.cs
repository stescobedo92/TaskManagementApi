using TaskManagementApi.Common.Results;
using TaskManagementApi.Contracts;
using TaskManagementApi.Domain;
using TaskManagementApi.Validation;

namespace TaskManagementApi.UnitTests;

public sealed class TaskValidatorTests
{
    [Fact]
    public void ValidateCreate_WithValidRequest_ReturnsSuccess()
    {
        var request = new CreateTaskRequest(
            Title: "Implement zero-allocation Minimal API",
            Description: "Use readonly record structs and ReadOnlySpan<char>",
            Status: TaskItemStatus.InProgress,
            Assignee: "Alice Henderson",
            DueDate: new DateTimeOffset(2026, 12, 15, 18, 0, 0, TimeSpan.Zero));

        Result<CreateTaskRequest> result = TaskValidator.ValidateCreate(in request);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(ErrorType.None, result.Error.Type);
    }

    [Fact]
    public void ValidateCreate_OnValidHotPath_AllocatesZeroHeapBytes()
    {
        var request = new CreateTaskRequest(
            Title: "Benchmark zero-allocation validation",
            Description: "Verify no dictionary or string allocations occur on valid input",
            Status: TaskItemStatus.Pending,
            Assignee: "Bob Martin",
            DueDate: new DateTimeOffset(2026, 12, 31, 12, 0, 0, TimeSpan.Zero));

        static int ValidateAndMatch(in CreateTaskRequest req)
        {
            Result<CreateTaskRequest> res = TaskValidator.ValidateCreate(in req);
            return res.Match(
                onSuccess: static _ => 200,
                onFailure: static _ => 400);
        }

        // Warm up JIT and call-site static lambda delegate cache
        _ = ValidateAndMatch(in request);

        long before = GC.GetAllocatedBytesForCurrentThread();
        int matchedCode = ValidateAndMatch(in request);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(200, matchedCode);
        Assert.Equal(0, after - before);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("Bad\nTitle")]
    public void ValidateCreate_WithInvalidTitle_ReturnsValidationError(string? invalidTitle)
    {
        var request = new CreateTaskRequest(
            Title: invalidTitle,
            Description: "Valid description",
            Status: TaskItemStatus.Pending,
            Assignee: "Alice",
            DueDate: new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));

        Result<CreateTaskRequest> result = TaskValidator.ValidateCreate(in request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.NotNull(result.Error.ValidationErrors);
        Assert.True(result.Error.ValidationErrors!.ContainsKey("title"));
    }

    [Fact]
    public void ValidateCreate_WithMultipleInvalidFields_AggregatesAllFieldErrors()
    {
        var request = new CreateTaskRequest(
            Title: "A",
            Description: new string('x', TaskValidator.MaxDescriptionLength + 1),
            Status: (TaskItemStatus)99,
            Assignee: "",
            DueDate: null);

        Result<CreateTaskRequest> result = TaskValidator.ValidateCreate(in request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        IReadOnlyDictionary<string, string[]> errors = result.Error.ValidationErrors!;
        Assert.True(errors.ContainsKey("title"));
        Assert.True(errors.ContainsKey("description"));
        Assert.True(errors.ContainsKey("status"));
        Assert.True(errors.ContainsKey("assignee"));
        Assert.True(errors.ContainsKey("dueDate"));
    }

    [Fact]
    public void ValidateUpdate_WhenStatusIsMissing_ReturnsValidationError()
    {
        var request = new UpdateTaskRequest(
            Title: "Updated Task Title",
            Description: "Updated description",
            Status: null,
            Assignee: "Carlos",
            DueDate: new DateTimeOffset(2026, 12, 10, 0, 0, 0, TimeSpan.Zero));

        Result<UpdateTaskRequest> result = TaskValidator.ValidateUpdate(in request);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.True(result.Error.ValidationErrors!.ContainsKey("status"));
    }

    [Theory]
    [InlineData("Pending", TaskItemStatus.Pending)]
    [InlineData("inprogress", TaskItemStatus.InProgress)]
    [InlineData("Completed", TaskItemStatus.Completed)]
    [InlineData("Pendiente", TaskItemStatus.Pending)]
    [InlineData("EnCurso", TaskItemStatus.InProgress)]
    [InlineData("Hecha", TaskItemStatus.Completed)]
    public void ValidateFilter_WithSupportedStatusStrings_ParsesCorrectly(string rawStatus, TaskItemStatus expected)
    {
        Result<TaskFilterQuery> result = TaskValidator.ValidateFilter(rawStatus, "  Alice  ", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetValue(out TaskFilterQuery query));
        Assert.Equal(expected, query.Status);
        Assert.Equal("Alice", query.Assignee);
        Assert.Equal(1, query.Page);
        Assert.Equal(20, query.PageSize);
    }

    [Theory]
    [InlineData("InvalidState", 1, 10, "status")]
    [InlineData("Pending", 0, 10, "page")]
    [InlineData("Pending", -2, 10, "page")]
    [InlineData("Pending", 1, 0, "pageSize")]
    [InlineData("Pending", 1, 101, "pageSize")]
    public void ValidateFilter_WithInvalidParameters_ReturnsExpectedFieldError(
        string? rawStatus,
        int page,
        int pageSize,
        string expectedErrorKey)
    {
        Result<TaskFilterQuery> result = TaskValidator.ValidateFilter(rawStatus, null, page, pageSize);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.True(result.Error.ValidationErrors!.ContainsKey(expectedErrorKey));
    }
}
