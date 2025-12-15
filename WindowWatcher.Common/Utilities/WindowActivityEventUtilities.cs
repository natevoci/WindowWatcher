using WindowWatcher.Common.Models;

namespace WindowWatcher.Common.Utilities;

public class WindowActivityEventComparer : Comparer<WindowActivityEvent>
{
    public override int Compare(WindowActivityEvent? x, WindowActivityEvent? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        return x.Date.CompareTo(y.Date);
    }
}

public class WindowActivityEventFilter
{
    public DateTime MinDate { get; set; } = DateTime.MinValue;
    public DateTime MaxDate { get; set; } = DateTime.MaxValue;

    public bool Filter(WindowActivityEvent ev)
    {
        if (ev.Date < MinDate)
            return false;
        if (ev.Date > MaxDate)
            return false;

        return true;
    }
}

public class WindowActivityEventGroup
{
    public string Name { get; set; } = string.Empty;
    public double Seconds { get; set; } = 0.0;

    public WindowActivityEventGroup(string name)
    {
        Name = name;
    }
}

public class WindowActivityEventGroupComparer : Comparer<WindowActivityEventGroup>
{
    public override int Compare(WindowActivityEventGroup? x, WindowActivityEventGroup? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;
        return x.Seconds.CompareTo(y.Seconds);
    }
}
