using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Windows.Media;
using TimeTracker.Core.Abstractions;
using TimeTracker.Core.Enums;
using TimeTracker.Core.Models;

namespace KR2.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ITimeTrackerService _timeTrackerService;
    private string _activeTaskText = "Нет активной задачи";
    private string _statusMessage = "Готов к работе";
    private string _todaySummary = "0ч 00м";
    private Brush _statusBrush = Brushes.Red;
    private bool _isTimerRunning;
    private bool _isTodayOnly;
    private string _taskTitleInput = string.Empty;
    private string _sessionCommentInput = string.Empty;
    private TaskListItemViewModel? _selectedTask;
    private readonly DispatcherTimer _refreshTimer;

    public MainViewModel(ITimeTrackerService timeTrackerService)
    {
        _timeTrackerService = timeTrackerService;

        StartTimerCommand = new RelayCommand(
            StartTimer,
            () => !IsTimerRunning && SelectedTask is not null);
        PauseTimerCommand = new RelayCommand(PauseTimer, () => IsTimerRunning);
        ResumeTimerCommand = new RelayCommand(ResumeTimer, () => !IsTimerRunning && SelectedTask is not null);
        StopTimerCommand = new RelayCommand(StopTimer, () => IsTimerRunning);
        CreateTaskCommand = new RelayCommand(CreateTask, () => !string.IsNullOrWhiteSpace(TaskTitleInput));
        OpenJournalCommand = new RelayCommand(OpenJournal);
        OpenReportsCommand = new RelayCommand(OpenReports);
        RecentEntries = new ObservableCollection<RecentTimeEntryViewModel>();
        Tasks = new ObservableCollection<TaskListItemViewModel>();
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _refreshTimer.Tick += (_, _) =>
        {
            if (IsTimerRunning)
            {
                LoadDashboardData();
            }
        };

        LoadDashboardData();
    }

    public RelayCommand StartTimerCommand { get; }
    public RelayCommand PauseTimerCommand { get; }
    public RelayCommand ResumeTimerCommand { get; }
    public RelayCommand StopTimerCommand { get; }
    public RelayCommand CreateTaskCommand { get; }
    public RelayCommand OpenJournalCommand { get; }
    public RelayCommand OpenReportsCommand { get; }
    public ObservableCollection<RecentTimeEntryViewModel> RecentEntries { get; }
    public ObservableCollection<TaskListItemViewModel> Tasks { get; }
    public event Action? OpenJournalRequested;
    public event Action? OpenReportsRequested;

    public string ActiveTaskText
    {
        get => _activeTaskText;
        private set
        {
            _activeTaskText = value;
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

    public string TodaySummary
    {
        get => _todaySummary;
        private set
        {
            _todaySummary = value;
            OnPropertyChanged();
        }
    }

    public bool IsTimerRunning
    {
        get => _isTimerRunning;
        private set
        {
            _isTimerRunning = value;
            OnPropertyChanged();
        }
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set
        {
            _statusBrush = value;
            OnPropertyChanged();
        }
    }

    public bool IsTodayOnly
    {
        get => _isTodayOnly;
        set
        {
            if (_isTodayOnly == value)
            {
                return;
            }

            _isTodayOnly = value;
            OnPropertyChanged();
            LoadDashboardData();
        }
    }

    public string TaskTitleInput
    {
        get => _taskTitleInput;
        set
        {
            if (_taskTitleInput == value)
            {
                return;
            }

            _taskTitleInput = value;
            OnPropertyChanged();
            CreateTaskCommand.RaiseCanExecuteChanged();
            StartTimerCommand.RaiseCanExecuteChanged();
            ResumeTimerCommand.RaiseCanExecuteChanged();
        }
    }

    public string SessionCommentInput
    {
        get => _sessionCommentInput;
        set
        {
            if (_sessionCommentInput == value)
            {
                return;
            }

            _sessionCommentInput = value;
            OnPropertyChanged();
        }
    }

    public TaskListItemViewModel? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask == value)
            {
                return;
            }

            _selectedTask = value;
            OnPropertyChanged();
            StartTimerCommand.RaiseCanExecuteChanged();
            ResumeTimerCommand.RaiseCanExecuteChanged();
        }
    }

    private void StartTimer()
    {
        try
        {
            if (SelectedTask is null)
            {
                throw new InvalidOperationException("Выбери задачу.");
            }

            _timeTrackerService.StartTask(SelectedTask.Id, SessionCommentInput);
            _timeTrackerService.UpdateTaskStatus(SelectedTask.Id, WorkTaskStatus.InProgress);
            IsTimerRunning = true;
            ActiveTaskText = $"Активная задача: {SelectedTask.Title}";
            StatusMessage = "Таймер запущен";
            StatusBrush = Brushes.LimeGreen;
            _refreshTimer.Start();
            LoadDashboardData();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            StatusBrush = Brushes.Red;
        }
    }

    private void PauseTimer()
    {
        try
        {
            _timeTrackerService.PauseActiveTask(SessionCommentInput);
            StatusMessage = "Таймер на паузе";
            StatusBrush = Brushes.Orange;
            LoadDashboardData();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            StatusBrush = Brushes.Red;
        }
    }

    private void ResumeTimer()
    {
        try
        {
            if (SelectedTask is null)
            {
                throw new InvalidOperationException("Выбери задачу для продолжения.");
            }

            _timeTrackerService.ResumeTask(SelectedTask.Id, SessionCommentInput);
            _timeTrackerService.UpdateTaskStatus(SelectedTask.Id, WorkTaskStatus.InProgress);
            StatusMessage = "Таймер продолжен";
            StatusBrush = Brushes.LimeGreen;
            LoadDashboardData();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            StatusBrush = Brushes.Red;
        }
    }

    private void StopTimer()
    {
        try
        {
            var activeTaskId = _timeTrackerService.GetActiveTaskId();
            _timeTrackerService.StopActiveTask(SessionCommentInput);
            if (activeTaskId.HasValue)
            {
                _timeTrackerService.UpdateTaskStatus(activeTaskId.Value, WorkTaskStatus.Done);
            }
            IsTimerRunning = false;
            ActiveTaskText = "Нет активной задачи";
            StatusMessage = "Таймер остановлен";
            StatusBrush = Brushes.Red;
            _refreshTimer.Stop();
            LoadDashboardData();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            StatusBrush = Brushes.Red;
        }
    }

    private void CreateTask()
    {
        try
        {
            var id = _timeTrackerService.CreateTask(TaskTitleInput);
            TaskTitleInput = string.Empty;
            StatusMessage = "Задача добавлена";
            LoadTasks();
            SelectedTask = Tasks.FirstOrDefault(x => x.Id == id);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
            StatusBrush = Brushes.Red;
        }
    }

    private void LoadDashboardData()
    {
        LoadTasks();

        IsTimerRunning = _timeTrackerService.HasActiveTimer();
        StatusBrush = IsTimerRunning ? Brushes.LimeGreen : Brushes.Red;
        if (IsTimerRunning)
        {
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }

        var activeTitle = _timeTrackerService.GetActiveTaskTitle();
        ActiveTaskText = IsTimerRunning && !string.IsNullOrWhiteSpace(activeTitle)
            ? $"Активная задача: {activeTitle}"
            : "Нет активной задачи";

        var activeTaskId = _timeTrackerService.GetActiveTaskId();
        if (activeTaskId.HasValue)
        {
            SelectedTask = Tasks.FirstOrDefault(x => x.Id == activeTaskId.Value) ?? SelectedTask;
        }

        var totalMinutes = _timeTrackerService.GetTodayTotalMinutes();
        TodaySummary = $"{totalMinutes / 60}ч {totalMinutes % 60:00}м";

        RecentEntries.Clear();
        foreach (var item in _timeTrackerService.GetRecentEntries(8, IsTodayOnly))
        {
            RecentEntries.Add(MapToVm(item));
        }

        RefreshCommands();
    }

    private void LoadTasks()
    {
        var taskDtos = _timeTrackerService.GetTasks();
        var selectedId = SelectedTask?.Id;
        Tasks.Clear();
        foreach (var item in taskDtos)
        {
            Tasks.Add(new TaskListItemViewModel
            {
                Id = item.Id,
                Title = item.Title,
                Status = item.Status
            });
        }

        if (selectedId.HasValue)
        {
            SelectedTask = Tasks.FirstOrDefault(x => x.Id == selectedId.Value);
        }
        else if (SelectedTask is null && Tasks.Count > 0)
        {
            SelectedTask = Tasks[0];
        }
    }

    private static RecentTimeEntryViewModel MapToVm(RecentTimeEntryDto item)
    {
        var duration = item.EndAt is null
            ? "В процессе"
            : $"{item.DurationMinutes / 60}ч {item.DurationMinutes % 60:00}м";

        return new RecentTimeEntryViewModel
        {
            TaskTitle = item.TaskTitle,
            StartedAt = item.StartAt.ToLocalTime().ToString("dd.MM HH:mm"),
            EndedAt = item.EndAt?.ToLocalTime().ToString("dd.MM HH:mm") ?? "-",
            Duration = duration
        };
    }

    private void RefreshCommands()
    {
        StartTimerCommand.RaiseCanExecuteChanged();
        PauseTimerCommand.RaiseCanExecuteChanged();
        ResumeTimerCommand.RaiseCanExecuteChanged();
        StopTimerCommand.RaiseCanExecuteChanged();
        CreateTaskCommand.RaiseCanExecuteChanged();
    }

    private void OpenJournal()
    {
        OpenJournalRequested?.Invoke();
    }

    private void OpenReports()
    {
        OpenReportsRequested?.Invoke();
    }
}

public sealed class TaskListItemViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public WorkTaskStatus Status { get; init; }
    public string DisplayName => $"{Title} ({Status})";
}
