using System;
using System.Collections.Generic;
using System.Text;

namespace WindowWatcherDaemon
{
    class Data
    {
        private static List<DataProcess> _processes = new List<DataProcess>();
        internal static List<DataProcess> Processes
        {
            get { return Data._processes; }
        }

        public static void AddData(string appName, string appTitle, double seconds)
        {
            // Find the process
            DataProcess dataProcess = FindProcess(appName);

            // Find the window
            DataWindow dataWindow = FindWindow(dataProcess, appTitle);

            dataWindow.AddSeconds(seconds);
        }

        public static void ActivateProcess(string appName, string appTitle)
        {
            // Find the process
            DataProcess dataProcess = FindProcess(appName);

            // Find the window
            DataWindow dataWindow = FindWindow(dataProcess, appTitle);

            dataWindow.Activate();
        }

        private static DataProcess FindProcess(string appName)
        {
            // Find the process
            DataProcess dataProcess = null;
            foreach (DataProcess item in _processes)
            {
                if (item.AppName == appName)
                {
                    dataProcess = item;
                    break;
                }
            }
            if (dataProcess == null)
            {
                // If no process found then add one
                dataProcess = new DataProcess(appName);
                _processes.Add(dataProcess);
            }
            return dataProcess;
        }

        private static DataWindow FindWindow(DataProcess dataProcess, string appTitle)
        {
            // Find the window
            DataWindow dataWindow = null;
            foreach (DataWindow item in dataProcess.Windows)
            {
                if (item.AppTitle == appTitle)
                {
                    dataWindow = item;
                    break;
                }
            }
            if (dataWindow == null)
            {
                // If no window found then add one
                dataWindow = new DataWindow(appTitle);
                dataProcess.Windows.Add(dataWindow);
            }
            return dataWindow;
        }
    }

    class DataProcess
    {

        private string _appName;
        private List<DataWindow> _windows;

        public DataProcess(string appName)
        {
            _appName = appName;
            _windows = new List<DataWindow>();
        }

        public string AppName
        {
            get { return _appName; }
        }

        internal List<DataWindow> Windows
        {
            get { return _windows; }
        }

        public double Seconds
        {
            get
            {
                double seconds = 0.0;
                foreach (DataWindow item in _windows)
                {
                    seconds += item.Seconds;
                }
                return seconds;
            }
        }
    }
}
