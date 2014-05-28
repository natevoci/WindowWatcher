using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WindowWatcherAnalyser
{
    public partial class WindowWatcherAnalyser : Form
    {
        Data _data;
        double _hoursVisible;

        private List<DataEvent> _events;
        private List<string> _rows;
        private List<string> _processes;

        public WindowWatcherAnalyser()
        {
            _data = new Data();
            _data.LoadData(@".");
            _hoursVisible = 24.0f;

            InitializeComponent();

            _processes = new List<string>();

            foreach (DataEvent ev in _data.Events)
            {
                if (ev.Event == DataEvent.EventTypes.WindowActivated)
                {
                    if (string.Compare(ev.ProcessName, "Idle", true) == 0)
                        continue;
                    if (_processes.Contains(ev.ProcessName) == false)
                        _processes.Add(ev.ProcessName);
                }
            }
            _processes.Sort();
            _processes.Insert(0, "Idle");
            _processes.Insert(0, "-- All Processes --");
            comboBoxProcess.DataSource = _processes;

            dateTimePickerCurrentDay.Value = DateTime.Today.AddHours(12.0);
        }

        private void pictureBoxGraph_Paint(object sender, PaintEventArgs e)
        {
            PaintGraph(e.Graphics);
        }

        private void pictureBoxGraph_SizeChanged(object sender, EventArgs e)
        {
            pictureBoxGraph.Invalidate();
        }

        private void dateTimePickerCurrentDay_ValueChanged(object sender, EventArgs e)
        {
            LoadEvents();
            pictureBoxGraph.Invalidate();
        }

        private void vScrollBarGraph_Scroll(object sender, ScrollEventArgs e)
        {
            pictureBoxGraph.Invalidate();
        }

        private void buttonPrevDay_Click(object sender, EventArgs e)
        {
            dateTimePickerCurrentDay.Value = dateTimePickerCurrentDay.Value.AddHours(_hoursVisible * -1.0);
        }
        private void buttonNextDay_Click(object sender, EventArgs e)
        {
            dateTimePickerCurrentDay.Value = dateTimePickerCurrentDay.Value.AddHours(_hoursVisible * 1.0);
        }
        private void buttonScrollLeft_Click(object sender, EventArgs e)
        {
            dateTimePickerCurrentDay.Value = dateTimePickerCurrentDay.Value.AddHours(_hoursVisible * -0.2);
        }
        private void buttonScrollRight_Click(object sender, EventArgs e)
        {
            dateTimePickerCurrentDay.Value = dateTimePickerCurrentDay.Value.AddHours(_hoursVisible * 0.2);
        }

        private void comboBoxProcess_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadEvents();
            pictureBoxGraph.Invalidate();
        }

        private void buttonFullDay_Click(object sender, EventArgs e)
        {
            _hoursVisible = 24.0;
            dateTimePickerCurrentDay.Value = dateTimePickerCurrentDay.Value.Date.AddHours(12.0);
        }
        private void buttonZoomIn_Click(object sender, EventArgs e)
        {
            _hoursVisible /= 1.5;
            LoadEvents();
            pictureBoxGraph.Invalidate();
        }
        private void buttonZoomOut_Click(object sender, EventArgs e)
        {
            _hoursVisible *= 1.5;
            LoadEvents();
            pictureBoxGraph.Invalidate();
        }


        private void LoadEvents()
        {
            DateTime startTime = dateTimePickerCurrentDay.Value.AddHours(_hoursVisible / -2);
            DateTime endTime = dateTimePickerCurrentDay.Value.AddHours(_hoursVisible / 2);
            startTime = startTime.ToUniversalTime();
            endTime = endTime.ToUniversalTime();
            TimeSpan windowTimeSpan = endTime.Subtract(startTime);
            float pixelsPerSecond = pictureBoxGraph.ClientRectangle.Width * 1.0f / (float)(windowTimeSpan.TotalHours * 60.0f * 60.0f);

            // Get events
            _events = _data.FilterEvents(startTime, endTime);

            Dictionary<string, DataEventGroup> groups = new Dictionary<string, DataEventGroup>();

            _rows = new List<string>();
            foreach (DataEvent ev in _events)
            {
                if (ev.Event == DataEvent.EventTypes.WindowActivated)
                {
                    if (string.Compare(ev.ProcessName, "Idle", true) == 0)
                        continue;

                    string name = "";
                    if (comboBoxProcess.Text == "-- All Processes --")
                    {
                        name = ev.ProcessName;
                    }
                    else if (ev.ProcessName == comboBoxProcess.Text)
                    {
                        name = ev.ProcessName + " - " + ev.WindowName;
                    }

                    if (name.Length > 0)
                    {
                        if (groups.ContainsKey(name) == false)
                        {
                            groups.Add(name, new DataEventGroup(name));
                        }
                        DataEventGroup group = groups[name];
                        group.Seconds += ev.Seconds;
                    }
                }
            }
            List<DataEventGroup> groupList = new List<DataEventGroup>();
            groupList.AddRange(groups.Values);
            groupList.Sort(new DataEventGroupComparer());
            groupList.Reverse();

            foreach (DataEventGroup group in groupList)
            {
                if (_rows.Contains(group.Name) == false)
                    _rows.Add(group.Name);
            }

            if (comboBoxProcess.Text == "-- All Processes --")
                _rows.Insert(0, "Idle");
            else
                _rows.Insert(0, comboBoxProcess.Text);

            vScrollBarGraph.Maximum = _rows.Count + vScrollBarGraph.LargeChange - 1;

        }

        private void PaintGraph(Graphics g)
        {
            Rectangle windowRect = pictureBoxGraph.ClientRectangle;

            g.FillRectangle(Brushes.Black, windowRect);

            Font font = new Font("Tahoma", 10.0f, FontStyle.Regular);
            Pen dashedPen = new Pen(Brushes.Gray);
            dashedPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;

            float offsetY = 40.0f;
            float rowHeight = 24.0f;
            float textHeight = 18.0f;

            DateTime startTime = dateTimePickerCurrentDay.Value.AddHours(_hoursVisible / -2);
            DateTime endTime = dateTimePickerCurrentDay.Value.AddHours(_hoursVisible / 2);
            startTime = startTime.ToUniversalTime();
            endTime = endTime.ToUniversalTime();
            TimeSpan windowTimeSpan = endTime.Subtract(startTime);
            float pixelsPerSecond = windowRect.Width / (float)(windowTimeSpan.TotalSeconds);


            // Draw time scale
            DateTime currHour = startTime;
            if (currHour.Second > 0)
                currHour = currHour.AddSeconds(60 - currHour.Second);
            if (currHour.Minute > 0)
                currHour = currHour.AddMinutes(60 - currHour.Minute);

            while (currHour < endTime)
            {
                TimeSpan ts = currHour.Subtract(startTime);
                float x = (float)ts.TotalSeconds * pixelsPerSecond;

                g.DrawLine(dashedPen, x, 0, x, windowRect.Bottom);

                g.DrawString(currHour.ToLocalTime().ToString("HH:mm"), font, Brushes.White, x, 0);

                currHour = currHour.AddHours(1.0);
            }
            g.DrawLine(dashedPen, 0, offsetY, windowRect.Right, offsetY);


            // Draw events
            if (false)
            {
                foreach (DataEvent ev in _events)
                {
                    TimeSpan ts = ev.Date.Subtract(startTime);
                    float startX = (float)ts.TotalSeconds * pixelsPerSecond;
                    float width = (float)ev.Seconds * pixelsPerSecond + 1;

                    int rowNumber = 0;
                    if (ev.Event == DataEvent.EventTypes.WindowActivated)
                        rowNumber = _rows.IndexOf(ev.ProcessName);

                    rowNumber -= vScrollBarGraph.Value;
                    if (rowNumber < 0)
                        continue;

                    float startY = offsetY + (rowNumber * rowHeight);

                    RectangleF rect = new RectangleF(startX, startY, width, rowHeight);
                    g.FillRectangle(Brushes.White, rect);
                }
            }
            else
            {
                float secondsPerPixel = (float)windowTimeSpan.TotalSeconds / (float)windowRect.Width;

                for (int row = vScrollBarGraph.Value; row < _rows.Count; row++)
                {
                    float startY = offsetY + ((row - vScrollBarGraph.Value) * rowHeight);
                    int lastPixel = -1;
                    float percent = 0.0f;
                    string rowName = _rows[row];

                    foreach (DataEvent ev in _events)
                    {
                        string processName = "";
                        if (ev.Event == DataEvent.EventTypes.Inactive)
                        {
                            processName = "Idle";
                        }
                        else if (ev.Event == DataEvent.EventTypes.WindowActivated)
                        {
                            processName = ev.ProcessName;
                        }
                        else
                        {
                            continue;
                        }

                        if (rowName != processName + " - " + ev.WindowName)
                        {
                            if (rowName != processName)
                                continue;
                        }

                        TimeSpan ts = ev.Date.Subtract(startTime);
                        float x = (float)ts.TotalSeconds * pixelsPerSecond;
                        int pixel = (int)Math.Floor(x);

                        if (lastPixel != pixel)
                        {
                            if (percent > 0.0f)
                            {
                                int colorComponent = (int)((127.0f * percent) + 128.0f);
                                Color color = Color.FromArgb(colorComponent, colorComponent, colorComponent);
                                g.DrawLine(new Pen(color), lastPixel, startY, lastPixel, startY + rowHeight);
                            }

                            percent = 0.0f;
                            lastPixel = pixel;
                        }

                        float fraction = (float)(ts.TotalSeconds + ev.Seconds) * pixelsPerSecond;
                        if (fraction > pixel + 1)
                        {
                            percent += pixel + 1 - x;
                            if (percent > 0.0f)
                            {
                                int colorComponent = (int)((127.0f * percent) + 128.0f);
                                Color color = Color.FromArgb(colorComponent, colorComponent, colorComponent);
                                g.DrawLine(new Pen(color), pixel, startY, pixel, startY + rowHeight);
                            }

                            percent = fraction - (pixel + 1);
                            pixel++;
                            while (percent >= 1.0f)
                            {
                                int colorComponent = (int)((127.0f * 1.0f) + 128.0f);
                                Color color = Color.FromArgb(colorComponent, colorComponent, colorComponent);
                                g.DrawLine(new Pen(color), pixel, startY, pixel, startY + rowHeight);

                                pixel++;
                                percent--;
                            }

                            lastPixel = pixel;
                        }
                        else
                        {
                            fraction -= x;
                            percent += fraction;
                        }
                    }

                }

            }

            // Draw row titles
            for (int index = vScrollBarGraph.Value; index < _rows.Count; index++)
            {
                string rowName = _rows[index];
                int rowNumber = index - vScrollBarGraph.Value;
                float startY = offsetY + (rowNumber * rowHeight);

                g.DrawLine(dashedPen, 0, startY, windowRect.Right, startY);
                
                SizeF textSize = g.MeasureString(rowName, font);
                //g.FillRectangle(Brushes.LightGray, 0, startY, textSize.Width, textSize.Height);
                g.DrawString(rowName, font, Brushes.LightGreen, 0, startY);
                g.DrawString(rowName, font, Brushes.LightGreen, windowRect.Width / 2, startY);
            }

        }





    }
}