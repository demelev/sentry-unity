using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Sentry.Unity.Integrations
{
    public class FlashbackDebouncer
    {
        TimeSpan delay;
        object sync = new();

        public FlashbackDebouncer(int delayMilliseconds = 1000)
        {
            this.delay = TimeSpan.FromMilliseconds(delayMilliseconds);
        }

        record LogEntry
        {
            #region Properties
            public string? Message;
            public LogType LogType;
            public int Count;
            public DateTime Timestamp;
            public required string Backtrace;
            #endregion

            #region Methods
            public override string ToString() => $"{Message} ({Count} times)";
            public static implicit operator string(LogEntry entry) => entry.ToString();
            #endregion
        }

        readonly Dictionary<int, LogEntry?> entries = new();

        void CaptureAccumulated(Dictionary<int, LogEntry?> entries, DebouncerCaptureCallback capture)
        {
            foreach (int key in entries.Keys.ToArray())
            {
                if (entries[key] is LogEntry tailEntry)
                {
                    capture?.Invoke(tailEntry, tailEntry.LogType, tailEntry.Backtrace, allowCaptureAsEvent: false);
                    entries[key] = null;
                }
            }
        }

        public async void Debounce(string message, LogType logType, string backtrace, DebouncerCaptureCallback capture)
        {
            int hash = ComputeCustomHash(message, backtrace);

            if (!entries.TryGetValue(hash, out LogEntry? entry))
            {
                lock (sync)
                {
                    CaptureAccumulated(entries, capture);
                }

                entries[hash] = null;
                capture?.Invoke(message, logType, backtrace, allowCaptureAsEvent: true);

                await Task.Delay(delay);

                while (entries[hash] is LogEntry tailEntry && (tailEntry.Timestamp - DateTime.Now) is { } time && (time.TotalMilliseconds > 100))
                    await Task.Delay(time);

                lock (sync)
                {
                    if (entries[hash] is LogEntry tailEntryCapture)
                        capture?.Invoke(tailEntryCapture, tailEntryCapture.LogType, tailEntryCapture.Backtrace, allowCaptureAsEvent: false);
                    entries.Remove(hash);
                }
            }
            else
            {
                lock (sync)
                {
                    entries[hash] = entry ??= new() { Message = message, LogType = logType, Count = 0, Backtrace = backtrace };
                    entry.Count++;
                    entry.Timestamp = DateTime.Now.Add(delay);
                }
            }
        }

        private int ComputeCustomHash(string message, string backtrace)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in message)
                {
                    hash = hash * 31 + c.GetHashCode();
                }
                foreach (char c in backtrace)
                {
                    hash = hash * 31 + c.GetHashCode();
                }
                return hash;
            }
        }
    }
}
