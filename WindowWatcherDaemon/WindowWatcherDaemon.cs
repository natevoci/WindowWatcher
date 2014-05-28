using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.Diagnostics;

namespace WindowWatcherDaemon
{
    public partial class WindowWatcher : Form
    {
        //private Int32 _lastAppPid = -1;
        //private string _lastAppName = "";
        //private string _lastAppTitle = "";
        //private DateTime _lastAppActivated = DateTime.MinValue;
        //private List<Color> _graphColors = new List<Color>();

        private int _lastLatestRevision = -1;

        private FormWindowState _previousWindowState = FormWindowState.Normal;

        public WindowWatcher()
        {
            InitializeComponent();

            _previousWindowState = FormWindowState.Normal;
            if (AppSettings.Default.Minimize)
            {
                //Show();
                SendToTray();
                //this.Visible = false;
                this.ShowInTaskbar = false;
            }

            //_graphColors.Add(Color.DarkGoldenrod);
            //_graphColors.Add(Color.Green);
            //_graphColors.Add(Color.Red);
            //_graphColors.Add(Color.Blue);
            //_graphColors.Add(Color.Purple);
            //_graphColors.Add(Color.Lime);
            //_graphColors.Add(Color.LightBlue);
            //_graphColors.Add(Color.LightGray);
            //_graphColors.Add(Color.Brown);
            //_graphColors.Add(Color.PaleGreen);

        }

        private void WindowWatcher_Load(object sender, EventArgs e)
        {
            timer1.Start();
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            RestoreFromTray();
        }

        private void WindowWatcher_Resize(object sender, EventArgs e)
        {
            UpdateTrayIcon();
        }

        private void UpdateTrayIcon()
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                SendToTray();
            }
            else
            {
                _previousWindowState = this.WindowState;
            }
        }

        private void SendToTray()
        {
            if (this.WindowState != FormWindowState.Minimized)
                this.WindowState = FormWindowState.Minimized;
            this.notifyIcon1.Visible = true;
            this.Hide();
        }

        private void RestoreFromTray()
        {
            this.ShowInTaskbar = true;
            this.Show();
            this.WindowState = _previousWindowState;
            this.notifyIcon1.Visible = false;
        }

        private void restoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (_lastLatestRevision != Log.LatestRevision)
            {
                _lastLatestRevision = Log.LatestRevision;
                RefreshData();
            }
        }

        private void RefreshData()
        {
            listView1.BeginUpdate();

            LogItem[] latest = Log.Latest;

            listView1.Items.Clear();

            ListViewItem item = null;
            foreach (LogItem logItem in latest)
            {
                if (logItem.Event == LogItem.EventTypes.WindowActivated)
                {
                    item = new ListViewItem(new string[] { logItem.Date.ToShortTimeString(), logItem.Process, logItem.Title });
                }
                else
                {
                    item = new ListViewItem(new string[] { logItem.Date.ToShortTimeString(), "<" + logItem.Event.ToString().ToLower() + ">", "" });
                }
                listView1.Items.Add(item);
            }
            if (item != null)
                item.EnsureVisible();

            listView1.EndUpdate();
        }

        private void viewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string currPath = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            System.Diagnostics.Process.Start(currPath + @"\WindowWatcherAnalyser.exe");
        }

        //private void RefreshData()
        //{
        //    label1.Invalidate();
        //    label1.Update();

        //    listView1.BeginUpdate();

        //    double totalSeconds = 0.0;
        //    foreach (DataProcess proc in Data.Processes)
        //    {
        //        totalSeconds += proc.Seconds;
        //    }

        //    listView1.Items.Clear();

        //    foreach (DataProcess proc in Data.Processes)
        //    {
        //        double seconds = proc.Seconds;
        //        double percentage = 0.0;
        //        if (totalSeconds > 0.0)
        //            percentage = 100.0 * seconds / totalSeconds;

        //        ListViewItem item = new ListViewItem(new string[] { proc.AppName, seconds.ToString("0"), percentage.ToString("0") + " %" });
        //        listView1.Items.Add(item);

        //        if (seconds == 0.0)
        //            continue;

        //        foreach (DataWindow wind in proc.Windows)
        //        {
        //            percentage = 100.0 * wind.Seconds / seconds;

        //            item = new ListViewItem(new string[] { "    " + wind.AppTitle, wind.Seconds.ToString("0"), percentage.ToString("0") + " %" });
        //            listView1.Items.Add(item);
        //        }

        //    }

        //    listView1.EndUpdate();
        //}

        //private void label1_Paint(object sender, PaintEventArgs e)
        //{
        //    PaintStats(e.Graphics);
        //}

        //private void PaintStats(Graphics g)
        //{
        //    g.FillRectangle(Brushes.Black, 0, 0, label1.Width, label1.Height);

        //    Font drawFont = new Font("Arial", 10);

        //    double maxSeconds = 1.0;
        //    foreach (DataProcess proc in Data.Processes)
        //    {
        //        if (maxSeconds < proc.Seconds)
        //            maxSeconds = proc.Seconds;
        //    }

        //    int y = 10;
        //    int index = 0;
        //    foreach (DataProcess proc in Data.Processes)
        //    {
        //        Color c = _graphColors[index % _graphColors.Count];
                
        //        Brush b = new SolidBrush(c);
        //        int width = (int)(proc.Seconds / maxSeconds * label1.Width);
        //        g.FillRectangle(b, 0, y, width, 20);

        //        g.DrawString(proc.AppName + "  - " + proc.Seconds.ToString("0"), drawFont, Brushes.White, 2, y);

        //        index += 1;
        //        y += 20;
        //    }
        //}

    }
}