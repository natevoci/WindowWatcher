using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WindowWatcher.Common.Models;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Analyser.ViewModels;

public class AnalyserViewModel : INotifyPropertyChanged
{
    private readonly CsvDataService _csvDataService;
    private readonly SettingsService _settingsService;
    private List<WindowActivityEvent> _allEvents = new();
    private ObservableCollection<WindowActivityEvent> _filteredEvents = new();
    private ObservableCollection<string> _processList = new();
    private ObservableCollection<string> _rows = new();
    private DateTime _selectedDate = DateTime.Today.AddHours(12);
    private string _selectedProcess = "-- All Processes --";
    private double _hoursVisible = 24.0;
    private DateTime _startTime;
    private DateTime _endTime;
    private int _scrollOffset = 0;

    public AnalyserViewModel()
    {
        _csvDataService = new CsvDataService();
        _settingsService = new SettingsService();

        // Initialize commands
        PreviousDayCommand = new RelayCommand(PreviousDay);
        NextDayCommand = new RelayCommand(NextDay);
        ScrollLeftCommand = new RelayCommand(ScrollLeft);
        ScrollRightCommand = new RelayCommand(ScrollRight);
        FullDayCommand = new RelayCommand(FullDay);
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);

        LoadData();
        UpdateTimeWindow();
        LoadEvents();
    }

    public ObservableCollection<WindowActivityEvent> FilteredEvents
    {
        get => _filteredEvents;
        set
        {
            _filteredEvents = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> ProcessList
    {
        get => _processList;
        set
        {
            _processList = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<string> Rows
    {
        get => _rows;
        set
        {
            _rows = value;
            OnPropertyChanged();
        }
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            _selectedDate = value;
            OnPropertyChanged();
            UpdateTimeWindow();
            LoadEvents();
        }
    }

    public string SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            _selectedProcess = value;
            OnPropertyChanged();
            LoadEvents();
        }
    }

    public DateTime StartTime
    {
        get => _startTime;
        set
        {
            _startTime = value;
            OnPropertyChanged();
        }
    }

    public DateTime EndTime
    {
        get => _endTime;
        set
        {
            _endTime = value;
            OnPropertyChanged();
        }
    }

    public int ScrollOffset
    {
        get => _scrollOffset;
        set
        {
            _scrollOffset = value;
            OnPropertyChanged();
        }
    }

    public ICommand PreviousDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand ScrollLeftCommand { get; }
    public ICommand ScrollRightCommand { get; }
    public ICommand FullDayCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }

    private void LoadData()
    {
        var settings = _settingsService.LoadSettings();
        _allEvents = _csvDataService.LoadAllEvents(settings.DataDirectory);

        // Build process list
        var processes = new HashSet<string>();
        foreach (var evt in _allEvents)
        {
            if (evt.EventType == WindowActivityEventType.WindowActivated &&
                !string.IsNullOrEmpty(evt.ProcessName) &&
                !string.Equals(evt.ProcessName, "Idle", StringComparison.OrdinalIgnoreCase))
            {
                processes.Add(evt.ProcessName);
            }
        }

        var sortedProcesses = processes.OrderBy(p => p).ToList();
        sortedProcesses.Insert(0, "Idle");
        sortedProcesses.Insert(0, "-- All Processes --");

        ProcessList = new ObservableCollection<string>(sortedProcesses);
    }

    private void UpdateTimeWindow()
    {
        StartTime = _selectedDate.AddHours(_hoursVisible / -2.0);
        EndTime = _selectedDate.AddHours(_hoursVisible / 2.0);
    }

    private void LoadEvents()
    {
        var filtered = _allEvents.Where(e => e.Date >= StartTime && e.Date <= EndTime).ToList();

        // Filter by process if selected
        if (!string.IsNullOrEmpty(_selectedProcess) && _selectedProcess != "-- All Processes --")
        {
            filtered = filtered.Where(e =>
            {
                if (e.EventType == WindowActivityEventType.Inactive && _selectedProcess == "Idle")
                    return true;
                if (e.EventType == WindowActivityEventType.WindowActivated)
                    return string.Equals(e.ProcessName, _selectedProcess, StringComparison.OrdinalIgnoreCase);
                return false;
            }).ToList();
        }

        FilteredEvents = new ObservableCollection<WindowActivityEvent>(filtered);

        // Build rows for display
        var rowSet = new HashSet<string>();
        foreach (var evt in filtered)
        {
            if (evt.EventType == WindowActivityEventType.Inactive)
            {
                rowSet.Add("Idle");
            }
            else if (evt.EventType == WindowActivityEventType.WindowActivated)
            {
                if (_selectedProcess == "-- All Processes --" || string.IsNullOrEmpty(_selectedProcess))
                {
                    rowSet.Add(evt.ProcessName);
                }
                else
                {
                    rowSet.Add($"{evt.ProcessName} - {evt.WindowTitle}");
                }
            }
        }

        var sortedRows = rowSet.OrderBy(r => r).ToList();
        if (sortedRows.Contains("Idle"))
        {
            sortedRows.Remove("Idle");
            sortedRows.Insert(0, "Idle");
        }

        Rows = new ObservableCollection<string>(sortedRows);
    }

    private void PreviousDay()
    {
        SelectedDate = SelectedDate.AddHours(_hoursVisible * -1.0);
    }

    private void NextDay()
    {
        SelectedDate = SelectedDate.AddHours(_hoursVisible * 1.0);
    }

    private void ScrollLeft()
    {
        SelectedDate = SelectedDate.AddHours(_hoursVisible * -0.2);
    }

    private void ScrollRight()
    {
        SelectedDate = SelectedDate.AddHours(_hoursVisible * 0.2);
    }

    private void FullDay()
    {
        _hoursVisible = 24.0;
        UpdateTimeWindow();
        LoadEvents();
    }

    private void ZoomIn()
    {
        _hoursVisible = Math.Max(1.0, _hoursVisible / 2.0);
        UpdateTimeWindow();
        LoadEvents();
    }

    private void ZoomOut()
    {
        _hoursVisible = Math.Min(48.0, _hoursVisible * 2.0);
        UpdateTimeWindow();
        LoadEvents();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}
