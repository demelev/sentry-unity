using System;
using System.Runtime.CompilerServices;
using Sentry.Unity;
using UnityEngine;

public class BugFarmButtons : MonoBehaviour
{
    bool testExceptionsInUpdate = false;

    private void Awake()
    {
        Debug.Log("The 🐛s awaken!");
    }

    private void Start()
    {
        // Log messages are getting captured as breadcrumbs
        Debug.Log("Starting the 🦋-Farm");
        Debug.LogWarning("Here come the bugs 🐞🦋🐛🐜🕷!");
    }

    void Update()
    {
        if (testExceptionsInUpdate)
        {
            string nullString = null;
            int len = nullString.Length;
        }
    }

    public void ThrowUnhandledException()
    {
        Debug.Log("Throwing an unhandled 🕷 exception!");
        DoSomeWorkHere();
    }

    public void ThrowExceptionButCatch()
    {
        Debug.Log("Throwing an exception but catching it! 🐜");

        try
        {
            DoSomeWorkHere();
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
        }
    }

    private void DoSomeWorkHere()
    {
        if (CheckSomeFakeWork())
        {
            DoSomeWorkThere();
        }
    }

    private void DoSomeWorkThere()
    {
        if (CheckSomeFakeWork())
        {
            throw new CustomException("Exception from an exceptional lady beetle 🐞!");
        }
    }

    public void CaptureMessage()
    {
        if (CheckSomeFakeWork())
        {
            // Messages do not have a stacktrace attached by default. This is an opt-in feature.
            // Note: That stack traces generated for message events are provided without line numbers. See known limitations
            // https://docs.sentry.io/platforms/unity/troubleshooting/known-limitations/#line-numbers-missing-in-events-captured-through-debuglogerror-or-sentrysdkcapturemessage
            SentrySdk.CaptureMessage("🕷️🕷️🕷️ Spider message 🕷️🕷️🕷️🕷️");
        }
    }

    public void LogError()
    {
        if (CheckSomeFakeWork())
        {
            // Error logs get captured as messages and do not have a stacktrace attached by default. This is an opt-in feature.
            // Note: That stack traces generated for message events are provided without line numbers. See known limitations
            // https://docs.sentry.io/platforms/unity/troubleshooting/known-limitations/#line-numbers-missing-in-events-captured-through-debuglogerror-or-sentrysdkcapturemessage
            Debug.LogError("This is a 'Debug.LogError()' message.");
        }
    }

    // Repeated Log and LogError.
    // Alternating message breaks grouping of messages.
    public void LogMultipleError()
    {
        if (CheckSomeFakeWork())
        {
            // Error logs get captured as messages and do not have a stacktrace attached by default. This is an opt-in feature.
            // Note: That stack traces generated for message events are provided without line numbers. See known limitations
            // https://docs.sentry.io/platforms/unity/troubleshooting/known-limitations/#line-numbers-missing-in-events-captured-through-debuglogerror-or-sentrysdkcapturemessage
            Debug.Log("Simple Log information"); // This should be captured
            Debug.LogError("'Debug.LogError()' message outside of loop"); // This should be captured

            for (int i = 0; i<3; i++)
            {
                Debug.Log($"This is simple log in loop idx {i}");
                Debug.LogError("This is a 'Debug.LogError()' message in loop");   // this captures one error with log message about repeats
            }
            // "Simple Log information"
            // "'Debug.LogError()' message outside of loop"
            // "This is simple log in loop idx 0"
            // "This is a 'Debug.LogError()' message in loop"
            // "This is simple log in loop idx 1"
            // "This is a 'Debug.LogError()' message in loop (1 times)"
            // "This is simple log in loop idx 2"
            // "This is a 'Debug.LogError()' message in loop (1 times)"

            testExceptionsInUpdate = true;
            WaitAndResetExceptionsInUpdate();
        }
    }
    // Wait for 3 frames
    private async void WaitAndResetExceptionsInUpdate()
    {
        await System.Threading.Tasks.Task.Yield();
        await System.Threading.Tasks.Task.Yield();
        await System.Threading.Tasks.Task.Yield();
        testExceptionsInUpdate = false;
    }

    // Repeated Log and LogError.
    // The same messages do not breaks grouping of messages.
    public void LogRepeatingErrors()
    {
        if (CheckSomeFakeWork())
        {
            // Error logs get captured as messages and do not have a stacktrace attached by default. This is an opt-in feature.
            // Note: That stack traces generated for message events are provided without line numbers. See known limitations
            // https://docs.sentry.io/platforms/unity/troubleshooting/known-limitations/#line-numbers-missing-in-events-captured-through-debuglogerror-or-sentrysdkcapturemessage
            Debug.Log("Simple Log information"); // This should be captured
            Debug.LogError("'Debug.LogError()' message outside of loop"); // This should be captured

            for (int i = 0; i<5; i++)
            {
                Debug.Log($"This is simple log in loop the same msg");
                if (i % 2 == 0)
                    Debug.LogError("This is a 'Debug.LogError()' message in loop");   // this captures one error with log message about repeats
            }
            // "Simple Log information"
            // "'Debug.LogError()' message outside of loop"
            // "This is simple log in loop the same msg"
            // "This is a 'Debug.LogError()' message in loop"
            // "This is simple log in loop the same msg (2 times)"
            // "This is a 'Debug.LogError()' message in loop (2 times)"
        }
    }


    public void LogException()
    {
        if (CheckSomeFakeWork())
        {
            // Error logs get captured as messages and do not have a stacktrace attached by default. This is an opt-in feature.
            Debug.LogException(new NullReferenceException("Some bugs are harder to catch than others. 🦋"));
        }
    }

    // NoInlining ends up being inlined through L2CPP anyway. :(
    // We're checking some fake work here to prevent too aggressive optimization. That way, we can show off some proper
    // stack traces that are closer to real-world bugs and events.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool CheckSomeFakeWork() => DateTime.Now.Ticks > 0; // Always true but not optimizable

    private class CustomException : Exception
    {
        public CustomException(string message) : base(message)
        { }
    }
}
