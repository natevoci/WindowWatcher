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

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var settings = _settingsService.LoadSettings();
        _snapshotsPath = settings.SnapshotsPath;

        SaveCommand = new RelayCommand(Save);
        BrowseCommand = new RelayCommand(Browse);
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

    public ICommand SaveCommand { get; }
    public ICommand BrowseCommand { get; }

    public event EventHandler? SettingsSaved;

    private void Save()
    {
        var settings = new AppSettings
        {
            SnapshotsPath = SnapshotsPath
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

    private void Browse()
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
                SnapshotsPath = path;
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