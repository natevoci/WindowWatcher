using System.Windows.Forms;

namespace WindowWatcher.Daemon;

public sealed class UserActivityService : IDisposable
{
    private readonly object _sync = new();
    private UserActivityHook? _activityHook;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private bool _started;

    public DateTime LastActivityUtc
    {
        get
        {
            lock (_sync)
            {
                return _lastActivityUtc;
            }
        }
    }

    public event EventHandler<DateTime>? ActivityDetected;

    public void Start()
    {
        lock (_sync)
        {
            if (_started)
            {
                return;
            }

            _activityHook = new UserActivityHook(true, true);
            _activityHook.KeyPress += ActivityHookOnKeyPress;
            _activityHook.OnMouseActivity += ActivityHookOnMouseActivity;
            _lastActivityUtc = DateTime.UtcNow;
            _started = true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!_started)
            {
                return;
            }

            if (_activityHook != null)
            {
                _activityHook.KeyPress -= ActivityHookOnKeyPress;
                _activityHook.OnMouseActivity -= ActivityHookOnMouseActivity;
                _activityHook.Stop();
                _activityHook = null;
            }

            _started = false;
        }
    }

    public bool HasActivitySince(DateTime utcTimestamp)
    {
        return LastActivityUtc > utcTimestamp;
    }

    private void ActivityHookOnKeyPress(object? sender, KeyPressEventArgs e)
    {
        MarkActivity();
    }

    private void ActivityHookOnMouseActivity(object? sender, MouseEventArgs e)
    {
        MarkActivity();
    }

    private void MarkActivity()
    {
        DateTime timestamp;
        lock (_sync)
        {
            _lastActivityUtc = DateTime.UtcNow;
            timestamp = _lastActivityUtc;
        }

        ActivityDetected?.Invoke(this, timestamp);
    }

    public void Dispose()
    {
        Stop();
    }
}
