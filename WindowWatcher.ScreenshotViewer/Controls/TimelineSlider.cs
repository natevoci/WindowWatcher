using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WindowWatcher.ScreenshotViewer.Models;

namespace WindowWatcher.ScreenshotViewer.Controls;

public class TimelineSlider : Slider
{
    public static readonly DependencyProperty SnapshotsProperty =
        DependencyProperty.Register(nameof(Snapshots), typeof(List<SnapshotInfo>),
            typeof(TimelineSlider), new FrameworkPropertyMetadata(null, 
                FrameworkPropertyMetadataOptions.AffectsRender, OnSnapshotsChanged));

    private HeatMapCanvas? _heatMapCanvas;
    private Track? _track;
    private Thumb? _thumb;

    public List<SnapshotInfo>? Snapshots
    {
        get => (List<SnapshotInfo>?)GetValue(SnapshotsProperty);
        set => SetValue(SnapshotsProperty, value);
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

    private static void OnSnapshotsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimelineSlider slider)
        {
            slider.UpdateTicksAndRange();
            slider._heatMapCanvas?.InvalidateVisual();
        }
    }

    private void UpdateTicksAndRange()
    {
        if (Snapshots == null || Snapshots.Count == 0)
        {
            Ticks.Clear();
            Minimum = 0;
            Maximum = 0;
            return;
        }

        // Use time-based values instead of indices
        var startTime = Snapshots[0].TimeOfDay;
        var endTime = Snapshots[^1].TimeOfDay;
        
        Minimum = startTime.TotalSeconds;
        Maximum = endTime.TotalSeconds;

        // Set ticks to actual time values
        Ticks.Clear();
        foreach (var snapshot in Snapshots)
        {
            Ticks.Add(snapshot.TimeOfDay.TotalSeconds);
        }

        // Update current value to match current index if needed
        UpdateValueFromExternalChange();
    }

    // Call this when the value changes externally (from binding)
    private void UpdateValueFromExternalChange()
    {
        // This will be called during initialization/snapshot changes
        // The Value binding will handle normal updates
    }

    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
    }

    // Helper method to convert time value to snapshot index
    public int GetSnapshotIndexFromTimeValue(double timeValue)
    {
        if (Snapshots == null || Snapshots.Count == 0)
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

    // Helper method to convert snapshot index to time value
    public double GetTimeValueFromSnapshotIndex(int index)
    {
        if (Snapshots == null || Snapshots.Count == 0 || index < 0 || index >= Snapshots.Count)
            return 0;

        return Snapshots[index].TimeOfDay.TotalSeconds;
    }
}
