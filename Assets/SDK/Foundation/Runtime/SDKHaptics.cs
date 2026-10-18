using System.Runtime.InteropServices;
using UnityEngine;

public enum HapticPreset
{
    Button,
    Selection,
    Toggle,
    LightImpact,
    MediumImpact,
    HeavyImpact,
    SoftImpact,
    RigidImpact,
    Success,
    Warning,
    Error,
    Explosion,
    Collision,
    Reward,
    Heartbeat,
    Tick,
    LongPress,
    ContinuousLight,
    ContinuousMedium,
    ContinuousHeavy
}

internal static class SDKHaptics
{
    public static bool IsSupported
    {
        get
        {
#if UNITY_IOS && !UNITY_EDITOR
            return SDKHapticsSupportsHaptics() != 0;
#else
            return false;
#endif
        }
    }

    public static void Play(HapticPreset preset)
    {
        if (SaveSystem.State.hapticsDisabled) return;
#if UNITY_IOS && !UNITY_EDITOR
        SDKHapticsPlayPreset((int)preset);
#endif
    }

    public static void PlayContinuous(float duration, float intensity, float sharpness)
    {
        if (SaveSystem.State.hapticsDisabled) return;
        duration = Mathf.Clamp(duration, 0.05f, 30f);
        intensity = Mathf.Clamp01(intensity);
        sharpness = Mathf.Clamp01(sharpness);
#if UNITY_IOS && !UNITY_EDITOR
        SDKHapticsPlayContinuous(duration, intensity, sharpness);
#endif
    }

    public static void Stop()
    {
#if UNITY_IOS && !UNITY_EDITOR
        SDKHapticsStop();
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int SDKHapticsSupportsHaptics();

    [DllImport("__Internal")]
    private static extern void SDKHapticsPlayPreset(int preset);

    [DllImport("__Internal")]
    private static extern void SDKHapticsPlayContinuous(float duration, float intensity, float sharpness);

    [DllImport("__Internal")]
    private static extern void SDKHapticsStop();
#endif
}
