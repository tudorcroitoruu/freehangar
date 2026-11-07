using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>Play-mode isolation checks for black frames. Never saves scene or prefab changes.</summary>
public static class BlackFrameDiagnostics
{
    private const string Root = "Tools/Rendering/Black frame diagnostics/";

    [MenuItem(Root + "Toggle held-tool pass")]
    private static void ToggleHeldTools()
    {
        var camera = Camera.main;
        int count = 0;
        foreach (var volume in Object.FindObjectsByType<CustomPassVolume>(FindObjectsSortMode.None))
        foreach (var pass in volume.customPasses)
        {
            if (!(pass is HeldToolPass held) || held.worldCamera != camera) continue;
            held.enabled = !held.enabled;
            count++;
            Debug.Log($"Black-frame check: held-tool pass {(held.enabled ? "enabled" : "disabled")}. " +
                "Repeat the same movement at the pile. Toggle again to restore, or exit Play mode.");
        }
        if (count == 0) Debug.LogWarning("No held-tool pass found for Camera.main.");
    }

    [MenuItem(Root + "Toggle Stop NaNs")]
    private static void ToggleStopNaNs()
    {
        var camera = Camera.main;
        if (camera == null || !camera.TryGetComponent<HDAdditionalCameraData>(out var data))
        {
            Debug.LogWarning("No HDRP Camera.main found.");
            return;
        }
        data.stopNaNs = !data.stopNaNs;
        Debug.Log($"Black-frame check: Stop NaNs {(data.stopNaNs ? "enabled" : "disabled")}. " +
            "This tests invalid shader pixels; it does not repair their source. Toggle again to restore, or exit Play mode.", camera);
    }

    [MenuItem(Root + "Toggle held-tool pass", true)]
    [MenuItem(Root + "Toggle Stop NaNs", true)]
    private static bool CanRun() => EditorApplication.isPlaying && Camera.main != null;
}
