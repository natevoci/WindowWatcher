using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Forms;
using WindowWatcher.Common.Models;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settingsService;
    private readonly LoggingService _loggingService;
    private string _screenshotsPath;
    private string _dataDirectory;
    private bool _minimizeOnStartup;
    private int _idleTimeoutSeconds;
    private int _logRetentionDays;
    private string _logLevel;
    private int _screenshotIntervalSeconds;
    private string _screenshotCaptureTarget;
    private string _screenshotImageFormat;
    private int _screenshotRetentionDays;

    public SettingsViewModel(SettingsService settingsService, LoggingService loggingService)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        var settings = _settingsService.LoadSettings();
        _screenshotsPath = settings.ScreenshotsPath;
        _dataDirectory = settings.DataDirectory;
        _minimizeOnStartup = settings.MinimizeOnStartup;
        _idleTimeoutSeconds = settings.IdleTimeoutSeconds;
        _logRetentionDays = settings.LogRetentionDays;
        _logLevel = settings.LogLevel;
        _screenshotIntervalSeconds = settings.ScreenshotIntervalSeconds;
        _screenshotCaptureTarget = settings.ScreenshotCaptureTarget;
        _screenshotImageFormat = NormalizeImageFormat(settings.ScreenshotImageFormat);
        _screenshotRetentionDays = settings.ScreenshotRetentionDays;

        CaptureTargetOptions = new ObservableCollection<CaptureTargetOption>();
        ImageFormatOptions = new ObservableCollection<ImageFormatOption>
        {
            new("PNG (lossless, larger)", "Png"),
            new("JPEG (fast, smaller, lossy)", "Jpeg")
        };
        LogLevelOptions = new ObservableCollection<string>
        {
            "Trace",
            "Debug",
            "Information",
            "Warning",
            "Error",
            "Critical"
        };
        BuildCaptureTargetOptions(_screenshotCaptureTarget);

        SaveCommand = new RelayCommand(Save);
        BrowseScreenshotsCommand = new RelayCommand(() => BrowsePath(path => ScreenshotsPath = path));
        BrowseDataDirectoryCommand = new RelayCommand(() => BrowsePath(path => DataDirectory = path));
        RefreshCaptureTargetsCommand = new RelayCommand(() => BuildCaptureTargetOptions(ScreenshotCaptureTarget));
    }

    public string ScreenshotsPath
    {
        get => _screenshotsPath;
        set
        {
            if (_screenshotsPath != value)
            {
                _screenshotsPath = value;
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

    public string LogLevel
    {
        get => _logLevel;
        set
        {
            if (_logLevel != value)
            {
                _logLevel = value;
                OnPropertyChanged();
            }
        }
    }

    public int ScreenshotIntervalSeconds
    {
        get => _screenshotIntervalSeconds;
        set
        {
            if (_screenshotIntervalSeconds != value)
            {
                _screenshotIntervalSeconds = value;
                OnPropertyChanged();
            }
        }
    }

    public string ScreenshotCaptureTarget
    {
        get => _screenshotCaptureTarget;
        set
        {
            if (_screenshotCaptureTarget != value)
            {
                _screenshotCaptureTarget = value;
                OnPropertyChanged();
            }
        }
    }

    public string ScreenshotImageFormat
    {
        get => _screenshotImageFormat;
        set
        {
            if (_screenshotImageFormat != value)
            {
                _screenshotImageFormat = value;
                OnPropertyChanged();
            }
        }
    }

    public int ScreenshotRetentionDays
    {
        get => _screenshotRetentionDays;
        set
        {
            if (_screenshotRetentionDays != value)
            {
                _screenshotRetentionDays = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<CaptureTargetOption> CaptureTargetOptions { get; }
    public ObservableCollection<ImageFormatOption> ImageFormatOptions { get; }
    public ObservableCollection<string> LogLevelOptions { get; }

    public ICommand SaveCommand { get; }
    public ICommand BrowseScreenshotsCommand { get; }
    public ICommand BrowseDataDirectoryCommand { get; }
    public ICommand RefreshCaptureTargetsCommand { get; }

    public event EventHandler? SettingsSaved;

    private void Save()
    {
        var settings = new AppSettings
        {
            ScreenshotsPath = ScreenshotsPath,
            DataDirectory = DataDirectory,
            MinimizeOnStartup = MinimizeOnStartup,
            IdleTimeoutSeconds = IdleTimeoutSeconds,
            LogRetentionDays = LogRetentionDays,
            LogLevel = LogLevel,
            ScreenshotIntervalSeconds = ScreenshotIntervalSeconds,
            ScreenshotCaptureTarget = ScreenshotCaptureTarget,
            ScreenshotImageFormat = ScreenshotImageFormat,
            ScreenshotRetentionDays = ScreenshotRetentionDays
        };

        try
        {
            _settingsService.SaveSettings(settings);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to save settings.", ex);
            System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void BuildCaptureTargetOptions(string? preferredTarget)
    {
        CaptureTargetOptions.Clear();
        CaptureTargetOptions.Add(new CaptureTargetOption("All screens", "AllScreens"));
        CaptureTargetOptions.Add(new CaptureTargetOption("Active application", "ActiveApplication"));

        var screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            string label = $"Screen {i + 1} - {screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})";
            string value = $"Screen:{i}";
            CaptureTargetOptions.Add(new CaptureTargetOption(label, value));
        }

        bool preferredExists = CaptureTargetOptions.Any(option =>
            string.Equals(option.Value, preferredTarget, StringComparison.OrdinalIgnoreCase));

        ScreenshotCaptureTarget = preferredExists ? preferredTarget! : "AllScreens";
    }

    private static string NormalizeImageFormat(string? format)
    {
        return string.Equals(format, "Jpeg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "Jpg", StringComparison.OrdinalIgnoreCase)
            ? "Jpeg"
            : "Png";
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

public sealed class CaptureTargetOption
{
    public CaptureTargetOption(string displayName, string value)
    {
        DisplayName = displayName;
        Value = value;
    }

    public string DisplayName { get; }
    public string Value { get; }
}

public sealed class ImageFormatOption
{
    public ImageFormatOption(string displayName, string value)
    {
        DisplayName = displayName;
        Value = value;
    }

    public string DisplayName { get; }
    public string Value { get; }
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