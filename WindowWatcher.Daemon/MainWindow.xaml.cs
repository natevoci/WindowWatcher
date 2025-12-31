using System.Windows;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public partial class MainWindow : Window
{
    private readonly ViewEventsViewModel _viewEventsViewModel;
    private readonly SettingsService _settingsService;
    private readonly ScreenshotCaptureService _screenshotCaptureService;
    private readonly LoggingService _loggingService;
    private ViewEventsWindow? _viewEventsWindow;
    private WindowWatcher.Analyser.MainWindow? _analyserWindow;
    private WindowWatcher.ScreenshotViewer.ScreenshotViewerWindow? _screenshotViewerWindow;

    public MainWindow(
        ViewEventsViewModel viewEventsViewModel,
        SettingsService settingsService,
        ScreenshotCaptureService screenshotCaptureService,
        LoggingService loggingService)
    {
        InitializeComponent();

        _viewEventsViewModel = viewEventsViewModel;
        _settingsService = settingsService;
        _screenshotCaptureService = screenshotCaptureService;
        _loggingService = loggingService;
    }

    private void ShowViewEventsWindow()
    {
        if (_viewEventsWindow == null || !_viewEventsWindow.IsLoaded)
        {
            _viewEventsWindow = new ViewEventsWindow(_viewEventsViewModel);
            _viewEventsWindow.Closed += (s, e) => _viewEventsWindow = null;
        }

        _viewEventsWindow.Show();
        _viewEventsWindow.Activate();
    }

    public ViewEventsViewModel ViewEventsViewModel => _viewEventsViewModel;

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowScreenshotViewerWindow();
    }

    private void ViewEventsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowViewEventsWindow();
    }

    private void ViewAnalyserMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowAnalyserWindow();
    }

    private void ViewScreenshotsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowScreenshotViewerWindow();
    }

    private void ShowAnalyserWindow()
    {
        try
        {
            if (_analyserWindow == null || !_analyserWindow.IsLoaded)
            {
                _analyserWindow = new WindowWatcher.Analyser.MainWindow();
                _analyserWindow.Closed += (s, e) => _analyserWindow = null;
            }

            _analyserWindow.Show();
            _analyserWindow.Activate();
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to open analyser window.", ex);
            ShowMessageBox($"Failed to open analyser window:\n{ex.Message}",
                "Launch Error", MessageBoxImage.Error);
        }
    }

    private void ShowScreenshotViewerWindow()
    {
        try
        {
            if (_screenshotViewerWindow == null || !_screenshotViewerWindow.IsLoaded)
            {
                _screenshotViewerWindow = new WindowWatcher.ScreenshotViewer.ScreenshotViewerWindow();
                _screenshotViewerWindow.Closed += (s, e) => _screenshotViewerWindow = null;
            }

            _screenshotViewerWindow.Show();
            _screenshotViewerWindow.Activate();
        }
        catch (Exception ex)
        {
            _loggingService.LogError("Failed to open screenshot viewer window.", ex);
            ShowMessageBox($"Failed to open screenshot viewer window:\n{ex.Message}",
                "Launch Error", MessageBoxImage.Error);
        }
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settingsService, _loggingService)
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void CaptureToggleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        bool isCapturing = _screenshotCaptureService.ToggleCapturing();
        if (sender is System.Windows.Controls.MenuItem menuItem)
        {
            menuItem.Header = isCapturing ? "Stop Capture" : "Start Capture";
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        TrayIcon.Dispose();
    }

    private void ShowMessageBox(string message, string title, MessageBoxImage icon)
    {
        // Create a temporary visible window to own the MessageBox
        var tempWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Width = 0,
            Height = 0,
            Left = -10000,
            Top = -10000,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };
        tempWindow.Show();
        MessageBox.Show(tempWindow, message, title, MessageBoxButton.OK, icon);
        tempWindow.Close();
    }
}
