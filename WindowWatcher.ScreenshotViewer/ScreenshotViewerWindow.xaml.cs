using System.Windows;
using System.Windows.Input;
using WindowWatcher.ScreenshotViewer.ViewModels;
using WindowWatcher.Common.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace WindowWatcher.ScreenshotViewer;

public partial class ScreenshotViewerWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private readonly LoggingService _loggingService;
    private bool _isDragging;
    private Point _lastMousePosition;
    private Point _mouseDownPosition;
    private double _scrollStartX;
    private double _scrollStartY;
    private System.Diagnostics.Stopwatch _playTestStopwatch = new System.Diagnostics.Stopwatch();

    public ScreenshotViewerWindow(string? screenshotsFolder = null)
    {
        InitializeComponent();
#if DEBUG
        PlayTestCacheButton.Visibility = Visibility.Visible;
        PlayTestRenderButton.Visibility = Visibility.Visible;
#endif
        _settingsService = new SettingsService();
        _loggingService = new LoggingService(_settingsService);
        _viewModel = new MainViewModel(_settingsService, screenshotsFolder);
        DataContext = _viewModel;

        // Add keyboard bindings
        KeyDown += ScreenshotViewerWindow_KeyDown;

        // Subscribe to property changes to reset zoom when image changes
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.IsZoomed))
        {
            UpdateImageZoom();
        }
    }

    private void UpdateImageZoom()
    {
        if (_viewModel.IsZoomed)
        {
            // In zoom mode, set the image to display at actual pixel size (1:1)
            var imageSource = _viewModel.CurrentImageSource;
            if (imageSource != null)
            {
                MainImage.Width = imageSource.PixelWidth;
                MainImage.Height = imageSource.PixelHeight;
            }
            MainImage.Stretch = System.Windows.Media.Stretch.None;
            MainImageScrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
            MainImageScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto;
        }
        else
        {
            // In non-zoom mode, clear explicit dimensions so Stretch.Fill works
            MainImage.Width = double.NaN;
            MainImage.Height = double.NaN;
            MainImage.Stretch = System.Windows.Media.Stretch.Fill;
            MainImageScrollViewer.ScrollToHorizontalOffset(0);
            MainImageScrollViewer.ScrollToVerticalOffset(0);
            MainImageScrollViewer.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
            MainImageScrollViewer.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
        }
    }

    private void ScreenshotViewerWindow_KeyDown(object sender, KeyEventArgs e)
    {
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        switch (e.Key)
        {
            case Key.Left:
                if (shift)
                    _viewModel.JumpBackward30Seconds();
                else
                    _viewModel.PreviousImage();
                e.Handled = true;
                break;

            case Key.Right:
                if (shift)
                    _viewModel.JumpForward30Seconds();
                else
                    _viewModel.NextImage();
                e.Handled = true;
                break;
        }
    }

    private void MainImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPosition = e.GetPosition(ImageContainer);
        _lastMousePosition = _mouseDownPosition;

        if (_viewModel.IsZoomed && _viewModel.CurrentImageSource != null)
        {
            _isDragging = false;
            MainImage.CaptureMouse();
            _scrollStartX = MainImageScrollViewer.HorizontalOffset;
            _scrollStartY = MainImageScrollViewer.VerticalOffset;
        }
        else if (_viewModel.CurrentImageSource != null)
        {
            MainImage.CaptureMouse();
        }
    }

    private void MainImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel.IsZoomed && MainImage.IsMouseCaptured)
        {
            Point currentPosition = e.GetPosition(ImageContainer);
            double deltaX = currentPosition.X - _lastMousePosition.X;
            double deltaY = currentPosition.Y - _lastMousePosition.Y;

            // Start dragging if we've moved at least 2 pixels
            if (!_isDragging && (Math.Abs(deltaX) > 2 || Math.Abs(deltaY) > 2))
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                double newX = _scrollStartX - (currentPosition.X - _mouseDownPosition.X);
                double newY = _scrollStartY - (currentPosition.Y - _mouseDownPosition.Y);
                MainImageScrollViewer.ScrollToHorizontalOffset(newX);
                MainImageScrollViewer.ScrollToVerticalOffset(newY);
            }
        }
    }

    private void MainImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (MainImage.IsMouseCaptured)
        {
            MainImage.ReleaseMouseCapture();
        }

        if (_isDragging)
        {
            _isDragging = false;
        }
        else
        {
            // No drag occurred - this is a click, so toggle zoom
            Point mouseUpPosition = e.GetPosition(ImageContainer);
            double distance = Math.Sqrt(
                Math.Pow(mouseUpPosition.X - _mouseDownPosition.X, 2) +
                Math.Pow(mouseUpPosition.Y - _mouseDownPosition.Y, 2));

            // Only toggle zoom if the mouse hasn't moved significantly (less than 5 pixels)
            if (distance < 5)
            {
                _viewModel.ToggleZoom();
            }
        }
    }

    private void PrevDayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedDate == null)
            return;
        var availableDates = _viewModel
            .GetType()
            .GetField("_snapshotService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_viewModel) as WindowWatcher.ScreenshotViewer.Services.SnapshotService;
        if (availableDates == null)
            return;
        var dates = availableDates.GetAvailableDates().OrderBy(d => d).ToList();
        int idx = dates.IndexOf(_viewModel.SelectedDate.Value);
        if (idx > 0)
        {
            _viewModel.SelectedDate = dates[idx - 1];
        }
    }

    private void NextDayButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedDate == null)
            return;
        var availableDates = _viewModel
            .GetType()
            .GetField("_snapshotService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(_viewModel) as WindowWatcher.ScreenshotViewer.Services.SnapshotService;
        if (availableDates == null)
            return;
        var dates = availableDates.GetAvailableDates().OrderBy(d => d).ToList();
        int idx = dates.IndexOf(_viewModel.SelectedDate.Value);
        if (idx >= 0 && idx < dates.Count - 1)
        {
            _viewModel.SelectedDate = dates[idx + 1];
        }
    }

    private void PlayTestCacheButton_Click(object sender, RoutedEventArgs e)
    {
        PlayTestCacheButton.IsEnabled = false;

        _playTestStopwatch = System.Diagnostics.Stopwatch.StartNew();

        var loadedTestImages = new List<System.Windows.Media.Imaging.BitmapImage>();

        // Get the next 100 snapshots starting from current index
        int start = _viewModel.CurrentIndex;
        int endIndex = Math.Min(start + 100, _viewModel.Snapshots.Count);
        int count = endIndex - start;

        if (count <= 0)
        {
            MessageBox.Show("No more snapshots to play.", "Play Test", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _loggingService.LogInformation($"{_playTestStopwatch.ElapsedMilliseconds} - Starting Play Test Cache... {count} images");

        for (int i = start; i < endIndex; i++)
        {
            var filePath = _viewModel.Snapshots[i].FilePath;
            var image = _viewModel.ImageCache.LoadImage(filePath);
            if (image != null)
            {
                loadedTestImages.Add(image);
            }
        }

        _loggingService.LogInformation($"{_playTestStopwatch.ElapsedMilliseconds} - Images loaded...");

        MessageBox.Show($"Loaded {loadedTestImages.Count} images into cache in {_playTestStopwatch.ElapsedMilliseconds} ms.", "Play Test", MessageBoxButton.OK, MessageBoxImage.Information);

        PlayTestCacheButton.IsEnabled = true;
    }

    private async void PlayTestRenderButton_Click(object sender, RoutedEventArgs e)
    {
        // Disable the button during playback
        PlayTestRenderButton.IsEnabled = false;

        int start = _viewModel.CurrentIndex;
        int endIndex = Math.Min(start + 100, _viewModel.Snapshots.Count);

        try
        {
            _playTestStopwatch = System.Diagnostics.Stopwatch.StartNew();

            _loggingService.LogInformation($"{_playTestStopwatch.ElapsedMilliseconds} - Starting Play Test Render... {endIndex - start} frames");

            // Run the rendering loop on a background thread
            await Task.Run(async () =>
            {
                // Play through the images
                for (int i = start; i < endIndex; i++)
                {
                    // Update the UI on the UI thread
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _viewModel.CurrentIndex = i;
                    }, System.Windows.Threading.DispatcherPriority.Normal);

                    // Wait on the background thread
                    await Task.Delay(25);
                }
            });

            _loggingService.LogInformation($"{_playTestStopwatch.ElapsedMilliseconds} - Images rendered...");

            MessageBox.Show($"Rendered {endIndex - start} images in {_playTestStopwatch.ElapsedMilliseconds} ms.", "Play Test", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            // Re-enable the button
            PlayTestRenderButton.IsEnabled = true;
        }
    }

    private void ViewerMenuButton_Click(object sender, RoutedEventArgs e)
    {
        ViewerMenu.PlacementTarget = ViewerMenuButton;
        ViewerMenu.IsOpen = true;
    }

    private void OpenScreenshotsFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            InitialDirectory = _viewModel.SnapshotsFolderPath
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.SetSnapshotsFolder(dialog.FolderName);
        }
    }

    private void CopyImageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentImageSource != null)
        {
            Clipboard.SetImage(_viewModel.CurrentImageSource);
        }
    }

    private void CopyImageFilenameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.CurrentImageFilePath != null)
        {
            Clipboard.SetText(_viewModel.CurrentImageFilePath);
        }
    }

    private void CopyScreenshotsFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_viewModel.SnapshotsFolderPath);
    }
}
