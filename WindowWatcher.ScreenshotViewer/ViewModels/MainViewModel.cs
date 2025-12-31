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
    private readonly SnapshotService _snapshotService;
    private readonly ImageCacheService _imageCache;
    private readonly SettingsService _settingsService;
    
    private DateTime? _selectedDate;
    private List<SnapshotInfo> _snapshots = new();
    private int _currentIndex;
    private BitmapImage? _currentImageSource;
    private bool _isZoomed;
    private int _loadRequestCounter = 0;

    public MainViewModel(SettingsService settingsService, string? screenshotsFolder = null)
    {
        _settingsService = settingsService;
        _snapshotService = new SnapshotService(_settingsService);
        _imageCache = new ImageCacheService();

        if (!string.IsNullOrWhiteSpace(screenshotsFolder))
        {
            _snapshotService.SetSnapshotsPathOverride(screenshotsFolder);
        }
        
        // Initialize with the most recent date
        var availableDates = _snapshotService.GetAvailableDates();
        if (availableDates.Count > 0)
        {
            SelectedDate = availableDates[0];
        }
    }

    public void RefreshData()
    {
        // Reload available dates and reset the view
        var availableDates = _snapshotService.GetAvailableDates();
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
                LoadSnapshotsForSelectedDate();
            }
        }
    }

    public List<SnapshotInfo> Snapshots
    {
        get => _snapshots;
        private set
        {
            _snapshots = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MaxIndex));
            OnPropertyChanged(nameof(MaxTimeValue));
            OnPropertyChanged(nameof(CurrentTimeValue));
        }
    }

    public int MaxIndex => Math.Max(0, Snapshots.Count - 1);

    // Time-based value for slider (in seconds)
    public double CurrentTimeValue
    {
        get
        {
            if (Snapshots.Count == 0 || CurrentIndex < 0 || CurrentIndex >= Snapshots.Count)
                return 0;
            return Snapshots[CurrentIndex].TimeOfDay.TotalSeconds;
        }
        set
        {
            // Find the snapshot index that corresponds to this time value
            if (Snapshots.Count == 0)
                return;

            int newIndex = FindClosestSnapshotIndex(value);
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
            if (Snapshots.Count == 0)
                return 0;
            return Snapshots[^1].TimeOfDay.TotalSeconds;
        }
    }

    private int FindClosestSnapshotIndex(double timeValue)
    {
        if (Snapshots.Count == 0)
            return 0;

        // Find the closest snapshot to the time value
        int closestIndex = 0;
        double closestDiff = Math.Abs(Snapshots[0].TimeOfDay.TotalSeconds - timeValue);

        for (int i = 1; i < Snapshots.Count; i++)
        {
            double diff = Math.Abs(Snapshots[i].TimeOfDay.TotalSeconds - timeValue);
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
            if (_currentIndex != value && value >= 0 && value < Snapshots.Count)
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
            if (Snapshots.Count == 0 || CurrentIndex < 0 || CurrentIndex >= Snapshots.Count)
                return string.Empty;
            
            var snapshot = Snapshots[CurrentIndex];
            return snapshot.TimeOfDay.ToString(@"hh\:mm\:ss");
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

    private void LoadSnapshotsForSelectedDate()
    {
        if (_selectedDate == null)
        {
            Snapshots = new List<SnapshotInfo>();
            _currentIndex = -1;
            CurrentImageSource = null;
            return;
        }

        _imageCache.Clear();
        Snapshots = _snapshotService.GetSnapshotsForDate(_selectedDate.Value);
        _currentIndex = -1;
        
        if (Snapshots.Count > 0)
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
        if (CurrentIndex < 0 || CurrentIndex >= Snapshots.Count)
        {
            CurrentImageSource = null;
            return;
        }

        // Increment counter to invalidate any pending loads
        var currentLoadRequest = ++_loadRequestCounter;
        
        var snapshot = Snapshots[CurrentIndex];
        var image = await _imageCache.LoadImageAsync(snapshot.FilePath);
        
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
        if (Snapshots.Count == 0)
            return;

        var currentTime = Snapshots[CurrentIndex].TimeOfDay;
        var targetTime = currentTime - TimeSpan.FromSeconds(30);
        
        var snapshot = _snapshotService.FindSnapshotBeforeTime(Snapshots, targetTime);
        
        if (snapshot != null)
        {
            CurrentIndex = snapshot.Index;
        }
        else
        {
            // No image before target time, go to first image
            CurrentIndex = 0;
        }
    }

    public void JumpForward30Seconds()
    {
        if (Snapshots.Count == 0)
            return;

        var currentTime = Snapshots[CurrentIndex].TimeOfDay;
        var targetTime = currentTime + TimeSpan.FromSeconds(30);
        
        var snapshot = _snapshotService.FindSnapshotAfterTime(Snapshots, targetTime);
        
        if (snapshot != null)
        {
            CurrentIndex = snapshot.Index;
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

    public string SnapshotsFolderPath => _snapshotService.SnapshotsPath;

    public string? CurrentImageFilePath => CurrentIndex >= 0 && CurrentIndex < Snapshots.Count
        ? Snapshots[CurrentIndex].FilePath
        : null;

    public void SetSnapshotsFolder(string snapshotsPath)
    {
        _snapshotService.SetSnapshotsPathOverride(snapshotsPath);
        RefreshData();
        OnPropertyChanged(nameof(SnapshotsFolderPath));
    }

    public ImageCacheService ImageCache => _imageCache;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
