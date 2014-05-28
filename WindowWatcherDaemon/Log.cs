using System;
using System.Collections.Generic;
using System.Text;

using System.Diagnostics;
using System.IO;

namespace WindowWatcherDaemon
{
    class LogItem
    {
        public DateTime Date = DateTime.UtcNow;
        public EventTypes Event = EventTypes.None;
        public string Process = "";
        public string Title = "";

        public enum EventTypes
        {
            None,
            WindowActivated,
            Inactive,
            Exiting
        }

        public override string ToString()
        {
            string date = Date.ToString("yyyy/MM/dd HH:mm:ss.fff");

            StringBuilder result = new StringBuilder();
            result.Append(date + ", " + Event.ToString());

            if (Process.Length > 0)
            {
                result.Append(", \"").Append(Process.Replace("\"", "\\\"")).Append("\"");

                if (Title.Length > 0)
                    result.Append(", \"").Append(Title.Replace("\"", "\\\"")).Append("\"");
            }
            return result.ToString();
        }
    }

    class Log
    {
        private static object fileLock = new object();

        private static Queue<LogItem> _latest = new Queue<LogItem>();
        private static int _latestRevision = 0;
        private static object latestLock = new object();

        internal static LogItem[] Latest
        {
            get
            {
                lock (latestLock)
                {
                    LogItem[] result = new LogItem[_latest.Count];
                    _latest.CopyTo(result, 0);
                    return result;
                }
            }
        }
        public static int LatestRevision
        {
            get { return Log._latestRevision; }
        }

        public static void Add(LogItem item)
        {
            lock (latestLock)
            {
                _latest.Enqueue(item);
                while (_latest.Count > 100)
                    _latest.Dequeue();
                _latestRevision++;
            }

            Debug.WriteLine(item.ToString());
            WriteToFile(item.ToString());
        }

        private static void WriteToFile(string text)
        {
            string filename;
            FileInfo fi = new FileInfo("stats-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".log");
            filename = fi.FullName;

            lock (fileLock)
            {
                StreamWriter sw = null;
                try
                {
                    sw = new StreamWriter(filename, true);
                    sw.WriteLine(text);
                }
                finally
                {
                    if (sw != null)
                        sw.Close();
                }
            }
        }
    }
}
