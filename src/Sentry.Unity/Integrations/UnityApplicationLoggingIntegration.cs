using System;
using Sentry.Extensibility;
using Sentry.Infrastructure;
using Sentry.Integrations;
using UnityEngine;

namespace Sentry.Unity.Integrations;

public delegate void DebouncerCaptureCallback(string message, LogType logType, string stacktrace, bool allowCaptureAsEvent);
public delegate void DebouncerFunction(string message, LogType logType, string stacktrace, DebouncerCaptureCallback capture);

/// <summary>
/// Hooks into Unity's `Application.LogMessageReceived` to capture breadcrumbs for Debug log methods
/// and optionally capture LogError events. Does not handle `Debug.LogException` - exception handling
/// is done by UnityLogHandlerIntegration (non-WebGL) or UnityWebGLExceptionHandler (WebGL).
/// </summary>
internal class UnityApplicationLoggingIntegration : ISdkIntegration
{
    private readonly IApplication _application;
    private readonly ISystemClock _clock;

#pragma warning disable CS0618 // Type or member is obsolete - maintaining backwards compatibility
    private ErrorTimeDebounce _errorTimeDebounce = null!;       // Set in Register
    private LogTimeDebounce _logTimeDebounce = null!;           // Set in Register
    private WarningTimeDebounce _warningTimeDebounce = null!;   // Set in Register
#pragma warning restore CS0618

    private IHub _hub = null!;                                  // Set in Register
    private SentryUnityOptions _options = null!;                // Set in Register

    internal UnityApplicationLoggingIntegration(IApplication? application = null, ISystemClock? clock = null)
    {
        _application = application ?? ApplicationAdapter.Instance;
        _clock = clock ?? SystemClock.Clock;
    }

    public void Register(IHub hub, SentryOptions sentryOptions)
    {
        // These should never throw but in case they do...
        _hub = hub ?? throw new ArgumentException("Hub is null.");
        _options = sentryOptions as SentryUnityOptions ?? throw new ArgumentException("Options is not of type 'SentryUnityOptions'.");

#pragma warning disable CS0618 // Type or member is obsolete - maintaining backwards compatibility
        _logTimeDebounce = new LogTimeDebounce(_options.DebounceTimeLog);
        _warningTimeDebounce = new WarningTimeDebounce(_options.DebounceTimeWarning);
        _errorTimeDebounce = new ErrorTimeDebounce(_options.DebounceTimeError);
#pragma warning restore CS0618

        _application.LogMessageReceived += OnLogMessageReceived;
        _application.Quitting += OnQuitting;
    }

    internal void OnLogMessageReceived(string message, string stacktrace, LogType logType)
    {
        // We're not capturing the SDK's own logs
        if (message.StartsWith(UnityLogger.LogTag))
        {
            return;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (_options?.EnableLogDebouncing is true && _options?.Debouncer != null)
#pragma warning restore CS0618 // Type or member is obsolete
        {
            _options.Debouncer(message, logType, stacktrace, Capture);
        }
        else
        {
            Capture(message, logType, stacktrace);
        }
    }

    private void Capture(string message, LogType logType, string stacktrace, bool allowCaptureAsEvent = true)
    {
        if (allowCaptureAsEvent)
            ProcessError(message, stacktrace, logType);

        ProcessBreadcrumbs(message, logType);
        ProcessStructuredLog(message, logType);
    }

    private void ProcessError(string message, string stacktrace, LogType logType)
    {
        if (logType is not (LogType.Error or LogType.Assert) || !_options.CaptureLogErrorEvents)
        {
            return;
        }

        if (_options.Throttler is { } throttler && !throttler.ShouldCaptureEvent(message, stacktrace, logType))
        {
            _options.LogDebug("Error event throttled: {0}", message);
            return;
        }

        _options.LogDebug("Error capture for 'Debug.LogError' is enabled. Capturing message.");

        if (_options.AttachStacktrace && !string.IsNullOrEmpty(stacktrace))
        {
            var evt = UnityLogEventFactory.CreateMessageEvent(message, stacktrace, SentryLevel.Error, _options);
            _hub.CaptureEvent(evt);
        }
        else
        {
            _hub.CaptureMessage(message, level: SentryLevel.Error);
        }
    }

    private void ProcessBreadcrumbs(string message, LogType logType)
    {
        if (logType is LogType.Exception)
        {
            // Capturing of breadcrumbs for exceptions happens inside the .NET SDK
            return;
        }

        // Breadcrumb collection on top of structure log capture must be opted in
        if (_options is { EnableLogs: true, AddBreadcrumbsWithStructuredLogs: false })
        {
            return;
        }

        if (_options.Throttler is { } throttler && !throttler.ShouldCaptureBreadcrumb(message, logType))
        {
            _options.LogDebug("Breadcrumb throttled: ({0}): {1}", logType, message);
            return;
        }

        if (_options.AddBreadcrumbsForLogType.TryGetValue(logType, out var value) && value)
        {
            _options.LogDebug("Adding breadcrumb for log message of type: {0}", logType);
            _hub.AddBreadcrumb(message: message, category: "unity.logger", level: ToBreadcrumbLevel(logType));
        }
    }

    private void ProcessStructuredLog(string message, LogType logType)
    {
        if (!_options.EnableLogs || !_options.CaptureStructuredLogsForLogType.TryGetValue(logType, out var captureLog) || !captureLog)
        {
            return;
        }

        if (_options.Throttler is { } throttler && !throttler.ShouldCaptureStructuredLog(message, logType))
        {
            _options.LogDebug("Structured log throttled: ({0}): {1}", logType, message);
            return;
        }

        _options.LogDebug("Capturing structured log message of type '{0}'.", logType);

        _hub.GetTraceIdAndSpanId(out var traceId, out var spanId);
        SentryLog log = new(_clock.GetUtcNow(), traceId, ToLogLevel(logType), message) { SpanId = spanId };

        log.SetDefaultAttributes(_options, UnitySdkInfo.Sdk);
        log.SetOrigin("auto.log.unity");

        _hub.Logger.CaptureLog(log);
    }

    private void OnQuitting() => _application.LogMessageReceived -= OnLogMessageReceived;

    private static BreadcrumbLevel ToBreadcrumbLevel(LogType logType)
        => logType switch
        {
            LogType.Assert => BreadcrumbLevel.Error,
            LogType.Error => BreadcrumbLevel.Error,
            LogType.Exception => BreadcrumbLevel.Error,
            LogType.Log => BreadcrumbLevel.Info,
            LogType.Warning => BreadcrumbLevel.Warning,
            _ => BreadcrumbLevel.Info
        };

    private static SentryLogLevel ToLogLevel(LogType logType)
        => logType switch
        {
            LogType.Assert => SentryLogLevel.Error,
            LogType.Error => SentryLogLevel.Error,
            LogType.Exception => SentryLogLevel.Error,
            LogType.Log => SentryLogLevel.Info,
            LogType.Warning => SentryLogLevel.Warning,
            _ => SentryLogLevel.Info
        };
}
