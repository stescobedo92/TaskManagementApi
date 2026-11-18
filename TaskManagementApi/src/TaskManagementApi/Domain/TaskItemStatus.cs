using System.Text.Json.Serialization;

namespace TaskManagementApi.Domain;

/// <summary>
/// Represents the lifecycle state of a task.
/// Uses the generic JsonStringEnumConverter for zero-reflection source-generated JSON support.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TaskItemStatus>))]
public enum TaskItemStatus : byte
{
    Pending = 0,
    InProgress = 1,
    Completed = 2
}

public static class TaskItemStatusExtensions
{
    /// <summary>
    /// Verifies whether the enum value is a valid defined status using C# pattern matching.
    /// </summary>
    public static bool IsDefinedStatus(this TaskItemStatus status) =>
        status is TaskItemStatus.Pending or TaskItemStatus.InProgress or TaskItemStatus.Completed;

    /// <summary>
    /// Maps a status value to a pre-interned constant string literal with zero heap allocation.
    /// </summary>
    public static string ToFastString(this TaskItemStatus status) => status switch
    {
        TaskItemStatus.Pending => "Pending",
        TaskItemStatus.InProgress => "InProgress",
        TaskItemStatus.Completed => "Completed",
        _ => "Unknown"
    };

    /// <summary>
    /// Zero-allocation parser using ReadOnlySpan&lt;char&gt; and modern pattern matching.
    /// Supports English names, numeric values, and Spanish equivalents.
    /// </summary>
    public static bool TryParseSpan(ReadOnlySpan<char> input, out TaskItemStatus status)
    {
        ReadOnlySpan<char> trimmed = input.Trim();

        if (trimmed.IsEmpty)
        {
            status = default;
            return false;
        }

        if (trimmed.Equals("Pending", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Pendiente", StringComparison.OrdinalIgnoreCase) ||
            trimmed is ['0'])
        {
            status = TaskItemStatus.Pending;
            return true;
        }

        if (trimmed.Equals("InProgress", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("In_Progress", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("In-Progress", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("EnCurso", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("En Curso", StringComparison.OrdinalIgnoreCase) ||
            trimmed is ['1'])
        {
            status = TaskItemStatus.InProgress;
            return true;
        }

        if (trimmed.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("Hecha", StringComparison.OrdinalIgnoreCase) ||
            trimmed is ['2'])
        {
            status = TaskItemStatus.Completed;
            return true;
        }

        status = default;
        return false;
    }
}
