using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using TaskManagementApi.Contracts;
using TaskManagementApi.Domain;

namespace TaskManagementApi.IntegrationTests;

public sealed class TaskEndpointsIntegrationTests : IClassFixture<TaskApiFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<TaskItemStatus>() }
    };

    public TaskEndpointsIntegrationTests(TaskApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateTask_WithValidPayload_Returns201CreatedWithLocationAndBody()
    {
        var request = new CreateTaskRequest(
            Title: "Build .NET 10 Minimal API",
            Description: "Implement zero-allocation Result pattern and ProblemDetails",
            Status: TaskItemStatus.Pending,
            Assignee: "Alice Engineer",
            DueDate: new DateTimeOffset(2026, 12, 20, 15, 0, 0, TimeSpan.Zero));

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/tasks", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        TaskResponse created = await response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal($"/api/tasks/{created.Id}", response.Headers.Location!.OriginalString);
        Assert.Equal("Build .NET 10 Minimal API", created.Title);
        Assert.Equal(TaskItemStatus.Pending, created.Status);
        Assert.Equal("Alice Engineer", created.Assignee);
    }

    [Fact]
    public async Task CreateTask_WithInvalidPayload_Returns400ValidationProblemDetails()
    {
        var invalidRequest = new CreateTaskRequest(
            Title: "Hi",
            Description: "Valid description",
            Status: TaskItemStatus.Pending,
            Assignee: "",
            DueDate: null);

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/tasks", invalidRequest, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.True(problem.Errors.ContainsKey("title"));
        Assert.True(problem.Errors.ContainsKey("assignee"));
        Assert.True(problem.Errors.ContainsKey("dueDate"));
    }

    [Fact]
    public async Task CreateTask_WithMalformedJsonEnum_Returns400ProblemDetails()
    {
        using var content = new StringContent(
            """{"title":"Valid Title","status":"NotAValidStatus","assignee":"Alice","dueDate":"2026-12-20T00:00:00Z"}""",
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response = await _client.PostAsync("/api/tasks", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
    }

    [Fact]
    public async Task GetTaskById_WhenExists_Returns200Ok_AndWhenMissing_Returns404ProblemDetails()
    {
        var request = new CreateTaskRequest(
            Title: "Verify GetTaskById endpoint",
            Description: "Fetch via pre-compiled EF query",
            Status: TaskItemStatus.InProgress,
            Assignee: "Bruno Silva",
            DueDate: new DateTimeOffset(2026, 12, 22, 10, 0, 0, TimeSpan.Zero));

        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/tasks", request, JsonOptions);
        TaskResponse created = await createResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);

        HttpResponseMessage getResponse = await _client.GetAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        TaskResponse fetched = await getResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Verify GetTaskById endpoint", fetched.Title);
        Assert.Equal(TaskItemStatus.InProgress, fetched.Status);

        Guid nonExistentId = Guid.CreateVersion7();
        HttpResponseMessage notFoundResponse = await _client.GetAsync($"/api/tasks/{nonExistentId}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);
        Assert.Equal("application/problem+json", notFoundResponse.Content.Headers.ContentType?.MediaType);

        ProblemDetails? problem = await notFoundResponse.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal(404, problem!.Status);
        Assert.Equal("Resource Not Found", problem.Title);
    }

    [Fact]
    public async Task ListTasks_SupportsStatusAndAssigneeFiltersWithPagination()
    {
        string uniqueAssignee = $"Clara_{Guid.NewGuid():N}";
        DateTimeOffset baseDate = new(2026, 12, 1, 8, 0, 0, TimeSpan.Zero);

        for (int i = 1; i <= 3; i++)
        {
            await _client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest(
                Title: $"Clara InProgress {i}",
                Description: $"Task #{i}",
                Status: TaskItemStatus.InProgress,
                Assignee: uniqueAssignee,
                DueDate: baseDate.AddDays(i)), JsonOptions);
        }

        await _client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest(
            Title: "Clara Completed",
            Description: "Finished task",
            Status: TaskItemStatus.Completed,
            Assignee: uniqueAssignee,
            DueDate: baseDate.AddDays(10)), JsonOptions);

        // Page 1 with pageSize=2, filtered by status=InProgress and case-insensitive assignee
        HttpResponseMessage page1Response = await _client.GetAsync(
            $"/api/tasks?status=InProgress&assignee={uniqueAssignee.ToLowerInvariant()}&page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, page1Response.StatusCode);
        PagedResponse<TaskResponse> page1 = await page1Response.Content.ReadFromJsonAsync<PagedResponse<TaskResponse>>(JsonOptions);

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);
        Assert.All(page1.Items, item =>
        {
            Assert.Equal(TaskItemStatus.InProgress, item.Status);
            Assert.Equal(uniqueAssignee, item.Assignee);
        });

        // Page 2 with pageSize=2
        HttpResponseMessage page2Response = await _client.GetAsync(
            $"/api/tasks?status=InProgress&assignee={uniqueAssignee}&page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, page2Response.StatusCode);
        PagedResponse<TaskResponse> page2 = await page2Response.Content.ReadFromJsonAsync<PagedResponse<TaskResponse>>(JsonOptions);

        Assert.Single(page2.Items);
        Assert.False(page2.HasNextPage);
        Assert.True(page2.HasPreviousPage);
        Assert.Equal("Clara InProgress 3", page2.Items[0].Title);
    }

    [Fact]
    public async Task ListTasks_WithInvalidFilterOrPagination_Returns400ValidationProblemDetails()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/tasks?status=UnknownStatus&page=0&pageSize=250");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ValidationProblemDetails? problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);

        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.True(problem.Errors.ContainsKey("status"));
        Assert.True(problem.Errors.ContainsKey("page"));
        Assert.True(problem.Errors.ContainsKey("pageSize"));
    }

    [Fact]
    public async Task UpdateTask_ModifiesAllFields_AndReturns404WhenTaskDoesNotExist()
    {
        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest(
            Title: "Original Title",
            Description: "Original Description",
            Status: TaskItemStatus.Pending,
            Assignee: "Daniel",
            DueDate: new DateTimeOffset(2026, 12, 10, 0, 0, 0, TimeSpan.Zero)), JsonOptions);

        TaskResponse created = await createResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);

        var updateRequest = new UpdateTaskRequest(
            Title: "Updated Title",
            Description: "Updated Description",
            Status: TaskItemStatus.Completed,
            Assignee: "Daniel Craig",
            DueDate: new DateTimeOffset(2026, 12, 15, 0, 0, 0, TimeSpan.Zero));

        HttpResponseMessage updateResponse = await _client.PutAsJsonAsync($"/api/tasks/{created.Id}", updateRequest, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        TaskResponse updated = await updateResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        Assert.Equal("Updated Title", updated.Title);
        Assert.Equal("Updated Description", updated.Description);
        Assert.Equal(TaskItemStatus.Completed, updated.Status);
        Assert.Equal("Daniel Craig", updated.Assignee);

        HttpResponseMessage missingUpdate = await _client.PutAsJsonAsync(
            $"/api/tasks/{Guid.CreateVersion7()}",
            updateRequest,
            JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
    }

    [Fact]
    public async Task UpdateTaskStatus_TransitionsStatus_AndValidatesPayload()
    {
        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest(
            Title: "Status Transition Task",
            Description: null,
            Status: TaskItemStatus.Pending,
            Assignee: "Eva",
            DueDate: new DateTimeOffset(2026, 12, 18, 0, 0, 0, TimeSpan.Zero)), JsonOptions);

        TaskResponse created = await createResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);

        HttpResponseMessage patchResponse = await _client.PatchAsJsonAsync(
            $"/api/tasks/{created.Id}/status",
            new UpdateTaskStatusRequest(TaskItemStatus.Completed),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        TaskResponse patched = await patchResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);
        Assert.Equal(TaskItemStatus.Completed, patched.Status);

        HttpResponseMessage invalidPatch = await _client.PatchAsJsonAsync(
            $"/api/tasks/{created.Id}/status",
            new UpdateTaskStatusRequest(null),
            JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPatch.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_RemovesTask_Returns204NoContent_And404OnSubsequentGetOrDelete()
    {
        HttpResponseMessage createResponse = await _client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest(
            Title: "Task to Delete",
            Description: "Will be deleted",
            Status: TaskItemStatus.Pending,
            Assignee: "Fernando",
            DueDate: new DateTimeOffset(2026, 12, 25, 0, 0, 0, TimeSpan.Zero)), JsonOptions);

        TaskResponse created = await createResponse.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions);

        HttpResponseMessage deleteResponse = await _client.DeleteAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        HttpResponseMessage getAfterDelete = await _client.GetAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);

        HttpResponseMessage secondDelete = await _client.DeleteAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, secondDelete.StatusCode);
    }

    [Fact]
    public async Task SwaggerJsonEndpoint_Returns200OkWithDocumentedTaskPaths()
    {
        HttpResponseMessage response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string openApiJson = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/tasks", openApiJson);
        Assert.Contains("Task Management Minimal API (.NET 10)", openApiJson);
    }
}
