using System.Diagnostics;
using System.IO;
using System.Windows;

namespace WindowWatcher.Daemon;

public partial class MainWindow : Window
{
    private readonly ViewEventsViewModel _viewEventsViewModel;
    private ViewEventsWindow? _viewEventsWindow;

    public MainWindow()
    {
        InitializeComponent();

        _viewEventsViewModel = new ViewEventsViewModel();
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
        ShowViewEventsWindow();
    }

    private void ViewEventsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ShowViewEventsWindow();
    }

    private void ViewAnalyserMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string currPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty) ?? string.Empty;
            string analyserPath = Path.Combine(currPath, "WindowWatcher.Analyser.exe");

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
                ShowMessageBox($"WindowWatcher.Analyser.exe not found at:\n{analyserPath}",
                    "Application Not Found", MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ShowMessageBox($"Failed to launch WindowWatcher.Analyser:\n{ex.Message}",
                "Launch Error", MessageBoxImage.Error);
        }
    }

    private void ViewScreenshotsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string currPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty) ?? string.Empty;
            string screenshotViewerPath = Path.Combine(currPath, "WindowWatcher.ScreenshotViewer.exe");

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
                ShowMessageBox($"WindowWatcher.ScreenshotViewer.exe not found at:\n{screenshotViewerPath}",
                    "Application Not Found", MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            ShowMessageBox($"Failed to launch WindowWatcher.ScreenshotViewer:\n{ex.Message}",
                "Launch Error", MessageBoxImage.Error);
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
