using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WindowWatcher.ScreenshotViewer.Models;

namespace WindowWatcher.ScreenshotViewer.Controls;

public class TimelineSlider : Slider
{
    public static readonly DependencyProperty ScreenshotsProperty =
        DependencyProperty.Register(nameof(Screenshots), typeof(List<ScreenshotInfo>),
            typeof(TimelineSlider), new FrameworkPropertyMetadata(null, 
                FrameworkPropertyMetadataOptions.AffectsRender, OnScreenshotsChanged));

    private HeatMapCanvas? _heatMapCanvas;
    private Track? _track;
    private Thumb? _thumb;

    public List<ScreenshotInfo>? Screenshots
    {
        get => (List<ScreenshotInfo>?)GetValue(ScreenshotsProperty);
        set => SetValue(ScreenshotsProperty, value);
    }

    static TimelineSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TimelineSlider),
            new FrameworkPropertyMetadata(typeof(TimelineSlider)));
    }

    public TimelineSlider()
    {
        IsSnapToTickEnabled = true;
        IsMoveToPointEnabled = true;
        ClipToBounds = false;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        
        // Get reference to the heat map canvas from the template
        _heatMapCanvas = GetTemplateChild("PART_HeatMap") as HeatMapCanvas;
        if (_heatMapCanvas != null)
        {
            _heatMapCanvas.TimelineSlider = this;
        }

        // Get references to track and thumb for drag behavior fix
        _track = GetTemplateChild("PART_Track") as Track;
        if (_track != null)
        {
            _thumb = _track.Thumb;
        }
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        if (_thumb == null || _track == null)
            return;

        // Check if click was on the track area (not on the thumb)
        var clickPosition = e.GetPosition(_track);
        var thumbPosition = _thumb.TransformToAncestor(_track).Transform(new Point(0, 0));
        var thumbRect = new Rect(thumbPosition, new Size(_thumb.ActualWidth, _thumb.ActualHeight));

        if (!thumbRect.Contains(clickPosition))
        {
            // Click was on track, not thumb - we need to initiate drag after the jump
            // Use Dispatcher to ensure IsMoveToPointEnabled has moved the thumb first
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_thumb != null && Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    // Start the thumb drag
                    var thumbCenter = new Point(_thumb.ActualWidth / 2, _thumb.ActualHeight / 2);
                    var mouseDownEvent = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                    {
                        RoutedEvent = Mouse.MouseDownEvent
                    };
                    
                    _thumb.RaiseEvent(mouseDownEvent);
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private static void OnScreenshotsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimelineSlider slider)
        {
            slider.UpdateTicksAndRange();
            slider._heatMapCanvas?.InvalidateVisual();
        }
    }

    private void UpdateTicksAndRange()
    {
        if (Screenshots == null || Screenshots.Count == 0)
        {
            Ticks.Clear();
            Minimum = 0;
            Maximum = 0;
            return;
        }

        // Use time-based values instead of indices
        var startTime = Screenshots[0].TimeOfDay;
        var endTime = Screenshots[^1].TimeOfDay;
        
        Minimum = startTime.TotalSeconds;
        Maximum = endTime.TotalSeconds;

        // Set ticks to actual time values
        Ticks.Clear();
        foreach (var screenshot in Screenshots)
        {
            Ticks.Add(screenshot.TimeOfDay.TotalSeconds);
        }

        // Update current value to match current index if needed
        UpdateValueFromExternalChange();
    }

    // Call this when the value changes externally (from binding)
    private void UpdateValueFromExternalChange()
    {
        // This will be called during initialization/screenshot changes
        // The Value binding will handle normal updates
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
    }

    // Helper method to convert time value to screenshot index
    public int GetScreenshotIndexFromTimeValue(double timeValue)
    {
        if (Screenshots == null || Screenshots.Count == 0)
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

    // Helper method to convert screenshot index to time value
    public double GetTimeValueFromScreenshotIndex(int index)
    {
        if (Screenshots == null || Screenshots.Count == 0 || index < 0 || index >= Screenshots.Count)
            return 0;

        return Screenshots[index].TimeOfDay.TotalSeconds;
    }
}
