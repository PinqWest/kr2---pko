namespace TimeTracker.Core.Entities;

public sealed class TimeEntry
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public int DurationMinutes { get; set; }
    public string? Comment { get; set; }
    public bool IsManual { get; set; }
    public bool IsArchived { get; set; }

    public TaskItem? TaskItem { get; set; }
}
