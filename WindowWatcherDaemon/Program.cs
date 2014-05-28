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

            LogItem logItem;

            while (_exitWindowWatchingThread.WaitOne(100, false) == false)
            {
                try
                {
                    TimeSpan elapsed = DateTime.UtcNow.Subtract(_lastActivity);
                    int idleTimeout = AppSettings.Default.IdleTimeoutSeconds;
                    if (elapsed.TotalSeconds < idleTimeout)
                    {
                        IntPtr hwnd = api.getforegroundWindow();
                        Int32 pid = api.GetWindowProcessID(hwnd);
                        string appTitle = api.ActiveApplTitle().Trim().Replace("\0", "");
                        Process p = Process.GetProcessById(pid);
                        string appName = p.ProcessName;

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
                        }
                    }
                    else
                    {
                        if (lastAppPid != -1)
                        {
                            lastAppPid = -1;
                            lastAppName = "";
                            lastAppTitle = "";
                            lastAppActivated = DateTime.MinValue;

                            logItem = new LogItem();
                            logItem.Event = LogItem.EventTypes.Inactive;
                            Log.Add(logItem);
                        }
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
    }
}