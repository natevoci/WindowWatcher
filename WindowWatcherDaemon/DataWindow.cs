using System;
using System.Collections.Generic;
using System.Text;

namespace WindowWatcherDaemon
{
    class DataWindow
    {
        private string _appTitle;
        private double _seconds;
        private DateTime _timeActivated;

        public DataWindow(string appTitle)
        {
            _appTitle = appTitle;
            _seconds = 0.0;
            _timeActivated = DateTime.MinValue;
        }

        public string AppTitle
        {
            get { return _appTitle; }
        }

        public double Seconds
        {
            get
            {
                double seconds = _seconds;
                if (_timeActivated != DateTime.MinValue)
                {
                    TimeSpan ts = DateTime.Now.Subtract(_timeActivated);
                    seconds += ts.TotalSeconds;
                }
                return seconds;
            }
        }

        public void AddSeconds(double seconds)
        {
            System.Diagnostics.Debug.WriteLine("Deactivating " + _appTitle);
            _timeActivated = DateTime.MinValue;
            _seconds += seconds;
        }

        public void Activate()
        {
            if (_timeActivated == DateTime.MinValue)
            {
                System.Diagnostics.Debug.WriteLine("Activating " + _appTitle);
                _timeActivated = DateTime.Now;
            }
        }
    }
}
