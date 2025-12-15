namespace WindowWatcher.Common.Models;

public enum WindowActivityEventType
{
    None,
    WindowActivated,
    Inactive,
    Exiting
}

public class WindowActivityEvent
{
    public DateTime Date { get; set; } = DateTime.Now;
    public WindowActivityEventType EventType { get; set; } = WindowActivityEventType.None;
    public string ProcessName { get; set; } = string.Empty;
    public string WindowTitle { get; set; } = string.Empty;
    public double DurationSeconds { get; set; } = 0.0;

    public WindowActivityEvent()
    {
    }

    public WindowActivityEvent(DateTime date, WindowActivityEventType eventType, string processName, string windowTitle)
    {
        Date = date;
        EventType = eventType;
        ProcessName = processName;
        WindowTitle = windowTitle;
    }

    public string ToCsvString()
    {
        string date = Date.ToString("yyyy/MM/dd HH:mm:ss.fff zzz");
        var result = new System.Text.StringBuilder();
        result.Append(date + "," + EventType.ToString());

        if (!string.IsNullOrEmpty(ProcessName))
        {
            result.Append(",\"").Append(ProcessName.Replace("\"", "\\\"")).Append("\"");

            if (!string.IsNullOrEmpty(WindowTitle))
                result.Append(",\"").Append(WindowTitle.Replace("\"", "\\\"")).Append("\"");
        }
        return result.ToString();
    }
}
