using System;
using UnityEngine;
#if UNITY_SWITCH && !UNITY_EDITOR
using UnityEngine.Switch;
#endif

public class SwitchExitHandling
{
    public static event Action OnBackground;
#if UNITY_SWITCH && !UNITY_EDITOR
    static void YourSystemMessageHandler(Notification.Message message)
    {
        switch (message)
        {
            case Notification.Message.ExitRequest:
                // Notification to the program of an request to exit.
                try { SaveBridge.SaveOnQuit(); }
                finally { RequestToClose(); }
                break;

            case Notification.Message.FocusStateChanged:
                // Notifies when there has been a change to the program's focus state.
                var state = Notification.GetCurrentFocusState();
                switch (state)
                {
                    case Notification.FocusState.InFocus:
                        // In-focus state.
                        break;
                    case Notification.FocusState.OutOfFocus:
                        // Out-of-focus state.
                        break;
                    case Notification.FocusState.Background:
                        // BG focus state.
                        try { OnBackground?.Invoke(); }
                        finally { SaveBridge.SaveOnBackground(); }
                        break;
                }
                //SaveBridge.SaveOnQuit();
                // Notification.SetResumeNotificationEnabled(false);
                break;
            case Notification.Message.Resume:
                // Notifies when the application resumes from a suspended state.
                //SaveBridge.SaveOnQuit();
                break;
            case Notification.Message.OperationModeChanged:
                // Notifies when the operation mode has changed between handheld mode and TV mode.
                //SaveBridge.SaveOnQuit();
                break;
            case Notification.Message.PerformanceModeChanged:
                // Notifies when the performance mode has changed between Normal mode and Boost mode.
                //SaveBridge.SaveOnQuit();
                break;
            default:
                // Unknown messages will be ignored.
                Debug.LogError("Unhandled message");
                //SaveBridge.SaveOnQuit();
                break;
        }

    }

    [RuntimeInitializeOnLoadMethod]
    static void OnRuntimeMethodLoad()
    {
        Notification.notificationMessageReceived -= YourSystemMessageHandler;
        Notification.notificationMessageReceived += YourSystemMessageHandler;
        Notification.EnterExitRequestHandlingSection();
        Notification.SetPerformanceModeChangedNotificationEnabled(true);
        Notification.SetResumeNotificationEnabled(true);
        Notification.SetOperationModeChangedNotificationEnabled(true);
        Notification.SetFocusHandlingMode(Notification.FocusHandlingMode.Notify);
    }

    static void RequestToClose()
    {
        Debug.Log("Attempting to close");
#if UNITY_SWITCH && !UNITY_EDITOR
        Notification.LeaveExitRequestHandlingSection();
#elif UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

#endif
}
