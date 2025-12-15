using System.Diagnostics;
using System.IO;
using System.Windows;
using WindowWatcher.Common.Services;

namespace WindowWatcherDaemon;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private WindowState _previousWindowState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _settingsService = new SettingsService();
        var settings = _settingsService.LoadSettings();

        // Start minimized if configured
        if (settings.MinimizeOnStartup)
        {
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;
            TrayIcon.Visibility = Visibility.Visible;
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            SendToTray();
        }
        else
        {
            _previousWindowState = WindowState;
        }
    }

    private void SendToTray()
    {
        Hide();
        ShowInTaskbar = false;
        TrayIcon.Visibility = Visibility.Visible;
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = _previousWindowState;
        TrayIcon.Visibility = Visibility.Collapsed;
        Activate();
    }

    private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    private void RestoreMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    private void ViewAnalyserMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string currPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty) ?? string.Empty;
            string analyserPath = Path.Combine(currPath, "WindowWatcherAnalyser.exe");

            if (File.Exists(analyserPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = analyserPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show($"WindowWatcherAnalyser.exe not found at:\n{analyserPath}",
                    "Application Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch WindowWatcherAnalyser:\n{ex.Message}",
                "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewScreenshotsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string currPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty) ?? string.Empty;
            string screenshotViewerPath = Path.Combine(currPath, "ScreenshotViewer.exe");

            if (File.Exists(screenshotViewerPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = screenshotViewerPath,
                    UseShellExecute = true
                });
            }
            else
            {
                MessageBox.Show($"ScreenshotViewer.exe not found at:\n{screenshotViewerPath}",
                    "Application Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to launch ScreenshotViewer:\n{ex.Message}",
                "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        TrayIcon.Dispose();
    }

    public MainViewModel ViewModel => _viewModel;
}
