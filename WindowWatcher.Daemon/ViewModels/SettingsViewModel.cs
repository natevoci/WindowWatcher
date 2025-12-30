using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WindowWatcher.Common.Models;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settingsService;
    private string _snapshotsPath;
    private string _dataDirectory;
    private bool _minimizeOnStartup;
    private int _idleTimeoutSeconds;
    private int _logRetentionDays;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var settings = _settingsService.LoadSettings();
        _snapshotsPath = settings.SnapshotsPath;
        _dataDirectory = settings.DataDirectory;
        _minimizeOnStartup = settings.MinimizeOnStartup;
        _idleTimeoutSeconds = settings.IdleTimeoutSeconds;
        _logRetentionDays = settings.LogRetentionDays;

        SaveCommand = new RelayCommand(Save);
        BrowseSnapshotsCommand = new RelayCommand(() => BrowsePath(path => SnapshotsPath = path));
        BrowseDataDirectoryCommand = new RelayCommand(() => BrowsePath(path => DataDirectory = path));
    }

    public string SnapshotsPath
    {
        get => _snapshotsPath;
        set
        {
            if (_snapshotsPath != value)
            {
                _snapshotsPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string DataDirectory
    {
        get => _dataDirectory;
        set
        {
            if (_dataDirectory != value)
            {
                _dataDirectory = value;
                OnPropertyChanged();
            }
        }
    }

    public bool MinimizeOnStartup
    {
        get => _minimizeOnStartup;
        set
        {
            if (_minimizeOnStartup != value)
            {
                _minimizeOnStartup = value;
                OnPropertyChanged();
            }
        }
    }

    public int IdleTimeoutSeconds
    {
        get => _idleTimeoutSeconds;
        set
        {
            if (_idleTimeoutSeconds != value)
            {
                _idleTimeoutSeconds = value;
                OnPropertyChanged();
            }
        }
    }

    public int LogRetentionDays
    {
        get => _logRetentionDays;
        set
        {
            if (_logRetentionDays != value)
            {
                _logRetentionDays = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand BrowseSnapshotsCommand { get; }
    public ICommand BrowseDataDirectoryCommand { get; }

    public event EventHandler? SettingsSaved;

    private void Save()
    {
        var settings = new AppSettings
        {
            SnapshotsPath = SnapshotsPath,
            DataDirectory = DataDirectory,
            MinimizeOnStartup = MinimizeOnStartup,
            IdleTimeoutSeconds = IdleTimeoutSeconds,
            LogRetentionDays = LogRetentionDays
        };

        try
        {
            _settingsService.SaveSettings(settings);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void BrowsePath(Action<string> setPath)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "Select Folder",
            Filter = "Folders|no.files",
            ValidateNames = false
        };

        if (dialog.ShowDialog() == true)
        {
            var path = System.IO.Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(path))
            {
                setPath(path);
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}