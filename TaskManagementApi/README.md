# Task Management REST API (.NET 10 Minimal API)

A high-performance, low-allocation RESTful API for managing tasks built with **ASP.NET Core Minimal APIs** on **.NET 10 (C# 14)**.

Designed around **zero-allocation hot paths**, **C# pattern matching & modern `switch` expressions**, the **Result Pattern**, **RFC 9457 `ProblemDetails`**, **EF Core 10 SQLite with Migrations**, **OpenAPI / Swagger UI**, and comprehensive **Unit & Integration Test suites**.

---

## Table of Contents

1. [Project Structure](#1-project-structure)
2. [How to Run the Project](#2-how-to-run-the-project)
3. [API Endpoints & Quick Examples](#3-api-endpoints--quick-examples)
4. [Architectural & Technical Decisions](#4-architectural--technical-decisions)
5. [What I Would Do With More Time](#5-what-i-would-do-with-more-time)
6. [How AI Was Used (Asked, Accepted, and Corrected)](#6-how-ai-was-used-asked-accepted-and-corrected)

---

## 1. Project Structure

```text
TaskManagementApi/
├── TaskManagementApi.slnx
├── README.md
├── docs/
│   └── TaskManagementApi-Documentation.pdf      # Complete PDF feature & endpoint documentation
├── src/
│   └── TaskManagementApi/
│       ├── Common/Results/                      # Stack-allocated Result<T>, Result, and Error structs
│       ├── Contracts/                           # Stack-allocated request/response DTO structs
│       ├── Domain/                              # TaskItem entity & TaskItemStatus enum + span extensions
│       ├── Endpoints/                           # Minimal API route group & ProblemDetails mapping
│       ├── Infrastructure/
│       │   ├── Data/                            # Pooled TaskDbContext & Design-time factory
│       │   └── Migrations/                      # EF Core SQLite migrations (InitialCreate)
│       ├── Serialization/                       # System.Text.Json source-generated serializer context
│       ├── Services/                            # TaskService with EF.CompileAsyncQuery & ExecuteDeleteAsync
│       ├── Validation/                          # Span-based & SearchValues<char> zero-alloc validator
│       └── Program.cs                           # Application bootstrap, DbContextPool, Swagger, ProblemDetails
└── tests/
    ├── TaskManagementApi.UnitTests/             # 29 unit tests (validation, 0-byte alloc verify, Result, service)
    └── TaskManagementApi.IntegrationTests/      # 10 end-to-end HTTP integration tests via WebApplicationFactory
```

---

## 2. How to Run the Project

### Prerequisites

- **.NET 10 SDK** (`10.0.100` or newer; tested with `10.0.202`)

### Build the Solution

```bash
cd TaskManagementApi
dotnet build
```

### Run the API

```bash
dotnet run --project src/TaskManagementApi/TaskManagementApi.csproj
```

- On startup, the API automatically applies any pending EF Core SQLite migrations (`await dbContext.Database.MigrateAsync()`) and creates `tasks.db`.
- **Swagger UI** is available at: `http://localhost:5000/swagger` (or the port displayed in the console output; root `/` automatically redirects to `/swagger`).
- **OpenAPI JSON Specification**: `/swagger/v1/swagger.json`

### Apply or Inspect EF Core Migrations Manually (Optional)

```bash
cd src/TaskManagementApi
dotnet ef migrations list
dotnet ef database update
```

### Run Unit and Integration Tests

Run all **39 tests** (29 unit tests + 10 HTTP integration tests):

```bash
dotnet test
```

Or run each test suite individually:

```bash
# Run Unit Tests (29 tests)
dotnet test tests/TaskManagementApi.UnitTests/TaskManagementApi.UnitTests.csproj

# Run Integration Tests (10 tests)
dotnet test tests/TaskManagementApi.IntegrationTests/TaskManagementApi.IntegrationTests.csproj
```

---

## 3. API Endpoints & Quick Examples

Full request and response payloads (including error payloads) are documented in [`docs/TaskManagementApi-Documentation.pdf`](docs/TaskManagementApi-Documentation.pdf).

| HTTP Method | Route | Description | Success Status | Error Statuses |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/tasks` | Create a new task | `201 Created` | `400 Bad Request` |
| `GET` | `/api/tasks` | List tasks with `status` & `assignee` filters and pagination (`page`, `pageSize`) | `200 OK` | `400 Bad Request` |
| `GET` | `/api/tasks/{id}` | Retrieve a single task by UUID | `200 OK` | `404 Not Found` |
| `PUT` | `/api/tasks/{id}` | Full update of an existing task | `200 OK` | `400 Bad Request`, `404 Not Found` |
| `PATCH` | `/api/tasks/{id}/status` | Transition a task's status (`Pending`, `InProgress`, `Completed`) | `200 OK` | `400 Bad Request`, `404 Not Found` |
| `DELETE` | `/api/tasks/{id}` | Permanently delete a task by UUID | `204 No Content` | `404 Not Found` |

### Quick `curl` Example

```bash
# 1. Create a task
curl -i -X POST http://localhost:5000/api/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Implement zero-allocation Minimal API",
    "description": "Use readonly record structs and ReadOnlySpan<char>",
    "status": "Pending",
    "assignee": "Alice Henderson",
    "dueDate": "2026-12-15T18:00:00Z"
  }'

# 2. List tasks filtered by status and assignee with pagination
curl -s "http://localhost:5000/api/tasks?status=Pending&assignee=alice%20henderson&page=1&pageSize=10"
```

---

## 4. Architectural & Technical Decisions

### 1. Minimal API on .NET 10
- Endpoints are organized using `RouteGroupBuilder` (`app.MapGroup("/api/tasks")`) in `TaskEndpoints.cs`, eliminating the controller instantiation, action filter pipeline, and model-binding overhead of legacy MVC controllers.

### 2. Zero-Allocation & Low-Latency Optimizations
To execute endpoints in the shortest possible time and minimize GC pressure:
- **Stack-Allocated `readonly record struct` Types**:
  - `Result<T>`, `Result`, `Error`, `CreateTaskRequest`, `UpdateTaskRequest`, `UpdateTaskStatusRequest`, `TaskResponse`, `PagedResponse<T>`, and `TaskFilterQuery` are all value types (`readonly record struct`), avoiding heap wrapper allocations when returning results or mapping DTOs.
- **Zero-Allocation Hot-Path Validation (`TaskValidator`)**:
  - String trimming and length checks operate on `ReadOnlySpan<char>` (`title.AsSpan().Trim()`) without allocating intermediate substrings.
  - Control-character validation uses SIMD-vectorized `SearchValues<char>`.
  - Error dictionaries (`Dictionary<string, List<string>>? errors = null`) are lazily instantiated **only** when a validation rule fails. On valid requests (the production hot path), `TaskValidator.ValidateCreate` and `Result<T>.Match` allocate **0 bytes** on the managed heap (verified by unit test `ValidateCreate_OnValidHotPath_AllocatesZeroHeapBytes` using `GC.GetAllocatedBytesForCurrentThread()`).
- **Zero-Allocation String Normalization (`TrimZeroAlloc`)**:
  - When persisting strings, `TrimZeroAlloc` inspects `value.AsSpan().Trim()`. If `span.Length == value.Length`, the original string reference is reused directly without allocating a new string copy.
- **Compile-Time JSON Source Generation (`AppJsonSerializerContext`)**:
  - Registered at index `0` of `TypeInfoResolverChain` with `JsonStringEnumConverter<TaskItemStatus>`, eliminating runtime reflection metadata lookups during request deserialization and response serialization.
- **EF Core Performance Tuning**:
  - **`AddDbContextPool<TaskDbContext>`**: Pools and resets `TaskDbContext` instances across HTTP requests so no `DbContext` instance is allocated per request.
  - **`EF.CompileAsyncQuery`**: Pre-compiles the LINQ expression tree for `GetByIdAsync` once at startup and projects directly into `TaskResponse` (`readonly record struct`) with `AsNoTracking()`.
  - **`ExecuteDeleteAsync`**: `DELETE /api/tasks/{id}` executes a single SQL `DELETE FROM "Tasks" WHERE "Id" = @p0` command without querying or materializing the entity in memory first.
  - **Empty Page Short-Circuit**: When `CountAsync` returns `0` in `ListAsync`, the service immediately returns `Array.Empty<TaskResponse>()` without issuing a second `SELECT` query.

### 3. Pattern Matching & Modern Switch Expressions
Modern C# pattern matching is used across every layer:
- **Relational & Property Patterns** in `TaskValidator`:
  ```csharp
  string? error = span.Length switch
  {
      0 => "Title is required and cannot be empty or whitespace.",
      < MinTitleLength => $"Title must be at least {MinTitleLength} characters long.",
      > MaxTitleLength => $"Title cannot exceed {MaxTitleLength} characters.",
      _ when span.ContainsAny(ForbiddenSingleLineChars) => "Title contains invalid control or newline characters.",
      _ => null
  };
  ```
- **Tuple Pattern Matching** in `TaskService.ListAsync` to compose filtered `IQueryable<TaskItem>` predicates cleanly:
  ```csharp
  baseQuery = (query.Status, query.Assignee) switch
  {
      ({ } s, { Length: > 0 } a) => baseQuery.Where(t => t.Status == s && t.Assignee == a),
      ({ } s, _)                 => baseQuery.Where(t => t.Status == s),
      (null, { Length: > 0 } a)  => baseQuery.Where(t => t.Assignee == a),
      _                          => baseQuery
  };
  ```
- **Property Pattern Matching** in `Result<T>.Match` and `ProblemDetailsExtensions.ToProblemDetails`.

### 4. Result Pattern & RFC 9457 `ProblemDetails`
- Expected domain and validation failures never throw exceptions, avoiding stack-trace capture overhead.
- `ProblemDetailsExtensions.ToProblemDetails` maps `ErrorType` (`Validation` -> `400`, `NotFound` -> `404`, `Conflict` -> `409`, `Failure` -> `500`) to `TypedResults.ValidationProblem` or `TypedResults.Problem` with structured `errorCode` and `traceId` extensions.
- Global `AddProblemDetails` + `UseExceptionHandler` intercepts malformed JSON payloads (`BadHttpRequestException`) and converts them into `400 Bad Request` `ProblemDetails` responses.

### 5. SQLite with EF Core Migrations & Indexing
- Uses **UUIDv7** (`Guid.CreateVersion7()`) primary keys so inserts are monotonically ordered, minimizing B-tree page splits.
- Configures `DateTimeOffsetToBinaryConverter` so `DueDate`, `CreatedAtUtc`, and `UpdatedAtUtc` are stored as sortable 64-bit integers in SQLite, enabling index-backed `ORDER BY DueDate` in SQL.
- Configures `.UseCollation("NOCASE")` on `Assignee` and a composite index `IX_Tasks_Status_Assignee_DueDate`, allowing case-insensitive assignee filtering to hit the B-tree index directly with zero `.ToLower()` string allocations.

---

## 5. What I Would Do With More Time

1. **Keyset (Seek / Cursor) Pagination**:
   - Offset pagination (`Skip`/`Take`) works well for small-to-medium datasets, but for millions of rows I would add cursor-based pagination (`?afterDueDate=...&afterId=...` translating to `WHERE (DueDate, Id) > (@dueDate, @id)`) for $O(\log N)$ seek performance at any depth.
2. **Optimistic Concurrency Control (`ETag` / `If-Match`)**:
   - Add a concurrency token (`Version` / `RowVersion`) mapped to HTTP `ETag` and `If-Match` headers, returning `412 Precondition Failed` / `409 Conflict` via `ProblemDetails` on concurrent conflicting edits.
3. **Authentication & Authorization**:
   - Integrate JWT Bearer authentication with policy-based authorization so users can only modify or delete tasks assigned to their team or tenant.
4. **Rate Limiting & Output Caching**:
   - Add ASP.NET Core `AddRateLimiter` (fixed-window or token-bucket per IP/client) and `OutputCache` with tag-based eviction on task mutations.
5. **Observability & BenchmarkDotNet CI Suite**:
   - Add OpenTelemetry tracing/metrics, `/healthz` readiness probes, and a dedicated `BenchmarkDotNet` project with `[MemoryDiagnoser]` in CI to prevent memory allocation regressions over time.

---

## 6. How AI Was Used (Asked, Accepted, and Corrected)

### What Was Asked
1. Design and scaffold a `.NET 10` Minimal API solution (`TaskManagementApi`) with separate `UnitTests` and `IntegrationTests` projects.
2. Implement a stack-allocated Result pattern (`Result<T>`, `Result`, `Error`) and DTO contracts (`readonly record struct`) with `System.Text.Json` source generation.
3. Build a zero-allocation input validator (`TaskValidator`) using `ReadOnlySpan<char>`, `SearchValues<char>`, C# pattern matching, and modern `switch` expressions.
4. Configure EF Core SQLite with `AddDbContextPool`, `EF.CompileAsyncQuery`, `AsNoTracking()`, `ExecuteDeleteAsync`, and EF Core migrations.
5. Create Minimal API endpoints mapped to RFC 9457 `ProblemDetails` and documented via Swagger/OpenAPI.
6. Write unit and integration test suites and generate the PDF technical documentation.

### What Was Accepted
- **Stack-Allocated `readonly record struct` Architecture**: Accepted the design for `Result<T>`, `Error`, and all request/response DTOs, as well as the `Match` pattern-matching API.
- **Span & `SearchValues<char>` Validator**: Accepted the `ReadOnlySpan<char>` validation rules and lazy `Dictionary<string, List<string>>?` allocation strategy so valid requests incur zero heap allocations.
- **Minimal API Endpoint Group & `ProblemDetails` Mapping**: Accepted the `ProblemDetailsExtensions.ToProblemDetails` switch expression mapping `ErrorType` values to `TypedResults.ValidationProblem` and `TypedResults.Problem`.
- **`WebApplicationFactory<Program>` Integration Test Setup**: Accepted the isolated in-memory SQLite connection per test fixture (`TaskApiFactory`) verifying full HTTP status codes, headers, and `application/problem+json` payloads.

### What Was Corrected / Refined
1. **NuGet Package Version Pinning for `.NET 10` & Security Advisory Fix**:
   - *Issue*: Running `dotnet add package Microsoft.EntityFrameworkCore.Sqlite` without a version constraint resolved to EF Core `11.0.0` (targeting `net11.0`), which failed compatibility with `net10.0`. Additionally, transitive `Microsoft.OpenApi 2.3.0` from `Swashbuckle.AspNetCore` triggered NuGet vulnerability warning `NU1903`.
   - *Correction*: Pinned `Microsoft.EntityFrameworkCore.*` and `Microsoft.AspNetCore.Mvc.Testing` to `10.0.*` (`10.0.15`) and explicitly referenced `Microsoft.OpenApi` `2.13.1`, resulting in a clean build with `0 Warning(s)` and `0 Error(s)`.
2. **C# Compiler Static Lambda Delegate Caching in Zero-Allocation Unit Test**:
   - *Issue*: In `ValidateCreate_OnValidHotPath_AllocatesZeroHeapBytes`, warming up `ValidateCreate` and `Match` on one line and calling `result.Match(static _ => 200, static _ => 400)` on a second line inside the `GC.GetAllocatedBytesForCurrentThread()` block initially reported `128 bytes` allocated because the C# compiler generates a distinct lazy static delegate cache field (`<>9`) for each lexical lambda call site.
   - *Correction*: Encapsulated the validation + `Match` invocation inside a local static function (`ValidateAndMatch`) so the warm-up call initializes that call site's cached static delegates before measuring `GC.GetAllocatedBytesForCurrentThread()`, confirming `0 bytes` allocated on subsequent calls.
3. **SQLite `DateTimeOffset` Sorting & Case-Insensitive `Assignee` Index Optimization**:
   - *Issue*: The EF Core SQLite provider does not support native SQL `ORDER BY` on raw `DateTimeOffset` properties without a converter, and using `.ToLower()` for case-insensitive assignee filtering allocates strings and prevents standard index usage.
   - *Correction*: Added `DateTimeOffsetToBinaryConverter` in `TaskDbContext.OnModelCreating` (storing timestamps as sortable 64-bit integers) and configured `.UseCollation("NOCASE")` on the `Assignee` column alongside a composite index `IX_Tasks_Status_Assignee_DueDate`.
4. **PDF Documentation Pagination Cleanup**:
   - *Issue*: After generating the initial PDF documentation, visual inspection of the pages revealed a blank Page 3 caused by an explicit `<div class="page-break">` immediately following a table that filled the bottom of Page 2.
   - *Correction*: Removed hard page-break elements in favor of natural CSS pagination and regenerated the PDF, producing a clean 7-page document with no blank pages.
