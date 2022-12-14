using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Sentry.Unity
{
    public class FlashbackDebouncer
    {
        record LogEntry
        {
            #region Properties
            public string? Message;
            public LogType LogType;
            public int Count;
            public DateTime Timestamp;
            #endregion

            #region Methods
            public override string ToString() => $"{Message} ({Count} times)";
            public static implicit operator string(LogEntry entry) => entry.ToString();
            #endregion
        }

        readonly Dictionary<int, LogEntry?> entries = new ();
        TimeSpan offset = TimeSpan.FromSeconds(1);

        public async void Debounce(string message, LogType logType, DebouncerCaptureCallback capture)
        {
            void FlushTails()
            {
                // TODO: sort by timestemps to order breadcrumbs
                foreach (int key in entries.Keys.ToArray())
                {
                    if (entries[key] is LogEntry tailEntry)
                    {
                        capture?.Invoke(tailEntry, tailEntry.LogType, AsBreadcrumbsOnly: true);
                        entries[key] = null;
                    }
                }
            }

            int hash = message.GetHashCode() + logType.GetHashCode();

            if (!entries.TryGetValue(hash, out LogEntry? entry))
            {
                FlushTails();

                entries[hash] = null;
                capture?.Invoke(message, logType, AsBreadcrumbsOnly: false);

                await Task.Delay(offset);

                while (entries[hash] is LogEntry tailEntry && (tailEntry.Timestamp - DateTime.Now) is {} time && (time.TotalMilliseconds > 100))
                    await Task.Delay(time);

                if (entries[hash] is LogEntry tailEntryCapture)
                    capture?.Invoke(tailEntryCapture, tailEntryCapture.LogType, AsBreadcrumbsOnly: true);

                entries.Remove(hash);
            }
            else
            {
                // TODO: var textDistance = GetTextDistance(firstMessage, currentMessage);
                //    if textDistance > threshold then add message to breadcrumbs
                entries[hash] = entry ??= new() { Message = message, LogType = logType, Count = 0 };
                entry.Count++;
                entry.Timestamp = DateTime.Now.Add(offset);
            }
        }
    }
}
