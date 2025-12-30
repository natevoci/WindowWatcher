using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using WindowWatcher.Common.Services;

namespace WindowWatcher.Daemon;

public sealed class LoggingService : ILogger
{
    private readonly object _sync = new();
    private readonly SettingsService _settingsService;

    public LoggingService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WindowWatcher",
            "Logs");
    }

    public string LogDirectory { get; }

    public void LogInformation(string message)
    {
        Log(LogLevel.Information, default, message, null, static (state, _) => state);
    }

    public void LogDebug(string message)
    {
        Log(LogLevel.Debug, default, message, null, static (state, _) => state);
    }

    public void LogTrace(string message)
    {
        Log(LogLevel.Trace, default, message, null, static (state, _) => state);
    }

    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, default, message, null, static (state, _) => state);
    }

    public void LogError(string message, Exception exception)
    {
        Log(LogLevel.Error, default, message, exception, static (state, _) => state);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        if (logLevel == LogLevel.None)
        {
            return false;
        }

        var settings = _settingsService.LoadSettings();
        return Enum.TryParse<LogLevel>(settings.LogLevel, true, out var minimumLogLevel)
            && logLevel >= minimumLogLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        Write(logLevel.ToString(), formatter(state, exception), exception);
    }

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string entry = $"{timestamp} [{level}] {message}";
            if (exception != null)
            {
                entry += $"{Environment.NewLine}{exception}";
            }

            entry += Environment.NewLine;
            string logPath = Path.Combine(LogDirectory, $"windowwatcher-{DateTime.Today:yyyy-MM-dd}.log");

            lock (_sync)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(logPath, entry, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must not interrupt the daemon when the log location is unavailable.
        }
    }
}