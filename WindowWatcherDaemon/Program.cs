using System;
using System.Collections.Generic;
using System.Windows.Forms;

using System.Diagnostics;

namespace WindowWatcherDaemon
{
    static class Program
    {
        private static UserActivityHook _activityHook;
        private static DateTime _lastActivity = DateTime.MinValue;
        private static System.Threading.ManualResetEvent _exitWindowWatchingThread = new System.Threading.ManualResetEvent(false);
        private static Dictionary<int, string> _processNamesCache = new Dictionary<int, string>();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            _activityHook = new UserActivityHook(true, true);
            _activityHook.KeyPress += new KeyPressEventHandler(activityHook_KeyPress);
            _activityHook.OnMouseActivity += new MouseEventHandler(activityHook_OnMouseActivity);

            System.Threading.Thread wwProc = new System.Threading.Thread(WindowWatcherProc);
            wwProc.Name = "Window Watcher Thread";
            wwProc.Start();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WindowWatcher());

            _activityHook.Stop();

            _exitWindowWatchingThread.Set();
            wwProc.Join();
        }

        static void activityHook_KeyPress(object sender, KeyPressEventArgs e)
        {
            _lastActivity = DateTime.UtcNow;
        }

        static void activityHook_OnMouseActivity(object sender, MouseEventArgs e)
        {
            _lastActivity = DateTime.UtcNow;
        }

        static void WindowWatcherProc()
        {
            Int32 lastAppPid = -1;
            string lastAppName = "";
            string lastAppTitle = "";
            DateTime lastAppActivated = DateTime.MinValue;
            _lastActivity = DateTime.UtcNow;
            bool inactiveWritten = false;

            LogItem logItem;

            while (_exitWindowWatchingThread.WaitOne(100, false) == false)
            {
                try
                {
                    TimeSpan elapsed = DateTime.UtcNow.Subtract(_lastActivity);
                    int idleTimeout = AppSettings.Default.IdleTimeoutSeconds;
                    if (elapsed.TotalSeconds > idleTimeout && !inactiveWritten)
                    {
                        logItem = new LogItem();
                        logItem.Event = LogItem.EventTypes.Inactive;
                        Log.Add(logItem);

                        inactiveWritten = true;
                        _lastActivity = DateTime.UtcNow;
                    }

                    IntPtr hwnd = GetHwnd();
                    Int32 pid = GetPid(hwnd);
                    string appTitle = GetAppTitle();
                    string appName = GetProcessNameFromProcessId(pid);

                    if ((lastAppPid != pid) || (lastAppTitle != appTitle))
                    {
                        DateTime now = DateTime.Now;
                        if (lastAppActivated != DateTime.MinValue)
                        {
                            TimeSpan ts = now.Subtract(lastAppActivated);
                        }

                        lastAppActivated = now;
                        lastAppPid = pid;
                        lastAppName = appName;
                        lastAppTitle = appTitle;

                        logItem = new LogItem();
                        logItem.Event = LogItem.EventTypes.WindowActivated;
                        logItem.Process = lastAppName;
                        logItem.Title = lastAppTitle;
                        Log.Add(logItem);

                        inactiveWritten = false;
                        _lastActivity = DateTime.UtcNow;
                    }
                }
                catch (Exception)
                {
                }
            }
            logItem = new LogItem();
            logItem.Event = LogItem.EventTypes.Exiting;
            Log.Add(logItem);
        }

        private static string GetProcessNameFromProcessId(int pid)
        {
            // Load from cache to reduce CPU usage
            if (_processNamesCache.TryGetValue(pid, out string cachedName))
            {
                return cachedName;
            }

            Process p = GetProcessById(pid);
            string appName = p.ProcessName;
            _processNamesCache[pid] = appName;
            return appName;
        }

        private static Process GetProcessById(int pid)
        {
            return Process.GetProcessById(pid);
        }

        private static string GetAppTitle()
        {
            return api.ActiveApplTitle().Trim().Replace("\0", "");
        }

        private static int GetPid(IntPtr hwnd)
        {
            return api.GetWindowProcessID(hwnd);
        }

        private static IntPtr GetHwnd()
        {
            return api.getforegroundWindow();
        }
    }
}