using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WindowWatcher.Common.Models;

namespace WindowWatcherDaemon;

public class ViewEventsViewModel : INotifyPropertyChanged
{
    private ObservableCollection<ActivityDisplayItem> _recentActivity = new();

    public ObservableCollection<ActivityDisplayItem> RecentActivity
    {
        get => _recentActivity;
        set
        {
            _recentActivity = value;
            OnPropertyChanged();
        }
    }

    public void AddActivity(WindowActivityEvent evt)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            RecentActivity.Add(new ActivityDisplayItem(evt));
            while (RecentActivity.Count > 100)
            {
                RecentActivity.RemoveAt(0);
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class ActivityDisplayItem
{
    public DateTime Date { get; set; }
    public string ProcessDisplay { get; set; }
    public string WindowTitleDisplay { get; set; }

    public ActivityDisplayItem(WindowActivityEvent evt)
    {
        Date = evt.Date;

        if (evt.EventType == WindowActivityEventType.WindowActivated)
        {
            ProcessDisplay = evt.ProcessName;
            WindowTitleDisplay = evt.WindowTitle;
        }
        else
        {
            ProcessDisplay = $"<{evt.EventType.ToString().ToLower()}>";
            WindowTitleDisplay = string.Empty;
        }
    }
}
