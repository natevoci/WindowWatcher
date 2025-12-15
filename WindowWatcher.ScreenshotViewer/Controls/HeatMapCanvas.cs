using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WindowWatcher.ScreenshotViewer.Controls;

// Separate top-level class for the heat map canvas
public class HeatMapCanvas : FrameworkElement
{
    private const double ThumbWidth = 15.0; // Must match the thumb width in the style
    
    public TimelineSlider? TimelineSlider { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (TimelineSlider?.Snapshots == null || TimelineSlider.Snapshots.Count == 0)
            return;

        DrawHeatMap(dc);
    }

    private void DrawHeatMap(DrawingContext dc)
    {
        if (TimelineSlider == null || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var snapshots = TimelineSlider.Snapshots;
        if (snapshots == null)
            return;
        if (snapshots.Count == 0)
            return;

        // Account for half the thumb width on each side
        // The thumb's center aligns with min/max positions, so we need to offset by half its width
        double thumbHalfWidth = ThumbWidth / 2.0;
        
        // Reserve space at the bottom for the hour labels (20 pixels)
        const double labelHeight = 15;
        var trackRect = new Rect(thumbHalfWidth, 0, ActualWidth - ThumbWidth, ActualHeight - labelHeight);

        // Calculate time range based on first and last snapshot
        var startTime = snapshots[0].TimeOfDay;
        var endTime = snapshots[^1].TimeOfDay;
        var timeRange = endTime - startTime;
        
        // Handle edge case where there's only one snapshot or very small range
        if (timeRange.TotalSeconds < 1)
            timeRange = TimeSpan.FromHours(1); // Default to 1 hour range for display

        var totalSeconds = timeRange.TotalSeconds;

        // Draw hour markers that fall within the time range
        var hourPen = new Pen(Brushes.White, 2);
        var hourTextBrush = Brushes.White;
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        
        // Find the first hour marker to display (round up to next hour)
        var firstHour = (int)Math.Ceiling(startTime.TotalHours);
        var lastHour = (int)Math.Floor(endTime.TotalHours);
        
        for (int hour = firstHour; hour <= lastHour; hour++)
        {
            var hourTime = TimeSpan.FromHours(hour);
            var position = ((hourTime - startTime).TotalSeconds / totalSeconds) * trackRect.Width;
            var x = trackRect.Left + position;
            
            // Draw vertical line for hour marker (shorter to make room for label)
            dc.DrawLine(hourPen, 
                new Point(x, trackRect.Top + (trackRect.Height * 3 / 4)), 
                new Point(x, trackRect.Bottom));
            
            // Draw hour label below the track
            var hourText = hour.ToString("00") + ":00";
            var formattedText = new FormattedText(
                hourText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                20,
                hourTextBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            
            // Position text below the track rect with some padding
            dc.DrawText(formattedText, new Point(x - formattedText.Width / 2, trackRect.Bottom));
        }

        // Draw vertical line for each snapshot
        var brush = new SolidColorBrush(Colors.White) { Opacity = 0.3 };
        var pen = new Pen(brush, 1);
        
        foreach (var snapshot in snapshots)
        {
            var position = ((snapshot.TimeOfDay - startTime).TotalSeconds / totalSeconds) * trackRect.Width;
            var x = trackRect.Left + position;
            
            dc.DrawLine(pen, 
                new Point(x, trackRect.Top), 
                new Point(x, trackRect.Bottom));
        }
    }
}
