namespace KR2.ViewModels;

public sealed class JournalEntryViewModel : ViewModelBase
{
    public int TimeEntryId { get; init; }
    public int TaskItemId { get; init; }
    public string TaskTitle { get; init; } = string.Empty;

    private DateTime _startAtLocal;
    private DateTime? _endAtLocal;
    private string _comment = string.Empty;

    public DateTime StartAtLocal
    {
        get => _startAtLocal;
        set
        {
            _startAtLocal = value;
            OnPropertyChanged();
        }
    }

    public DateTime? EndAtLocal
    {
        get => _endAtLocal;
        set
        {
            _endAtLocal = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Duration));
        }
    }

    public string Comment
    {
        get => _comment;
        set
        {
            _comment = value;
            OnPropertyChanged();
        }
    }

    public string Duration
    {
        get
        {
            if (EndAtLocal is null)
            {
                return "В процессе";
            }

            var minutes = Math.Max(1, (int)Math.Round((EndAtLocal.Value - StartAtLocal).TotalMinutes));
            return $"{minutes / 60}ч {minutes % 60:00}м";
        }
    }
}
