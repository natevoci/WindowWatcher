using System.Windows;
using WindowWatcher.Common.Services;
using WindowWatcher.Daemon.ViewModels;

namespace WindowWatcher.Daemon;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsService settingsService, LoggingService loggingService)
    {
        InitializeComponent();

        var viewModel = new SettingsViewModel(settingsService, loggingService);
        viewModel.SettingsSaved += (s, e) => DialogResult = true;
        DataContext = viewModel;
    }
}