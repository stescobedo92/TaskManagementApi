using TaskManagementApi.Common.Results;
using TaskManagementApi.Domain;

namespace TaskManagementApi.UnitTests;

public sealed class ResultAndPatternMatchingTests
{
    [Fact]
    public void ResultOfT_WhenSuccess_MatchesSuccessBranch()
    {
        Result<int> result = 42;

        string output = result.Match(
            onSuccess: static value => $"Value:{value}",
            onFailure: static error => $"Error:{error.Code}");

        Assert.True(result.IsSuccess);
        Assert.Equal("Value:42", output);
    }

    [Fact]
    public void ResultOfT_WhenFailure_MatchesFailureBranch()
    {
        Error notFound = Error.NotFound("Task.NotFound", "Task not found.");
        Result<int> result = notFound;

        string output = result.Match(
            onSuccess: static value => $"Value:{value}",
            onFailure: static error => $"{error.Type}:{error.Code}");

        Assert.True(result.IsFailure);
        Assert.Equal("NotFound:Task.NotFound", output);
    }

    [Fact]
    public void NonGenericResult_SupportsPatternMatchedEvaluation()
    {
        Result ok = Result.Success();
        Result fail = Error.Conflict("Task.Conflict", "Duplicate state.");

        Assert.Equal("OK", ok.Match(() => "OK", err => err.Code));
        Assert.Equal("Task.Conflict", fail.Match(() => "OK", err => err.Code));
    }

    [Fact]
    public void TaskItemStatusExtensions_ToFastString_And_IsDefinedStatus_WorkWithSwitchExpressions()
    {
        Assert.Equal("Pending", TaskItemStatus.Pending.ToFastString());
        Assert.Equal("InProgress", TaskItemStatus.InProgress.ToFastString());
        Assert.Equal("Completed", TaskItemStatus.Completed.ToFastString());
        Assert.Equal("Unknown", ((TaskItemStatus)255).ToFastString());

        Assert.True(TaskItemStatus.Pending.IsDefinedStatus());
        Assert.True(TaskItemStatus.InProgress.IsDefinedStatus());
        Assert.True(TaskItemStatus.Completed.IsDefinedStatus());
        Assert.False(((TaskItemStatus)99).IsDefinedStatus());
    }
}
