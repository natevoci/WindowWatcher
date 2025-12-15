namespace WindowWatcher.Common.Models;

public class AppSettings
{
    public string SnapshotsPath { get; set; } = @".\Screenshots";
    public string DataDirectory { get; set; } = @".\ActivityLogs";
    public bool MinimizeOnStartup { get; set; } = true;
    public int IdleTimeoutSeconds { get; set; } = 300;
    public int LogRetentionDays { get; set; } = 90;
}
