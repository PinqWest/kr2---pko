using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Text;
using TimeTracker.Core.Abstractions;
using TimeTracker.Core.Entities;
using TimeTracker.Core.Enums;
using TimeTracker.Core.Models;
using TimeTracker.Infrastructure.Persistence;

namespace TimeTracker.Infrastructure.Services;

public sealed class TimeTrackerService : ITimeTrackerService
{
    private readonly AppDbContextFactory _contextFactory;

    public TimeTrackerService(AppDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
        using var db = _contextFactory.CreateDbContext();
        db.Database.EnsureCreated();
        EnsureAdditionalColumns(db);
    }

    public IReadOnlyList<TaskItemDto> GetTasks()
    {
        using var db = _contextFactory.CreateDbContext();
        return db.TaskItems
            .AsNoTracking()
            .OrderBy(x => x.Title)
            .Select(x => new TaskItemDto
            {
                Id = x.Id,
                Title = x.Title,
                Status = x.Status
            })
            .ToList();
    }

    public int CreateTask(string taskTitle)
    {
        if (string.IsNullOrWhiteSpace(taskTitle))
        {
            throw new InvalidOperationException("Task title is required.");
        }

        using var db = _contextFactory.CreateDbContext();
        var normalizedTitle = taskTitle.Trim();
        var existing = db.TaskItems.FirstOrDefault(x => x.Title == normalizedTitle);
        if (existing is not null)
        {
            return existing.Id;
        }

        var defaultProject = GetOrCreateDefaultProject(db);
        var task = new TaskItem
        {
            Title = normalizedTitle,
            ProjectId = defaultProject.Id,
            Status = WorkTaskStatus.New
        };
        db.TaskItems.Add(task);
        db.SaveChanges();
        return task.Id;
    }

    public void UpdateTaskStatus(int taskItemId, WorkTaskStatus status)
    {
        using var db = _contextFactory.CreateDbContext();
        var task = db.TaskItems.FirstOrDefault(x => x.Id == taskItemId)
            ?? throw new InvalidOperationException("Task not found.");
        task.Status = status;
        db.SaveChanges();
    }

    public void StartTask(int taskItemId, string? comment)
    {
        if (taskItemId <= 0)
        {
            throw new InvalidOperationException("Task is required.");
        }
        using var db = _contextFactory.CreateDbContext();

        var activeEntryExists = db.TimeEntries.Any(x => x.EndAt == null);
        if (activeEntryExists)
        {
            throw new InvalidOperationException("A timer is already running.");
        }

        var task = db.TaskItems.FirstOrDefault(x => x.Id == taskItemId)
            ?? throw new InvalidOperationException("Task not found.");
        if (task.Status == WorkTaskStatus.Done)
        {
            throw new InvalidOperationException("Cannot start completed task.");
        }

        var entry = new TimeEntry
        {
            TaskItemId = task.Id,
            StartAt = DateTime.UtcNow,
            IsManual = false,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        };

        task.Status = WorkTaskStatus.InProgress;
        db.TimeEntries.Add(entry);
        db.SaveChanges();
    }

    public void PauseActiveTask(string? comment)
    {
        EndActiveEntry(comment, keepTaskInProgress: true);
    }

    public void ResumeTask(int taskItemId, string? comment)
    {
        StartTask(taskItemId, comment);
    }

    public void StopActiveTask(string? comment)
    {
        EndActiveEntry(comment, keepTaskInProgress: false);
    }

    private void EndActiveEntry(string? comment, bool keepTaskInProgress)
    {
        using var db = _contextFactory.CreateDbContext();

        var activeEntry = db.TimeEntries
            .OrderByDescending(x => x.StartAt)
            .FirstOrDefault(x => x.EndAt == null);

        if (activeEntry is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(comment))
        {
            activeEntry.Comment = comment.Trim();
        }

        activeEntry.EndAt = DateTime.UtcNow;
        activeEntry.DurationMinutes = Math.Max(
            1,
            (int)Math.Round((activeEntry.EndAt.Value - activeEntry.StartAt).TotalMinutes));

        var task = db.TaskItems.FirstOrDefault(x => x.Id == activeEntry.TaskItemId);
        if (task is not null && !keepTaskInProgress)
        {
            task.Status = WorkTaskStatus.Done;
        }

        db.SaveChanges();
    }

    public IReadOnlyList<RecentTimeEntryDto> GetRecentEntries(int count, bool onlyToday)
    {
        using var db = _contextFactory.CreateDbContext();
        var query = db.TimeEntries
            .AsNoTracking()
            .Include(x => x.TaskItem)
            .Where(x => !x.IsArchived)
            .AsQueryable();

        if (onlyToday)
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            query = query.Where(x => x.StartAt >= today && x.StartAt < tomorrow);
        }

