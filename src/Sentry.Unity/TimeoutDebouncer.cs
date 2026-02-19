using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Sentry.Unity.Integrations
{
    public class TimeoutDebouncer
    {
#pragma warning disable CS0618 // Type or member is obsolete - maintaining backwards compatibility
        private ErrorTimeDebounce _errorTimeDebounce = null!;       // Set in Register
        private LogTimeDebounce _logTimeDebounce = null!;           // Set in Register
        private WarningTimeDebounce _warningTimeDebounce = null!;   // Set in Register

        public TimeoutDebouncer(TimeSpan logTime, TimeSpan errorTime, TimeSpan warningTime)
        {
            _errorTimeDebounce = new ErrorTimeDebounce(errorTime);
            _logTimeDebounce = new LogTimeDebounce(logTime);
            _warningTimeDebounce = new WarningTimeDebounce(warningTime);
        }

        public async void Debounce(string message, LogType logType, string backtrace, DebouncerCaptureCallback capture)
        {
            bool shouldCapture = logType switch
            {
                LogType.Exception => _errorTimeDebounce.Debounced(),
                LogType.Error or LogType.Assert => _errorTimeDebounce.Debounced(),
                LogType.Log => _logTimeDebounce.Debounced(),
                LogType.Warning => _warningTimeDebounce.Debounced(),
                _ => true
            };

            if (shouldCapture)
            {
                capture(message, logType, backtrace, allowCaptureAsEvent: true);
            }
        }
#pragma warning restore CS0618 // Type or member is obsolete - maintaining backwards compatibility
    }
}
