using TimeTracker.Core.Enums;

namespace TimeTracker.Core.Entities;

public sealed class TaskItem
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int ProjectId { get; set; }
    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.New;

    public Project? Project { get; set; }
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public ICollection<TaskItemTag> TaskItemTags { get; set; } = new List<TaskItemTag>();
}
