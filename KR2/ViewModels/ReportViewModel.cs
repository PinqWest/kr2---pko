using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using TimeTracker.Core.Abstractions;
using TimeTracker.Core.Models;

namespace KR2.ViewModels;

public sealed class ReportViewModel : ViewModelBase
{
    private readonly ITimeTrackerService _timeTrackerService;
    private DateTime _startDate = DateTime.Today.AddDays(-6);
    private DateTime _endDate = DateTime.Today;
    private double _dayGoalHours = 8;
    private double _weekGoalHours = 40;
    private string _totalHours = "0";
    private string _planFactDay = "0%";
    private string _planFactWeek = "0%";
    private string _statusMessage = "Готово";
    private TimeReportDto? _currentReport;

    public ReportViewModel(ITimeTrackerService timeTrackerService)
    {
        _timeTrackerService = timeTrackerService;
        ByProjects = new ObservableCollection<ReportAggregateItemViewModel>();
        ByTasks = new ObservableCollection<ReportAggregateItemViewModel>();
        ByTags = new ObservableCollection<ReportAggregateItemViewModel>();
        ByDays = new ObservableCollection<ReportDayItemViewModel>();

        BuildReportCommand = new RelayCommand(BuildReport);
        ExportCsvCommand = new RelayCommand(ExportCsv, () => _currentReport is not null);
        ExportPdfCommand = new RelayCommand(ExportPdf, () => _currentReport is not null);

        BuildReport();
    }

    public DateTime StartDate
    {
        get => _startDate;
        set
        {
            _startDate = value.Date;
            OnPropertyChanged();
        }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set
        {
            _endDate = value.Date;
            OnPropertyChanged();
        }
    }

    public double DayGoalHours
    {
        get => _dayGoalHours;
        set
        {
            _dayGoalHours = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public double WeekGoalHours
    {
        get => _weekGoalHours;
        set
        {
            _weekGoalHours = Math.Max(0, value);
            OnPropertyChanged();
        }
    }

    public string TotalHours
    {
        get => _totalHours;
        private set
        {
            _totalHours = value;
            OnPropertyChanged();
        }
    }

    public string PlanFactDay
    {
        get => _planFactDay;
        private set
        {
            _planFactDay = value;
            OnPropertyChanged();
        }
    }

    public string PlanFactWeek
    {
        get => _planFactWeek;
        private set
        {
            _planFactWeek = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ReportAggregateItemViewModel> ByProjects { get; }
    public ObservableCollection<ReportAggregateItemViewModel> ByTasks { get; }
    public ObservableCollection<ReportAggregateItemViewModel> ByTags { get; }
    public ObservableCollection<ReportDayItemViewModel> ByDays { get; }
    public RelayCommand BuildReportCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand ExportPdfCommand { get; }

    private void BuildReport()
    {
        try
        {
            var startUtc = StartDate.Date.ToUniversalTime();
            var endUtc = EndDate.Date.AddDays(1).ToUniversalTime();
            var dayGoalMinutes = (int)Math.Round(DayGoalHours * 60, MidpointRounding.AwayFromZero);
            var weekGoalMinutes = (int)Math.Round(WeekGoalHours * 60, MidpointRounding.AwayFromZero);

            _currentReport = _timeTrackerService.GetPeriodReport(startUtc, endUtc, dayGoalMinutes, weekGoalMinutes);
            FillReportCollections(_currentReport);

            TotalHours = $"{_currentReport.TotalMinutes / 60.0:0.##} ч";
            PlanFactDay = $"{_currentReport.PlanFact.DayPlanCompletionPercent:0.#}%";
            PlanFactWeek = $"{_currentReport.PlanFact.WeekPlanCompletionPercent:0.#}%";
            StatusMessage = $"Сформировано записей: {_currentReport.Entries.Count}";
            RefreshExportState();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    private void FillReportCollections(TimeReportDto report)
    {
        FillAggregate(ByProjects, report.ByProjects);
        FillAggregate(ByTasks, report.ByTasks);
        FillAggregate(ByTags, report.ByTags);

        ByDays.Clear();
        var maxMinutes = Math.Max(1, report.ByDays.DefaultIfEmpty(new ReportDaySummaryDto { Minutes = 0 }).Max(x => x.Minutes));
        foreach (var item in report.ByDays)
        {
            ByDays.Add(new ReportDayItemViewModel
            {
                DayLabel = item.Day.ToString("dd.MM"),
                HoursLabel = $"{item.Minutes / 60.0:0.#}ч",
                ColumnHeight = 30 + 150.0 * item.Minutes / maxMinutes
            });
        }
    }

    private static void FillAggregate(ObservableCollection<ReportAggregateItemViewModel> target, IReadOnlyList<ReportAggregateItemDto> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(new ReportAggregateItemViewModel
            {
                Name = item.Name,
                Hours = $"{item.Minutes / 60.0:0.##} ч"
            });
        }
    }

    private void ExportCsv()
    {
        if (_currentReport is null)
        {
            return;
        }

        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"report_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _timeTrackerService.ExportPeriodReportCsv(_currentReport, dialog.FileName);
            StatusMessage = $"CSV сохранен: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка CSV: {ex.Message}";
        }
    }

    private void ExportPdf()
    {
        if (_currentReport is null)
        {
            return;
        }

        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"report_{DateTime.Now:yyyyMMdd_HHmm}.pdf"
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            _timeTrackerService.ExportPeriodReportPdf(_currentReport, dialog.FileName);
            StatusMessage = $"PDF сохранен: {Path.GetFileName(dialog.FileName)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка PDF: {ex.Message}";
        }
    }

    private void RefreshExportState()
    {
        ExportCsvCommand.RaiseCanExecuteChanged();
        ExportPdfCommand.RaiseCanExecuteChanged();
    }
}

public sealed class ReportAggregateItemViewModel
{
    public string Name { get; init; } = string.Empty;
    public string Hours { get; init; } = "0";
}

public sealed class ReportDayItemViewModel
{
    public string DayLabel { get; init; } = string.Empty;
    public string HoursLabel { get; init; } = "0";
    public double ColumnHeight { get; init; }
}
