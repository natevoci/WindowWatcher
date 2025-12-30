using System.Windows;
using WindowWatcher.Common.Services;
using WindowWatcher.Daemon.ViewModels;

namespace WindowWatcher.Daemon;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();

        var viewModel = new SettingsViewModel(settingsService);
        viewModel.SettingsSaved += (s, e) => DialogResult = true;
        DataContext = viewModel;
    }
}