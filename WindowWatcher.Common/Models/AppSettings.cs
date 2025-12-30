namespace WindowWatcher.Common.Models;

public class AppSettings
{
    public string SnapshotsPath { get; set; } = @".\Screenshots";
    public string DataDirectory { get; set; } = @".\ActivityLogs";
    public bool MinimizeOnStartup { get; set; } = true;
    public int IdleTimeoutSeconds { get; set; } = 300;
    public int LogRetentionDays { get; set; } = 90;
    public int ScreenshotIntervalSeconds { get; set; } = 5;
    public string ScreenshotCaptureTarget { get; set; } = "AllScreens";
    public int ScreenshotRetentionDays { get; set; } = 30;
}
