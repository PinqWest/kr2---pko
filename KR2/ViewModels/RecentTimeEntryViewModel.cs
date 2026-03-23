namespace KR2.ViewModels;

public sealed class RecentTimeEntryViewModel
{
    public string TaskTitle { get; init; } = string.Empty;
    public string StartedAt { get; init; } = string.Empty;
    public string EndedAt { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
}
