using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WindowWatcher.Common.Models;

namespace WindowWatcher.Analyser.Controls;

public class TimelineCanvas : FrameworkElement
{
    private readonly List<DrawingVisual> _visuals = new();
    private const double OffsetY = 40.0;
    private const double RowHeight = 24.0;

    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.Register(nameof(Events), typeof(ObservableCollection<WindowActivityEvent>),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnEventsChanged));

    public static readonly DependencyProperty StartTimeProperty =
        DependencyProperty.Register(nameof(StartTime), typeof(DateTime),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsRender, OnTimeChanged));

    public static readonly DependencyProperty EndTimeProperty =
        DependencyProperty.Register(nameof(EndTime), typeof(DateTime),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(DateTime.MinValue, FrameworkPropertyMetadataOptions.AffectsRender, OnTimeChanged));

    public static readonly DependencyProperty RowsProperty =
        DependencyProperty.Register(nameof(Rows), typeof(ObservableCollection<string>),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnRowsChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(int),
            typeof(TimelineCanvas), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public ObservableCollection<WindowActivityEvent>? Events
    {
        get => (ObservableCollection<WindowActivityEvent>?)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public DateTime StartTime
    {
        get => (DateTime)GetValue(StartTimeProperty);
        set => SetValue(StartTimeProperty, value);
    }

    public DateTime EndTime
    {
        get => (DateTime)GetValue(EndTimeProperty);
        set => SetValue(EndTimeProperty, value);
    }

    public ObservableCollection<string>? Rows
    {
        get => (ObservableCollection<string>?)GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    public int ScrollOffset
    {
        get => (int)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    private static void OnEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimelineCanvas canvas)
        {
            canvas.RenderTimeline();
        }
    }

    private static void OnTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimelineCanvas canvas)
        {
            canvas.RenderTimeline();
        }
    }

    private static void OnRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimelineCanvas canvas)
        {
            canvas.UpdateHeight();
            canvas.RenderTimeline();
        }
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index)
    {
        if (index < 0 || index >= _visuals.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _visuals[index];
    }

    private void UpdateHeight()
    {
        if (Rows != null)
        {
            Height = OffsetY + (Rows.Count * RowHeight) + 20;
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        RenderTimeline();
    }

    private void RenderTimeline()
    {
        // Clear existing visuals
        foreach (var oldVisual in _visuals)
        {
            RemoveVisualChild(oldVisual);
        }
        _visuals.Clear();

        if (Events == null || Rows == null || StartTime == DateTime.MinValue || EndTime == DateTime.MinValue)
            return;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var width = ActualWidth > 0 ? ActualWidth : 1000;
            var height = ActualHeight > 0 ? ActualHeight : 500;

            // Fill background
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));

            var windowTimeSpan = EndTime - StartTime;
            if (windowTimeSpan.TotalSeconds == 0)
                return;

            var pixelsPerSecond = width / windowTimeSpan.TotalSeconds;

            // Draw time scale
            DrawTimeScale(dc, width, height, windowTimeSpan, pixelsPerSecond);

            // Draw events
            DrawEvents(dc, width, pixelsPerSecond);

            DrawRowTitles(dc, width);
        }

        AddVisualChild(visual);
        _visuals.Add(visual);
    }

    private void DrawTimeScale(DrawingContext dc, double width, double height, TimeSpan windowTimeSpan, double pixelsPerSecond)
    {
        var typeface = new Typeface("Tahoma");
        var dashedPen = new Pen(Brushes.Gray, 1) { DashStyle = DashStyles.Dash };

        var currHour = new DateTime(StartTime.Year, StartTime.Month, StartTime.Day, StartTime.Hour, 0, 0);
        if (StartTime.Minute > 0 || StartTime.Second > 0)
            currHour = currHour.AddHours(1);

        while (currHour < EndTime)
        {
            var ts = currHour - StartTime;
            var x = ts.TotalSeconds * pixelsPerSecond;

            dc.DrawLine(dashedPen, new Point(x, 0), new Point(x, height));

            var formattedText = new FormattedText(
                currHour.ToString("HH:mm"),
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(formattedText, new Point(x, 0));

            currHour = currHour.AddHours(1.0);
        }

        dc.DrawLine(dashedPen, new Point(0, OffsetY), new Point(width, OffsetY));
    }

    private void DrawEvents(DrawingContext dc, double width, double pixelsPerSecond)
    {
        if (Events == null || Rows == null)
            return;

        var windowTimeSpan = EndTime - StartTime;
        var secondsPerPixel = windowTimeSpan.TotalSeconds / width;

        for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
        {
            var rowName = Rows[rowIndex];
            var startY = OffsetY + (rowIndex * RowHeight);

            var pixelData = new Dictionary<int, double>();

            foreach (var evt in Events)
            {
                string processName = evt.EventType == WindowActivityEventType.Inactive ? "Idle" : evt.ProcessName;

                bool matches = false;
                if (rowName == processName)
                    matches = true;
                else if (rowName == $"{processName} - {evt.WindowTitle}")
                    matches = true;

                if (!matches)
                    continue;

                var ts = evt.Date - StartTime;
                var x = ts.TotalSeconds * pixelsPerSecond;
                int pixel = (int)Math.Floor(x);

                if (pixel < 0 || pixel >= width)
                    continue;

                var durationInPixels = evt.DurationSeconds / secondsPerPixel;
                if (!pixelData.ContainsKey(pixel))
                    pixelData[pixel] = 0;
                pixelData[pixel] += durationInPixels;
            }

            // Draw accumulated pixel data
            foreach (var kvp in pixelData)
            {
                var pixel = kvp.Key;
                var percent = Math.Min(1.0, kvp.Value);

                int colorComponent = (int)((127.0 * percent) + 128.0);
                var color = Color.FromRgb((byte)colorComponent, (byte)colorComponent, (byte)colorComponent);
                var brush = new SolidColorBrush(color);

                dc.DrawLine(new Pen(brush, 1), new Point(pixel, startY), new Point(pixel, startY + RowHeight));
            }
        }
    }

    private void DrawRowTitles(DrawingContext dc, double width)
    {
        if (Rows == null)
            return;

        var typeface = new Typeface("Tahoma");
        var dashedPen = new Pen(Brushes.Gray, 1) { DashStyle = DashStyles.Dash };
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
        {
            var startY = OffsetY + (rowIndex * RowHeight);
            var rowName = Rows[rowIndex];
            var formattedText = new FormattedText(
                rowName,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                13,
                Brushes.LightGreen,
                pixelsPerDip);

            dc.DrawLine(dashedPen, new Point(0, startY), new Point(width, startY));
            dc.DrawText(formattedText, new Point(0, startY));
            dc.DrawText(formattedText, new Point(width / 2, startY));
        }
    }
}
