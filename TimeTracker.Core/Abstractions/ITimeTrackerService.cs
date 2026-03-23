using TimeTracker.Core.Enums;
using TimeTracker.Core.Models;

namespace TimeTracker.Core.Abstractions;

public interface ITimeTrackerService
{
    IReadOnlyList<TaskItemDto> GetTasks();
    int CreateTask(string taskTitle);
    void UpdateTaskStatus(int taskItemId, WorkTaskStatus status);

    void StartTask(int taskItemId, string? comment);
    void PauseActiveTask(string? comment);
    void ResumeTask(int taskItemId, string? comment);
    void StopActiveTask(string? comment);

    IReadOnlyList<RecentTimeEntryDto> GetRecentEntries(int count, bool onlyToday);
    IReadOnlyList<RecentTimeEntryDto> GetAllEntries(bool onlyToday);
    void UpdateTimeEntry(int timeEntryId, DateTime startAt, DateTime? endAt, string? comment);
    void ArchiveTimeEntry(int timeEntryId);
    TimeReportDto GetPeriodReport(DateTime startUtcInclusive, DateTime endUtcExclusive, int dayGoalMinutes, int weekGoalMinutes);
    void ExportPeriodReportCsv(TimeReportDto report, string filePath);
    void ExportPeriodReportPdf(TimeReportDto report, string filePath);

    bool HasActiveTimer();
    int? GetActiveTaskId();
    string? GetActiveTaskTitle();
    int GetTodayTotalMinutes();
}
