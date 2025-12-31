using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowWatcher.Common.Models;
using WindowWatcher.Common.Services;
using WindowWatcher.ScreenshotViewer.Models;
using WindowWatcher.ScreenshotViewer.Services;

namespace WindowWatcher.ScreenshotViewer.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ScreenshotService _screenshotService;
    private readonly ImageCacheService _imageCache;
    private readonly SettingsService _settingsService;
    
    private DateTime? _selectedDate;
    private List<ScreenshotInfo> _screenshots = new();
    private int _currentIndex;
    private BitmapImage? _currentImageSource;
    private bool _isZoomed;
    private int _loadRequestCounter = 0;

    public MainViewModel(SettingsService settingsService, string? screenshotsFolder = null)
    {
        _settingsService = settingsService;
        _screenshotService = new ScreenshotService(_settingsService);
        _imageCache = new ImageCacheService();

        if (!string.IsNullOrWhiteSpace(screenshotsFolder))
        {
            _screenshotService.SetScreenshotsPathOverride(screenshotsFolder);
        }
        
        // Initialize with the most recent date
        var availableDates = _screenshotService.GetAvailableDates();
        if (availableDates.Count > 0)
        {
            SelectedDate = availableDates[0];
        }
    }

    public void RefreshData()
    {
        // Reload available dates and reset the view
        var availableDates = _screenshotService.GetAvailableDates();
        SelectedDate = null;
        if (availableDates.Count > 0)
        {
            SelectedDate = availableDates[0];
        }
    }

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (_selectedDate != value)
            {
                _selectedDate = value;
                OnPropertyChanged();
                LoadScreenshotsForSelectedDate();
            }
        }
    }

    public List<ScreenshotInfo> Screenshots
    {
        get => _screenshots;
        private set
        {
            _screenshots = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MaxIndex));
            OnPropertyChanged(nameof(MaxTimeValue));
            OnPropertyChanged(nameof(CurrentTimeValue));
        }
    }

    public int MaxIndex => Math.Max(0, Screenshots.Count - 1);

    // Time-based value for slider (in seconds)
    public double CurrentTimeValue
    {
        get
        {
            if (Screenshots.Count == 0 || CurrentIndex < 0 || CurrentIndex >= Screenshots.Count)
                return 0;
            return Screenshots[CurrentIndex].TimeOfDay.TotalSeconds;
        }
        set
        {
            // Find the screenshot index that corresponds to this time value
            if (Screenshots.Count == 0)
                return;

            int newIndex = FindClosestScreenshotIndex(value);
            if (newIndex != _currentIndex)
            {
                CurrentIndex = newIndex;
            }
        }
    }

    public double MaxTimeValue
    {
        get
        {
            if (Screenshots.Count == 0)
                return 0;
            return Screenshots[^1].TimeOfDay.TotalSeconds;
        }
    }

    private int FindClosestScreenshotIndex(double timeValue)
    {
        if (Screenshots.Count == 0)
            return 0;

        // Find the closest screenshot to the time value
        int closestIndex = 0;
        double closestDiff = Math.Abs(Screenshots[0].TimeOfDay.TotalSeconds - timeValue);

        for (int i = 1; i < Screenshots.Count; i++)
        {
            double diff = Math.Abs(Screenshots[i].TimeOfDay.TotalSeconds - timeValue);
            if (diff < closestDiff)
            {
                closestDiff = diff;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    public int CurrentIndex
    {
        get => _currentIndex;
        set
        {
            if (_currentIndex != value && value >= 0 && value < Screenshots.Count)
            {
                _currentIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTimeDisplay));
                OnPropertyChanged(nameof(CurrentTimeValue));
                LoadCurrentImage();
            }
        }
    }

    public BitmapImage? CurrentImageSource
    {
        get => _currentImageSource;
        set
        {
            _currentImageSource = value;
            OnPropertyChanged();
        }
    }

    public string CurrentTimeDisplay
    {
        get
        {
            if (Screenshots.Count == 0 || CurrentIndex < 0 || CurrentIndex >= Screenshots.Count)
                return string.Empty;
            
            var screenshot = Screenshots[CurrentIndex];
            return screenshot.TimeOfDay.ToString(@"hh\:mm\:ss");
        }
    }

    public bool IsZoomed
    {
        get => _isZoomed;
        private set
        {
            _isZoomed = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ImageStretch));
            OnPropertyChanged(nameof(ImageCursor));
        }
    }

    public Stretch ImageStretch => IsZoomed ? Stretch.None : Stretch.Fill;

    public Cursor ImageCursor => IsZoomed ? Cursors.Hand : Cursors.Arrow;

    private void LoadScreenshotsForSelectedDate()
    {
        if (_selectedDate == null)
        {
            Screenshots = new List<ScreenshotInfo>();
            _currentIndex = -1;
            CurrentImageSource = null;
            return;
        }

        _imageCache.Clear();
        Screenshots = _screenshotService.GetScreenshotsForDate(_selectedDate.Value);
        _currentIndex = -1;
        
        if (Screenshots.Count > 0)
        {
            CurrentIndex = 0;
        }
        else
        {
            CurrentImageSource = null;
            OnPropertyChanged(nameof(CurrentTimeDisplay));
        }
    }

    private async void LoadCurrentImage()
    {
        if (CurrentIndex < 0 || CurrentIndex >= Screenshots.Count)
        {
            CurrentImageSource = null;
            return;
        }

        // Increment counter to invalidate any pending loads
        var currentLoadRequest = ++_loadRequestCounter;
        
        var screenshot = Screenshots[CurrentIndex];
        var image = await _imageCache.LoadImageAsync(screenshot.FilePath);
        
        // Only update the image if this is still the latest request
        if (currentLoadRequest == _loadRequestCounter && image != null)
        {
            CurrentImageSource = image;
        }
    }

    public void PreviousImage()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
        }
    }

    public void NextImage()
    {
        if (CurrentIndex < MaxIndex)
        {
            CurrentIndex++;
        }
    }

    public void JumpBackward30Seconds()
    {
        if (Screenshots.Count == 0)
            return;

        var currentTime = Screenshots[CurrentIndex].TimeOfDay;
        var targetTime = currentTime - TimeSpan.FromSeconds(30);
        
        var screenshot = _screenshotService.FindScreenshotBeforeTime(Screenshots, targetTime);
        
        if (screenshot != null)
        {
            CurrentIndex = screenshot.Index;
        }
        else
        {
            // No image before target time, go to first image
            CurrentIndex = 0;
        }
    }

    public void JumpForward30Seconds()
    {
        if (Screenshots.Count == 0)
            return;

        var currentTime = Screenshots[CurrentIndex].TimeOfDay;
        var targetTime = currentTime + TimeSpan.FromSeconds(30);
        
        var screenshot = _screenshotService.FindScreenshotAfterTime(Screenshots, targetTime);
        
        if (screenshot != null)
        {
            CurrentIndex = screenshot.Index;
        }
        else
        {
            // No image after target time, go to last image
            CurrentIndex = MaxIndex;
        }
    }

    public void ToggleZoom()
    {
        IsZoomed = !IsZoomed;
    }

    public string ScreenshotsFolderPath => _screenshotService.ScreenshotsPath;

    public string? CurrentImageFilePath => CurrentIndex >= 0 && CurrentIndex < Screenshots.Count
        ? Screenshots[CurrentIndex].FilePath
        : null;

    public void SetScreenshotsFolder(string screenshotsPath)
    {
        _screenshotService.SetScreenshotsPathOverride(screenshotsPath);
        RefreshData();
        OnPropertyChanged(nameof(ScreenshotsFolderPath));
    }

    public ImageCacheService ImageCache => _imageCache;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
