using UnityEngine;

/// <summary>Retries pending writes using real time, including while gameplay is paused.</summary>
public sealed class SwitchSavePump : MonoBehaviour
{
#if UNITY_SWITCH && !UNITY_EDITOR
    private double nextAttempt;
    private void Update()
    {
        if (Time.realtimeSinceStartupAsDouble < nextAttempt) return;
        nextAttempt = Time.realtimeSinceStartupAsDouble + 1;
        SaveBridge.FlushPending();
    }
    private void OnApplicationQuit() => SaveBridge.SaveOnQuit();
#endif
}
