using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using TaskManagementApi.Domain;
using TaskManagementApi.Endpoints;
using TaskManagementApi.Infrastructure.Data;
using TaskManagementApi.Serialization;
using TaskManagementApi.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// 1. Source-generated JSON serialization for minimal allocation and fast throughput
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<TaskItemStatus>());
});

// 2. RFC 9457 / RFC 7807 ProblemDetails support with pattern-matched exception mapping
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        Exception? exception = context.HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;
        if (exception is BadHttpRequestException badRequest)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.ProblemDetails.Status = StatusCodes.Status400BadRequest;
            context.ProblemDetails.Title = "Malformed or invalid request payload.";
            context.ProblemDetails.Detail = badRequest.Message;
            context.ProblemDetails.Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1";
            context.ProblemDetails.Extensions["errorCode"] = "Request.InvalidPayload";
        }
    };
});

// 3. DbContext pooling reuses TaskDbContext instances across requests (zero DbContext allocation per request)
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=tasks.db";

builder.Services.AddDbContextPool<TaskDbContext>(options =>
    options.UseSqlite(connectionString));

// 4. Register application services
builder.Services.AddScoped<ITaskService, TaskService>();

// 5. Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Task Management Minimal API (.NET 10)",
        Version = "v1",
        Description = "High-performance, low-allocation REST API for managing tasks using .NET 10 Minimal APIs, Result Pattern, Pattern Matching, ProblemDetails, and EF Core SQLite Migrations."
    });
});

WebApplication app = builder.Build();

// Apply pending EF Core migrations automatically on startup
using (IServiceScope scope = app.Services.CreateScope())
{
    TaskDbContext dbContext = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Task Management API v1");
    options.RoutePrefix = "swagger";
});

app.MapGet("/", () => TypedResults.Redirect("/swagger"))
    .ExcludeFromDescription();

app.MapTaskEndpoints();

app.Run();

// Expose Program class for WebApplicationFactory<Program> in integration tests
public partial class Program;
