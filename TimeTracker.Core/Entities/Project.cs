namespace TimeTracker.Core.Entities;

public sealed class Project
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
