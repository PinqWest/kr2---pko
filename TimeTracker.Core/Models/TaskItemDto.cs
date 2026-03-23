using TimeTracker.Core.Enums;

namespace TimeTracker.Core.Models;

public sealed class TaskItemDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public WorkTaskStatus Status { get; init; }
}
