namespace WindowWatcher.Common.Models;

public class AppSettings
{
    public string ScreenshotsPath { get; set; } = @".\Screenshots";
    public string DataDirectory { get; set; } = @".\ActivityLogs";
    public bool MinimizeOnStartup { get; set; } = true;
    public int IdleTimeoutSeconds { get; set; } = 300;
    public int LogRetentionDays { get; set; } = 90;
    public string LogLevel { get; set; } = "Information";
    public int ScreenshotIntervalSeconds { get; set; } = 5;
    public string ScreenshotCaptureTarget { get; set; } = "AllScreens";
    public string ScreenshotImageFormat { get; set; } = "Jpeg";
    public int ScreenshotRetentionDays { get; set; } = 30;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            ScreenshotsPath = ScreenshotsPath,
            DataDirectory = DataDirectory,
            MinimizeOnStartup = MinimizeOnStartup,
            IdleTimeoutSeconds = IdleTimeoutSeconds,
            LogRetentionDays = LogRetentionDays,
            LogLevel = LogLevel,
            ScreenshotIntervalSeconds = ScreenshotIntervalSeconds,
            ScreenshotCaptureTarget = ScreenshotCaptureTarget,
            ScreenshotImageFormat = ScreenshotImageFormat,
            ScreenshotRetentionDays = ScreenshotRetentionDays
        };
    }
}
