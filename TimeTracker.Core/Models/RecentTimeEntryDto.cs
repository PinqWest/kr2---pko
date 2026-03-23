namespace TimeTracker.Core.Models;

public sealed class RecentTimeEntryDto
{
    public int TimeEntryId { get; init; }
    public int TaskItemId { get; init; }
    public string TaskTitle { get; init; } = string.Empty;
    public DateTime StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public int DurationMinutes { get; init; }
    public string? Comment { get; init; }
    public bool IsArchived { get; init; }
}
