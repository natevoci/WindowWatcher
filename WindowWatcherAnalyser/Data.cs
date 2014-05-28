using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WindowWatcherAnalyser
{
    class Data
    {
        List<DataEvent> _events;

        internal List<DataEvent> Events
        {
            get { return _events; }
        }

        public Data()
        {
            _events = new List<DataEvent>();
        }

        public List<DataEvent> FilterEvents(DateTime startTime, DateTime endTime)
        {
            List<DataEvent> result = new List<DataEvent>();

            DataEvent item = new DataEvent();
            item.Date = startTime;
            int indexStart = _events.BinarySearch(item, new DataEventComparer());
            if (indexStart < 0)
            {
                indexStart = -1 - indexStart;
            }
            indexStart--;
            if (indexStart < 0)
                indexStart = 0;

            item.Date = endTime;
            int indexEnd = _events.BinarySearch(item, new DataEventComparer());
            if (indexEnd < 0)
            {
                indexEnd = -1 - indexEnd;
            }
            if (indexEnd >= _events.Count)
                indexEnd = _events.Count - 1;

            for (int index = indexStart; index <= indexEnd; index++)
            {
                result.Add(_events[index]);
            }

            return result;
        }

        public void LoadData(string folderPath)
        {
            _events.Clear();

            foreach (string filename in System.IO.Directory.GetFiles(folderPath, "*.log"))
            {
                LoadFile(filename);
            }

            _events.Sort(new DataEventComparer());

            for (int index = 1; index < _events.Count; index++)
            {
                DataEvent next = _events[index];
                DataEvent curr = _events[index - 1];

                TimeSpan ts = next.Date.Subtract(curr.Date);
                curr.Seconds = ts.TotalSeconds;
            }
        }

        private void LoadFile(string filename)
        {
            using (StreamReader sr = new StreamReader(filename))
            {
                string line = "";
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim().Trim('\0');
                    if (line.Length == 0)
                        continue;

                    string[] args = ParseLine(line);

                    if (args.Length < 2)
                        continue;

                    DataEvent item = new DataEvent();
                    item.Date = DateTime.Parse(args[0]);
                    if (args[1] == DataEvent.EventTypes.WindowActivated.ToString())
                        item.Event = DataEvent.EventTypes.WindowActivated;
                    else if (args[1] == DataEvent.EventTypes.Inactive.ToString())
                        item.Event = DataEvent.EventTypes.Inactive;
                    else if (args[1] == DataEvent.EventTypes.Exiting.ToString())
                        item.Event = DataEvent.EventTypes.Exiting;
                    else if (args[1] == DataEvent.EventTypes.None.ToString())
                        item.Event = DataEvent.EventTypes.None;

                    if (item.Event == DataEvent.EventTypes.WindowActivated)
                    {
                        if (args.Length >= 3)
                            item.ProcessName = args[2];
                        if (args.Length >= 4)
                            item.WindowName = args[3];
                    }

                    _events.Add(item);
                }
            }
        }

        private string[] ParseLine(string line)
        {
            List<string> arguments = new List<string>();

            int indexCurr = 0;
            int length = line.Length;
            string argument = "";

            while (indexCurr < length)
            {
                if ((line[indexCurr] == ' ') || (line[indexCurr] == '\t'))
                {
                    indexCurr++;
                    continue;
                }
                if ((line[indexCurr] == ','))
                {
                    arguments.Add(argument);
                    argument = "";
                    indexCurr++;
                    continue;
                }
                if (line[indexCurr] == '"')
                {
                    int indexStart = indexCurr + 1;
                    bool incompleteArgument = false;

                    while (true)
                    {
                        int indexNextQuote = line.IndexOf("\"", indexStart);
                        int indexNextEscapedQuote = line.IndexOf("\\\"", indexStart);

                        int nextIndex = GetNextIndex(new int[] { indexNextQuote, indexNextEscapedQuote });

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

                int indexComma = line.IndexOf(",", indexCurr);
                if (indexComma == -1)
                    indexComma = line.Length;

                argument = line.Substring(indexCurr, indexComma - indexCurr);

                indexCurr = indexComma;
            }
            arguments.Add(argument);

            return arguments.ToArray();
        }

        private int GetNextIndex(int[] indexes)
        {
            int min = -1;
            int minIndex = -1;
            for (int index = 0; index < indexes.Length; index++)
            {
                if (indexes[index] != -1)
                {
                    if ((min == -1) || (min > indexes[index]))
                    {
                        minIndex = index;
                        min = indexes[index];
                    }
                }
            }

            return minIndex;
        }

    }

    class DataEvent
    {
        public DateTime Date = DateTime.MinValue;
        public EventTypes Event = EventTypes.None;
        public string ProcessName = "";
        public string WindowName = "";

        public double Seconds = 0.0;
        
        public enum EventTypes
        {
            None,
            WindowActivated,
            Inactive,
            Exiting
        }

    }

    class DataEventComparer : Comparer<DataEvent>
    {
        public override int Compare(DataEvent x, DataEvent y)
        {
            return x.Date.CompareTo(y.Date);
        }
    }

    class DataEventFilter
    {
        public DateTime MinDate = DateTime.MinValue;
        public DateTime MaxDate = DateTime.MaxValue;

        public bool Filter(DataEvent ev)
        {
            if (ev.Date < MinDate)
                return false;
            if (ev.Date > MaxDate)
                return false;

            return true;
        }
    }

    class DataEventGroup
    {
        public string Name = "";
        public double Seconds = 0.0;

        public DataEventGroup(string name)
        {
            this.Name = name;
        }
    }

    class DataEventGroupComparer : Comparer<DataEventGroup>
    {
        public override int Compare(DataEventGroup x, DataEventGroup y)
        {
            return x.Seconds.CompareTo(y.Seconds);
        }
    }

}
