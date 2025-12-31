using System.IO;

namespace WindowWatcher.ScreenshotViewer.Models;

public record ScreenshotInfo
{
    public required string FilePath { get; init; }
    public TimeSpan TimeOfDay { get; init; }
    public int Index { get; init; }
    
    public DateTime Date { get; init; }
    public string FileName => Path.GetFileName(FilePath);
}
