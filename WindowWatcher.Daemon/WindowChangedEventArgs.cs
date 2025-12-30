namespace WindowWatcher.Daemon;

public sealed class WindowChangedEventArgs : EventArgs
{
    public required IntPtr WindowHandle { get; init; }
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public required DateTime ChangedUtc { get; init; }
}
