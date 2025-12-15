using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;

namespace WindowWatcher.ScreenshotViewer.Services;

public class ImageCacheService
{
    private readonly ConcurrentDictionary<string, BitmapImage> _cache = new();
    private readonly Queue<string> _accessQueue = new();
    private readonly object _lockObject = new();
    private long _currentCacheSize = 0;
    private const long MaxCacheSizeBytes = 500 * 1024 * 1024; // 500 MB
    private CancellationTokenSource? _preloadCancellation;

    public async Task<BitmapImage?> LoadImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        // Check cache first
        if (_cache.TryGetValue(filePath, out var cachedImage))
        {
            UpdateAccessQueue(filePath);
            return cachedImage;
        }

        // Check if cancelled before loading
        if (cancellationToken.IsCancellationRequested)
            return null;

        // Load image
        var image = await Task.Run(() => LoadImage(filePath), cancellationToken);
        
        if (image != null && !cancellationToken.IsCancellationRequested)
        {
            CacheImage(filePath, image);
        }

        return image;
    }

    public async Task PreloadAdjacentImages(List<string> filePaths, int currentIndex, int preloadCount = 3)
    {
        // Cancel any previous preload operation
        _preloadCancellation?.Cancel();
        _preloadCancellation?.Dispose();
        _preloadCancellation = new CancellationTokenSource();
        
        var cancellationToken = _preloadCancellation.Token;
        
        try
        {
            var tasks = new List<Task>();
            
            // Preload previous images
            for (int i = Math.Max(0, currentIndex - preloadCount); i < currentIndex; i++)
            {
                if (i >= 0 && i < filePaths.Count && !cancellationToken.IsCancellationRequested)
                {
                    tasks.Add(LoadImageAsync(filePaths[i], cancellationToken));
                }
            }
            
            // Preload next images
            for (int i = currentIndex + 1; i <= Math.Min(filePaths.Count - 1, currentIndex + preloadCount); i++)
            {
                if (i >= 0 && i < filePaths.Count && !cancellationToken.IsCancellationRequested)
                {
                    tasks.Add(LoadImageAsync(filePaths[i], cancellationToken));
                }
            }
            
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled, ignore
        }
    }

    public BitmapImage? LoadImage(string filePath)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.None;
            //bitmap.DecodePixelWidth = 1920;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze(); // Make it thread-safe
            
            return bitmap;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load image {filePath}: {ex.Message}");
            return null;
        }
    }

    private void CacheImage(string filePath, BitmapImage image)
    {
        lock (_lockObject)
        {
            if (_cache.ContainsKey(filePath))
                return;

            long imageSize = EstimateImageSize(image);
            
            // Evict old images if needed
            while (_currentCacheSize + imageSize > MaxCacheSizeBytes && _accessQueue.Count > 0)
            {
                var oldestKey = _accessQueue.Dequeue();
                if (_cache.TryRemove(oldestKey, out var oldImage))
                {
                    _currentCacheSize -= EstimateImageSize(oldImage);
                }
            }
            
            // Add new image
            if (_cache.TryAdd(filePath, image))
            {
                _currentCacheSize += imageSize;
                _accessQueue.Enqueue(filePath);
            }
        }
    }

    private void UpdateAccessQueue(string filePath)
    {
        lock (_lockObject)
        {
            // Remove from queue and re-add to mark as recently used
            var tempQueue = new Queue<string>(_accessQueue.Where(k => k != filePath));
            tempQueue.Enqueue(filePath);
            _accessQueue.Clear();
            foreach (var key in tempQueue)
            {
                _accessQueue.Enqueue(key);
            }
        }
    }

    private long EstimateImageSize(BitmapImage image)
    {
        // Rough estimate: width * height * 4 bytes per pixel (RGBA)
        return image.PixelWidth * image.PixelHeight * 4L;
    }

    public void Clear()
    {
        lock (_lockObject)
        {
            _preloadCancellation?.Cancel();
            _preloadCancellation?.Dispose();
            _preloadCancellation = null;
            
            _cache.Clear();
            _accessQueue.Clear();
            _currentCacheSize = 0;
        }
    }
}
