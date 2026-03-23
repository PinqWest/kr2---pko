using System.Collections.ObjectModel;
using TimeTracker.Core.Abstractions;
using TimeTracker.Core.Models;

namespace KR2.ViewModels;

public sealed class JournalViewModel : ViewModelBase
{
    private readonly ITimeTrackerService _timeTrackerService;
    private bool _isTodayOnly;
    private JournalEntryViewModel? _selectedEntry;
    private string _statusMessage = "Готово";

    public JournalViewModel(ITimeTrackerService timeTrackerService)
    {
        _timeTrackerService = timeTrackerService;
        Entries = new ObservableCollection<JournalEntryViewModel>();
        RefreshCommand = new RelayCommand(LoadEntries);
        SaveChangesCommand = new RelayCommand(SaveSelected, () => SelectedEntry is not null);
        ArchiveCommand = new RelayCommand(ArchiveSelected, () => SelectedEntry is not null);
        LoadEntries();
    }

    public ObservableCollection<JournalEntryViewModel> Entries { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand SaveChangesCommand { get; }
    public RelayCommand ArchiveCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
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
            LoadEntries();
        }
    }

    public JournalEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            _selectedEntry = value;
            OnPropertyChanged();
            SaveChangesCommand.RaiseCanExecuteChanged();
            ArchiveCommand.RaiseCanExecuteChanged();
        }
    }

    private void LoadEntries()
    {
        Entries.Clear();
        foreach (var item in _timeTrackerService.GetAllEntries(IsTodayOnly))
        {
            Entries.Add(MapToVm(item));
        }
        StatusMessage = $"Записей: {Entries.Count}";
    }

    private void SaveSelected()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            _timeTrackerService.UpdateTimeEntry(
                SelectedEntry.TimeEntryId,
                SelectedEntry.StartAtLocal.ToUniversalTime(),
                SelectedEntry.EndAtLocal?.ToUniversalTime(),
                SelectedEntry.Comment);
            StatusMessage = "Изменения сохранены";
            LoadEntries();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    private void ArchiveSelected()
    {
        if (SelectedEntry is null)
        {
            return;
        }

        try
        {
            _timeTrackerService.ArchiveTimeEntry(SelectedEntry.TimeEntryId);
            StatusMessage = "Запись архивирована";
            LoadEntries();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка: {ex.Message}";
        }
    }

    private static JournalEntryViewModel MapToVm(RecentTimeEntryDto item)
    {
        return new JournalEntryViewModel
        {
            TimeEntryId = item.TimeEntryId,
            TaskItemId = item.TaskItemId,
            TaskTitle = item.TaskTitle,
            StartAtLocal = item.StartAt.ToLocalTime(),
            EndAtLocal = item.EndAt?.ToLocalTime(),
            Comment = item.Comment ?? string.Empty
        };
    }
}
