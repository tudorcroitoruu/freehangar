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
        if (!TryGetCamera(out var camera)) return;
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
        if (count == 0) Debug.LogWarning($"No held-tool pass found for {camera.name}.", camera);
    }

    [MenuItem(Root + "Toggle Stop NaNs")]
    private static void ToggleStopNaNs()
    {
        if (!TryGetCamera(out var camera)) return;
        if (!camera.TryGetComponent<HDAdditionalCameraData>(out var data))
        {
            Debug.LogWarning($"Camera {camera.name} has no HDRP camera data.", camera);
            return;
        }
        data.stopNaNs = !data.stopNaNs;
        Debug.Log($"Black-frame check: Stop NaNs {(data.stopNaNs ? "enabled" : "disabled")}. " +
            "This tests invalid shader pixels; it does not repair their source. Toggle again to restore, or exit Play mode.", camera);
    }

    private static bool TryGetCamera(out Camera camera)
    {
        camera = null;
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Black-frame diagnostics require Play Mode. Press Play, then choose the diagnostic again.");
            return false;
        }
        // Selection lets multi-camera scenes choose the intended view explicitly.
        var selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Camera>() : null;
        if (selected != null && selected.isActiveAndEnabled && selected.cameraType == CameraType.Game)
            camera = selected;
        else camera = Camera.main;
        if (camera != null) return true;
        foreach (var candidate in Camera.allCameras)
        {
            if (candidate.cameraType != CameraType.Game || candidate.targetTexture != null) continue;
            if (camera != null)
            {
                camera = null;
                Debug.LogWarning("Several game cameras are active. Select the gameplay camera in the Hierarchy, then choose the diagnostic again.");
                return false;
            }
            camera = candidate;
        }
        if (camera != null) return true;
        Debug.LogWarning("No active game camera found. Resume gameplay, then choose the diagnostic again.");
        return false;
    }
}
