namespace TimeTracker.Core.Entities;

public sealed class TaskItemTag
{
    public int TaskItemId { get; set; }
    public int TagId { get; set; }

    public TaskItem? TaskItem { get; set; }
    public Tag? Tag { get; set; }
}
