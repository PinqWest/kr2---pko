namespace TimeTracker.Core.Models;

public sealed class TimeReportDto
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public int TotalMinutes { get; init; }
    public IReadOnlyList<ReportAggregateItemDto> ByProjects { get; init; } = [];
    public IReadOnlyList<ReportAggregateItemDto> ByTasks { get; init; } = [];
    public IReadOnlyList<ReportAggregateItemDto> ByTags { get; init; } = [];
    public IReadOnlyList<ReportDaySummaryDto> ByDays { get; init; } = [];
    public IReadOnlyList<ReportEntryDto> Entries { get; init; } = [];
    public PlanFactDto PlanFact { get; init; } = new();
}

public sealed class ReportAggregateItemDto
{
    public string Name { get; init; } = string.Empty;
    public int Minutes { get; init; }
}

public sealed class ReportDaySummaryDto
{
    public DateOnly Day { get; init; }
    public int Minutes { get; init; }
}

public sealed class ReportEntryDto
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string TaskTitle { get; init; } = string.Empty;
    public string Tags { get; init; } = string.Empty;
    public int Minutes { get; init; }
}

public sealed class PlanFactDto
{
    public int DayGoalMinutes { get; init; }
    public int WeekGoalMinutes { get; init; }
    public int ActualMinutes { get; init; }
    public int DaysInPeriod { get; init; }
    public int WeeksInPeriod { get; init; }
    public double DayPlanCompletionPercent { get; init; }
    public double WeekPlanCompletionPercent { get; init; }
}
