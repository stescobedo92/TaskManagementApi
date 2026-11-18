namespace TaskManagementApi.Domain;

/// <summary>
/// Core domain entity representing a managed task.
/// Uses UUIDv7 (Guid.CreateVersion7) for time-ordered index locality in SQLite/relational B-trees.
/// </summary>
public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;
    public string Assignee { get; set; } = string.Empty;
    public DateTimeOffset DueDate { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