        return query
            .OrderByDescending(x => x.StartAt)
            .Take(count)
            .Select(ToDtoExpression)
            .ToList();
    }

    public IReadOnlyList<RecentTimeEntryDto> GetAllEntries(bool onlyToday)
    {
        using var db = _contextFactory.CreateDbContext();
        var query = db.TimeEntries
            .AsNoTracking()
            .Include(x => x.TaskItem)
            .Where(x => !x.IsArchived)
            .AsQueryable();

        if (onlyToday)
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            query = query.Where(x => x.StartAt >= today && x.StartAt < tomorrow);
        }

        return query
            .OrderByDescending(x => x.StartAt)
            .Select(ToDtoExpression)
            .ToList();
    }

    public void UpdateTimeEntry(int timeEntryId, DateTime startAt, DateTime? endAt, string? comment)
    {
        if (endAt is not null && endAt <= startAt)
        {
            throw new InvalidOperationException("End time must be greater than start time.");
        }

        using var db = _contextFactory.CreateDbContext();
        var entry = db.TimeEntries.FirstOrDefault(x => x.Id == timeEntryId && !x.IsArchived)
            ?? throw new InvalidOperationException("Entry not found.");

        var overlapEnd = endAt ?? DateTime.MaxValue;
        var overlaps = db.TimeEntries.Any(x =>
            x.Id != timeEntryId &&
            !x.IsArchived &&
            x.TaskItemId == entry.TaskItemId &&
            x.StartAt < overlapEnd &&
            (x.EndAt ?? DateTime.MaxValue) > startAt);
        if (overlaps)
        {
            throw new InvalidOperationException("Time entry overlaps another entry for this task.");
        }

        entry.StartAt = startAt;
        entry.EndAt = endAt;
        entry.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        entry.DurationMinutes = endAt is null
            ? 0
            : Math.Max(1, (int)Math.Round((endAt.Value - startAt).TotalMinutes));

        db.SaveChanges();
    }

    public void ArchiveTimeEntry(int timeEntryId)
    {
        using var db = _contextFactory.CreateDbContext();
        var entry = db.TimeEntries.FirstOrDefault(x => x.Id == timeEntryId)
            ?? throw new InvalidOperationException("Entry not found.");
        entry.IsArchived = true;
        db.SaveChanges();
    }

    private static readonly System.Linq.Expressions.Expression<Func<TimeEntry, RecentTimeEntryDto>> ToDtoExpression = x =>
        new RecentTimeEntryDto
        {
            TimeEntryId = x.Id,
            TaskItemId = x.TaskItemId,
            TaskTitle = x.TaskItem != null ? x.TaskItem.Title : "Unknown task",
            StartAt = x.StartAt,
            EndAt = x.EndAt,
            DurationMinutes = x.DurationMinutes,
            Comment = x.Comment,
            IsArchived = x.IsArchived
        };

    public bool HasActiveTimer()
    {
        using var db = _contextFactory.CreateDbContext();
        return db.TimeEntries.Any(x => x.EndAt == null && !x.IsArchived);
    }

    public int? GetActiveTaskId()
    {
        using var db = _contextFactory.CreateDbContext();
        return db.TimeEntries
            .AsNoTracking()
            .Where(x => x.EndAt == null && !x.IsArchived)
            .OrderByDescending(x => x.StartAt)
            .Select(x => (int?)x.TaskItemId)
            .FirstOrDefault();
    }

    public string? GetActiveTaskTitle()
    {
        using var db = _contextFactory.CreateDbContext();

        return db.TimeEntries
            .AsNoTracking()
            .Where(x => x.EndAt == null && !x.IsArchived)
            .Include(x => x.TaskItem)
            .OrderByDescending(x => x.StartAt)
            .Select(x => x.TaskItem != null ? x.TaskItem.Title : null)
            .FirstOrDefault();
    }

    public int GetTodayTotalMinutes()
    {
        using var db = _contextFactory.CreateDbContext();
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return db.TimeEntries
            .Where(x => !x.IsArchived && x.EndAt != null && x.StartAt >= today && x.StartAt < tomorrow)
            .Sum(x => x.DurationMinutes);
    }

    public TimeReportDto GetPeriodReport(DateTime startUtcInclusive, DateTime endUtcExclusive, int dayGoalMinutes, int weekGoalMinutes)
    {
        if (endUtcExclusive <= startUtcInclusive)
        {
            throw new InvalidOperationException("Report period is invalid.");
        }

        using var db = _contextFactory.CreateDbContext();
        var entries = db.TimeEntries
            .AsNoTracking()
            .Where(x => !x.IsArchived && x.EndAt != null && x.StartAt >= startUtcInclusive && x.StartAt < endUtcExclusive)
            .Include(x => x.TaskItem)
            .ThenInclude(x => x!.Project)
            .Include(x => x.TaskItem)
            .ThenInclude(x => x!.TaskItemTags)
            .ThenInclude(x => x.Tag)
            .OrderBy(x => x.StartAt)
            .ToList();

        var reportEntries = entries
            .Select(x => new ReportEntryDto
            {
                StartUtc = x.StartAt,
                EndUtc = x.EndAt!.Value,
                ProjectName = x.TaskItem?.Project?.Name ?? "Без проекта",
                TaskTitle = x.TaskItem?.Title ?? "Без задачи",
                Tags = string.Join(", ", x.TaskItem?.TaskItemTags.Select(t => t.Tag?.Name).Where(t => !string.IsNullOrWhiteSpace(t)) ?? []),
                Minutes = x.DurationMinutes
            })
            .ToList();

        var byProjects = reportEntries
            .GroupBy(x => x.ProjectName)
            .Select(x => new ReportAggregateItemDto { Name = x.Key, Minutes = x.Sum(v => v.Minutes) })
            .OrderByDescending(x => x.Minutes)
            .ToList();

        var byTasks = reportEntries
            .GroupBy(x => x.TaskTitle)
            .Select(x => new ReportAggregateItemDto { Name = x.Key, Minutes = x.Sum(v => v.Minutes) })
            .OrderByDescending(x => x.Minutes)
            .ToList();

        var byTags = entries
            .SelectMany(x =>
            {
                var tagNames = x.TaskItem?.TaskItemTags
                    .Select(t => t.Tag?.Name)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (tagNames is null || tagNames.Count == 0)
                {
                    return new[] { ("Без тега", x.DurationMinutes) };
                }

                return tagNames.Select(name => (name, x.DurationMinutes));
            })
            .GroupBy(x => x.name)
            .Select(x => new ReportAggregateItemDto { Name = x.Key, Minutes = x.Sum(v => v.DurationMinutes) })
            .OrderByDescending(x => x.Minutes)
            .ToList();

        var byDays = reportEntries
            .GroupBy(x => DateOnly.FromDateTime(x.StartUtc))
            .Select(x => new ReportDaySummaryDto { Day = x.Key, Minutes = x.Sum(v => v.Minutes) })
            .OrderBy(x => x.Day)
            .ToList();

        var totalMinutes = reportEntries.Sum(x => x.Minutes);
        var dayCount = Math.Max(1, (int)Math.Ceiling((endUtcExclusive - startUtcInclusive).TotalDays));
        var weekCount = Math.Max(1, (int)Math.Ceiling(dayCount / 7.0));

        var dayGoalTotal = Math.Max(0, dayGoalMinutes) * dayCount;
        var weekGoalTotal = Math.Max(0, weekGoalMinutes) * weekCount;

        return new TimeReportDto
        {
            StartUtc = startUtcInclusive,
            EndUtc = endUtcExclusive,
            TotalMinutes = totalMinutes,
            ByProjects = byProjects,
            ByTasks = byTasks,
            ByTags = byTags,
            ByDays = byDays,
            Entries = reportEntries,
            PlanFact = new PlanFactDto
            {
                DayGoalMinutes = Math.Max(0, dayGoalMinutes),
                WeekGoalMinutes = Math.Max(0, weekGoalMinutes),
                ActualMinutes = totalMinutes,
                DaysInPeriod = dayCount,
                WeeksInPeriod = weekCount,
                DayPlanCompletionPercent = dayGoalTotal == 0 ? 0 : Math.Round(totalMinutes * 100.0 / dayGoalTotal, 1),
                WeekPlanCompletionPercent = weekGoalTotal == 0 ? 0 : Math.Round(totalMinutes * 100.0 / weekGoalTotal, 1)
            }
        };
    }

    public void ExportPeriodReportCsv(TimeReportDto report, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Дата;Проект;Задача;Теги;Минут;Часы");
        foreach (var row in report.Entries.OrderBy(x => x.StartUtc))
        {
            var hours = Math.Round(row.Minutes / 60.0, 2).ToString("0.##", CultureInfo.InvariantCulture);
            sb.AppendLine(
                $"{EscapeCsv(row.StartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))};" +
                $"{EscapeCsv(row.ProjectName)};" +
                $"{EscapeCsv(row.TaskTitle)};" +
                $"{EscapeCsv(row.Tags)};" +
                $"{row.Minutes};{hours}");
        }

        sb.AppendLine();
        sb.AppendLine($"Итого минут;{report.TotalMinutes}");
        sb.AppendLine($"Итого часов;{Math.Round(report.TotalMinutes / 60.0, 2).ToString("0.##", CultureInfo.InvariantCulture)}");
        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    public void ExportPeriodReportPdf(TimeReportDto report, string filePath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header()
                    .Text($"Отчет {report.StartUtc.ToLocalTime():dd.MM.yyyy} - {report.EndUtc.AddSeconds(-1).ToLocalTime():dd.MM.yyyy}")
                    .FontSize(16)
                    .SemiBold();

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Итого: {report.TotalMinutes / 60}ч {report.TotalMinutes % 60:00}м");
                    col.Item().Text($"План-факт (день): {report.PlanFact.DayPlanCompletionPercent:0.#}%");
                    col.Item().Text($"План-факт (неделя): {report.PlanFact.WeekPlanCompletionPercent:0.#}%");

                    col.Item().Text("По дням").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("День");
                            header.Cell().Element(CellStyle).AlignRight().Text("Часы");
                        });

                        foreach (var day in report.ByDays)
                        {
                            table.Cell().Element(CellStyle).Text(day.Day.ToString("dd.MM.yyyy"));
                            table.Cell().Element(CellStyle).AlignRight().Text(FormatDuration(day.Minutes));
                        }
                    });

                    col.Item().PaddingTop(6).Text("По задачам").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Задача");
                            header.Cell().Element(CellStyle).AlignRight().Text("Минут");
                            header.Cell().Element(CellStyle).AlignRight().Text("Часы");
                        });

                        foreach (var task in report.ByTasks)
                        {
                            table.Cell().Element(CellStyle).Text(task.Name);
                            table.Cell().Element(CellStyle).AlignRight().Text(task.Minutes.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(CellStyle).AlignRight().Text(FormatDuration(task.Minutes));
                        }
                    });

                    col.Item().PaddingTop(6).Text("Детализация по дням и задачам").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("День");
                            header.Cell().Element(CellStyle).Text("Задача");
                            header.Cell().Element(CellStyle).AlignRight().Text("Минут");
                            header.Cell().Element(CellStyle).AlignRight().Text("Часы");
                        });

                        var byDayAndTask = report.Entries
                            .GroupBy(x => new { Day = DateOnly.FromDateTime(x.StartUtc.ToLocalTime()), x.TaskTitle })
                            .Select(x => new
                            {
                                x.Key.Day,
                                x.Key.TaskTitle,
                                Minutes = x.Sum(v => v.Minutes)
                            })
                            .OrderBy(x => x.Day)
                            .ThenByDescending(x => x.Minutes)
                            .ToList();

                        foreach (var row in byDayAndTask)
                        {
                            table.Cell().Element(CellStyle).Text(row.Day.ToString("dd.MM.yyyy"));
                            table.Cell().Element(CellStyle).Text(row.TaskTitle);
                            table.Cell().Element(CellStyle).AlignRight().Text(row.Minutes.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(CellStyle).AlignRight().Text(FormatDuration(row.Minutes));
                        }
                    });

                    col.Item().PaddingTop(6).Text("Детализация записей").SemiBold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Старт");
                            header.Cell().Element(CellStyle).Text("Конец");
                            header.Cell().Element(CellStyle).Text("Задача");
                            header.Cell().Element(CellStyle).Text("Проект");
                            header.Cell().Element(CellStyle).AlignRight().Text("Часы");
                        });

                        foreach (var entry in report.Entries.OrderBy(x => x.StartUtc))
                        {
                            table.Cell().Element(CellStyle).Text(entry.StartUtc.ToLocalTime().ToString("dd.MM HH:mm"));
                            table.Cell().Element(CellStyle).Text(entry.EndUtc.ToLocalTime().ToString("dd.MM HH:mm"));
                            table.Cell().Element(CellStyle).Text(entry.TaskTitle);
                            table.Cell().Element(CellStyle).Text(entry.ProjectName);
                            table.Cell().Element(CellStyle).AlignRight().Text(FormatDuration(entry.Minutes));
                        }
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(';') && !value.Contains('"') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4);
    }

    private static string FormatDuration(int minutes)
    {
        return $"{minutes / 60.0:0.##}";
    }

    private static Project GetOrCreateDefaultProject(AppDbContext db)
    {
        var defaultProject = db.Projects.FirstOrDefault();
        if (defaultProject is not null)
        {
            return defaultProject;
        }

        defaultProject = new Project { Name = "Default project" };
        db.Projects.Add(defaultProject);
        db.SaveChanges();
        return defaultProject;
    }

    private static void EnsureAdditionalColumns(AppDbContext db)
    {
        TryAddColumn(db, "ALTER TABLE TaskItems ADD COLUMN Status INTEGER NOT NULL DEFAULT 0;");
        TryAddColumn(db, "ALTER TABLE TimeEntries ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0;");
    }

    private static void TryAddColumn(AppDbContext db, string sql)
    {
        try
        {
            db.Database.ExecuteSqlRaw(sql);
        }
        catch
        {
            // Column already exists on existing databases.
        }
    }
}
