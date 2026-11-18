using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskManagementApi.Contracts;
using TaskManagementApi.Domain;

namespace TaskManagementApi.Serialization;

/// <summary>
/// Compile-time source-generated System.Text.Json context for zero-reflection serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(TaskItemStatus))]
[JsonSerializable(typeof(CreateTaskRequest))]
[JsonSerializable(typeof(UpdateTaskRequest))]
[JsonSerializable(typeof(UpdateTaskStatusRequest))]
[JsonSerializable(typeof(TaskResponse))]
[JsonSerializable(typeof(IReadOnlyList<TaskResponse>))]
[JsonSerializable(typeof(TaskResponse[]))]
[JsonSerializable(typeof(PagedResponse<TaskResponse>))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(HttpValidationProblemDetails))]
[JsonSerializable(typeof(ValidationProblemDetails))]
public partial class AppJsonSerializerContext : JsonSerializerContext;
