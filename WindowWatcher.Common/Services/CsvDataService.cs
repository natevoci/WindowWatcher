using System.Globalization;
using System.Text;
using WindowWatcher.Common.Models;

namespace WindowWatcher.Common.Services;

public class CsvDataService
{
    private static readonly object FileLock = new();
    private const int MaxRetryAttempts = 5;
    private const int InitialRetryDelayMs = 100;

    public void WriteEvent(WindowActivityEvent evt, string dataDirectory)
    {
        string filename = Path.Combine(dataDirectory, $"stats-{evt.Date:yyyy-MM-dd}.csv");
        bool fileExists = File.Exists(filename);

        lock (FileLock)
        {
            try
            {
                using var writer = new StreamWriter(filename, append: true, Encoding.UTF8);
                
                if (!fileExists)
                {
                    writer.WriteLine("Date,Event,Process,WindowTitle");
                }
                
                writer.WriteLine(evt.ToCsvString());
            }
            catch (IOException ex)
            {
                throw new IOException($"Failed to write to CSV file: {filename}", ex);
            }
        }
    }

    public List<WindowActivityEvent> LoadAllEvents(string dataDirectory)
    {
        var events = new List<WindowActivityEvent>();

        if (!Directory.Exists(dataDirectory))
            return events;

        var csvFiles = Directory.GetFiles(dataDirectory, "stats-*.csv");

        foreach (var filename in csvFiles)
        {
            var fileEvents = LoadEventsFromFile(filename);
            events.AddRange(fileEvents);
        }

        events.Sort(new Utilities.WindowActivityEventComparer());

        // Calculate durations between events
        for (int i = 1; i < events.Count; i++)
        {
            var current = events[i - 1];
            var next = events[i];
            var duration = next.Date - current.Date;
            current.DurationSeconds = duration.TotalSeconds;
        }

        return events;
    }

    public List<WindowActivityEvent> LoadEventsFromFile(string filename)
    {
        var events = new List<WindowActivityEvent>();
        int retryCount = 0;
        int delay = InitialRetryDelayMs;

        while (retryCount < MaxRetryAttempts)
        {
            try
            {
                using var reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == "Date,Event,Process,WindowTitle")
                        continue;

                    line = line.Trim().Trim('\0');
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var evt = ParseCsvLine(line);
                    if (evt != null)
                        events.Add(evt);
                }

                return events;
            }
            catch (IOException) when (retryCount < MaxRetryAttempts - 1)
            {
                retryCount++;
                Thread.Sleep(delay);
                delay *= 2; // Exponential backoff
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to read CSV file after {retryCount} retries: {filename}", ex);
            }
        }

        throw new IOException($"Failed to read CSV file after {MaxRetryAttempts} attempts: {filename}");
    }

    private WindowActivityEvent? ParseCsvLine(string line)
    {
        try
        {
            var args = ParseLine(line);

            if (args.Length < 2)
                return null;

            var evt = new WindowActivityEvent
            {
                Date = DateTime.Parse(args[0], CultureInfo.InvariantCulture)
            };

            if (Enum.TryParse<WindowActivityEventType>(args[1], out var eventType))
            {
                evt.EventType = eventType;
            }

            if (eventType == WindowActivityEventType.WindowActivated)
            {
                if (args.Length >= 3)
                    evt.ProcessName = args[2];
                if (args.Length >= 4)
                    evt.WindowTitle = args[3];
            }

            return evt;
        }
        catch
        {
            return null;
        }
    }

    private string[] ParseLine(string line)
    {
        var arguments = new List<string>();
        int indexCurr = 0;
        int length = line.Length;
        string argument = string.Empty;

        while (indexCurr < length)
        {
            if (line[indexCurr] is ' ' or '\t')
            {
                indexCurr++;
                continue;
            }
            
            if (line[indexCurr] == ',')
            {
                arguments.Add(argument);
                argument = string.Empty;
                indexCurr++;
                continue;
            }
            
            if (line[indexCurr] == '"')
            {
                int indexStart = indexCurr + 1;
                bool incompleteArgument = false;

                while (true)
                {
                    int indexNextQuote = line.IndexOf('"', indexStart);
                    int indexNextEscapedQuote = line.IndexOf("\\\"", indexStart, StringComparison.Ordinal);

                    int nextIndex = GetNextIndex([indexNextQuote, indexNextEscapedQuote]);

                    if (nextIndex == 0) // indexNextQuote
                    {
                        argument = line.Substring(indexStart, indexNextQuote - indexStart);
                        indexCurr = indexNextQuote + 1;
                        break;
                    }
                    else if (nextIndex == 1) // indexNextEscapedQuote
                    {
                        indexStart = indexNextEscapedQuote + 2;
                        continue;
                    }
                    else // incomplete quoted string
                    {
                        incompleteArgument = true;
                        break;
                    }
                }
                
                if (!incompleteArgument)
                    continue;
            }

            int indexComma = line.IndexOf(',', indexCurr);
            if (indexComma == -1)
                indexComma = line.Length;

            argument = line.Substring(indexCurr, indexComma - indexCurr);
            indexCurr = indexComma;
        }
        
        arguments.Add(argument);
        return arguments.ToArray();
    }

    private static int GetNextIndex(int[] indexes)
    {
        int min = -1;
        int minIndex = -1;
        
        for (int i = 0; i < indexes.Length; i++)
        {
            if (indexes[i] != -1)
            {
                if (min == -1 || min > indexes[i])
                {
                    minIndex = i;
                    min = indexes[i];
                }
            }
        }

        return minIndex;
    }
}
