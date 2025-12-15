using System.Windows;
using WindowWatcher.Common.Services;
using ScreenshotViewer.ViewModels;

namespace ScreenshotViewer;

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
