using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
public static class InspectDlss
{
    public static object Main()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SettingsScreen.prefab");
        var manager = prefab.GetComponentInChildren<GameSettingsManager>(true);
        var so = new SerializedObject(manager);
        var vsync = (UnityEngine.UI.Toggle)so.FindProperty("vSyncToggle").objectReferenceValue;
        string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        var pipeline = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as HDRenderPipelineAsset;
        return new {
            manager = Path(manager.transform), toggle = Path(vsync.transform),
            rows = Enumerable.Range(0, so.FindProperty("videoSettings").arraySize).Select(i => so.FindProperty("videoSettings").GetArrayElementAtIndex(i).objectReferenceValue.name).ToArray(),
            hierarchy = vsync.transform.parent.parent.GetComponentsInChildren<Transform>(true).Select(t => new { path = Path(t), components = t.GetComponents<Component>().Select(c => c.GetType().Name).ToArray(), rect = t is RectTransform r ? r.anchoredPosition.ToString() + " size " + r.sizeDelta : "", text = t.GetComponent<UnityEngine.UI.Text>()?.text }).ToArray(),
            playerPrefabs = AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Where(p => p.IndexOf("player", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("controller", StringComparison.OrdinalIgnoreCase) >= 0).ToArray(),
            pipeline = AssetDatabase.GetAssetPath(pipeline),
            dynamicResolution = pipeline.currentPlatformRenderPipelineSettings.dynamicResolutionSettings,
            dlss = HDDynamicResolutionPlatformCapabilities.DLSSDetected
        };
    }
}
