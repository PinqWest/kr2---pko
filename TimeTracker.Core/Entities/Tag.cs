namespace TimeTracker.Core.Entities;

public sealed class Tag
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<TaskItemTag> TaskItemTags { get; set; } = new List<TaskItemTag>();
}
