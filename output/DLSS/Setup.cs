using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
public static class SetupDlss
{
    public static object Main()
    {
        const string settingsPath = "Assets/Prefabs/UI/SettingsScreen.prefab";
        var root = PrefabUtility.LoadPrefabContents(settingsPath);
        try
        {
            var manager = root.GetComponent<GameSettingsManager>();
            var so = new SerializedObject(manager);
            var vsync = (Toggle)so.FindProperty("vSyncToggle").objectReferenceValue;
            var source = vsync.transform.parent;
            var parent = source.parent;
            var row = parent.Find("DLSS");
            if (row == null) row = UnityEngine.Object.Instantiate(source.gameObject, parent).transform;
            row.name = "DLSS";
            row.SetSiblingIndex(source.GetSiblingIndex() + 1);
            row.Find("Name").GetComponent<Text>().text = "DLSS";
            var unused = row.Find("ValueBg");
            if (unused != null) UnityEngine.Object.DestroyImmediate(unused.gameObject);
            var toggle = row.GetComponentInChildren<Toggle>(true);
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(false);
            var status = row.Find("Status");
            if (status == null) status = UnityEngine.Object.Instantiate(row.Find("Name").gameObject, row).transform;
            status.name = "Status";
            var text = status.GetComponent<Text>();
            text.text = "Off";
            text.fontSize = 17;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            var rect = (RectTransform)status;
            rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
            rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = new Vector2(-255, 0);
            rect.sizeDelta = new Vector2(220, 30);
            var rows = parent.GetComponentsInChildren<SelectedUI>(true).OrderBy(r => r.transform.GetSiblingIndex()).ToArray();
            var layout = parent.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 0;
            foreach (var item in rows) ((RectTransform)item.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 70);
            so.FindProperty("dlssToggle").objectReferenceValue = toggle;
            so.FindProperty("dlssStatusText").objectReferenceValue = text;
            var references = so.FindProperty("videoSettings");
            references.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++) references.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, settingsPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        const string playerPath = "Assets/Resources/Player.prefab";
        var player = PrefabUtility.LoadPrefabContents(playerPath);
        int cameras = 0;
        try
        {
            foreach (var camera in player.GetComponentsInChildren<HDAdditionalCameraData>(true))
            {
                camera.allowDynamicResolution = true;
                camera.allowDeepLearningSuperSampling = true;
                camera.deepLearningSuperSamplingUseCustomQualitySettings = false;
                camera.deepLearningSuperSamplingUseCustomAttributes = false;
                camera.deepLearningSuperSamplingUseOptimalSettings = true;
                EditorUtility.SetDirty(camera);
                cameras++;
            }
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        var paths = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:HDRenderPipelineAsset"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
            var settings = pipeline.currentPlatformRenderPipelineSettings;
            var resolution = settings.dynamicResolutionSettings;
            resolution.enabled = true;
            resolution.advancedUpscalerNames = new List<string> { "DLSS" };
            resolution.DLSSUseOptimalSettings = true;
            resolution.DLSSPerfQualitySetting = (uint)UnityEngine.NVIDIA.DLSSQuality.Balanced;
            resolution.dynResType = DynamicResolutionType.Software;
            resolution.upsampleFilter = DynamicResUpscaleFilter.CatmullRom;
            resolution.forceResolution = false;
            resolution.minPercentage = 25;
            resolution.maxPercentage = 100;
            settings.dynamicResolutionSettings = resolution;
            pipeline.currentPlatformRenderPipelineSettings = settings;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssetIfDirty(pipeline);
            paths.Add(path);
        }
        return new { cameras, pipelines = paths, balanced = (uint)UnityEngine.NVIDIA.DLSSQuality.Balanced, supported = GameSettingsManager.IsDLSSSupported };
    }
}
