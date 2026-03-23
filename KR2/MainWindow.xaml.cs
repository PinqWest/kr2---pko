using System.Windows;
using KR2.ViewModels;
using TimeTracker.Core.Abstractions;
using TimeTracker.Infrastructure.Persistence;
using TimeTracker.Infrastructure.Services;

namespace KR2;

public partial class MainWindow : Window
{
    private readonly ITimeTrackerService _trackerService;

    public MainWindow()
    {
        InitializeComponent();

        var dbContextFactory = new AppDbContextFactory();
        _trackerService = new TimeTrackerService(dbContextFactory);
        var viewModel = new MainViewModel(_trackerService);
        viewModel.OpenJournalRequested += OpenJournal;
        viewModel.OpenReportsRequested += OpenReports;
        DataContext = viewModel;
    }

    private void OpenJournal()
    {
        var window = new JournalWindow
        {
            Owner = this,
            DataContext = new JournalViewModel(_trackerService)
        };
        window.ShowDialog();
    }

    private void OpenReports()
    {
        var window = new ReportsWindow
        {
            Owner = this,
            DataContext = new ReportViewModel(_trackerService)
        };
        window.ShowDialog();
    }
}
