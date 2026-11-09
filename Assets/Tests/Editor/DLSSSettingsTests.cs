using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

public class DLSSSettingsTests
{
    [TestCase(.65f, true, true)]
    [TestCase(1f, true, true)]
    [TestCase(.65f, true, false)]
    [TestCase(1f, true, false)]
    [TestCase(.85f, false, true)]
    [TestCase(1f, false, true)]
    public void DLSSUsesOptimalScalingAndUnsupportedOrDisabledUsesPreset(float scale, bool requested, bool supported)
    {
        var source = GlobalDynamicResolutionSettings.NewDefault();
        source.advancedUpscalerNames = new List<string> { "STP" };
        var result = GameSettingsManager.ConfigureUpscaling(source, scale, requested, supported);
        bool enabled = requested && supported;
        Assert.AreEqual(enabled || scale < 1f, result.enabled);
        Assert.AreEqual(!enabled, result.forceResolution);
        Assert.AreEqual(scale * 100, result.forcedPercentage, .001f);
        Assert.AreEqual(DynamicResUpscaleFilter.CatmullRom, result.upsampleFilter);
        CollectionAssert.AreEqual(new[] { "STP" }, source.advancedUpscalerNames, "Do not mutate the authored asset's shared list.");
        if (enabled)
        {
            CollectionAssert.AreEqual(new[] { "DLSS" }, result.advancedUpscalerNames);
            Assert.IsTrue(result.DLSSUseOptimalSettings);
            Assert.AreEqual((uint)UnityEngine.NVIDIA.DLSSQuality.Balanced, result.DLSSPerfQualitySetting);
        }
        else Assert.IsEmpty(result.advancedUpscalerNames);
    }

    [Test]
    public void DLSSPreferenceRoundTripsAndOldSettingsDefaultOff()
    {
        Assert.IsTrue(JsonUtility.FromJson<SettingsData>(JsonUtility.ToJson(new SettingsData { dlss = true })).dlss);
        Assert.IsFalse(JsonUtility.FromJson<SettingsData>("{\"vSync\":true}").dlss);
    }

    [Test]
    public void PrefabsAndPipelineHaveDLSSWiredBelowVSync()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SettingsScreen.prefab");
        var so = new SerializedObject(root.GetComponent<GameSettingsManager>());
        var toggle = (Toggle)so.FindProperty("dlssToggle").objectReferenceValue;
        Assert.IsNotNull(toggle);
        Assert.IsNotNull(so.FindProperty("dlssStatusText").objectReferenceValue);
        var rows = so.FindProperty("videoSettings");
        Assert.AreEqual(6, rows.arraySize);
        Assert.AreEqual("vSyn", rows.GetArrayElementAtIndex(3).objectReferenceValue.name);
        Assert.AreEqual("DLSS", rows.GetArrayElementAtIndex(4).objectReferenceValue.name);
        Assert.AreEqual("Quality", rows.GetArrayElementAtIndex(5).objectReferenceValue.name);
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Player.prefab");
        var camera = player.GetComponentInChildren<HDAdditionalCameraData>(true);
        Assert.IsNotNull(camera);
        Assert.IsTrue(camera.allowDynamicResolution);
        Assert.IsTrue(camera.allowDeepLearningSuperSampling);
        Assert.IsFalse(camera.deepLearningSuperSamplingUseCustomQualitySettings);
        var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/HDRPDefaultResources/HDRenderPipelineAsset.asset");
        var options = pipeline.currentPlatformRenderPipelineSettings.dynamicResolutionSettings;
        Assert.IsTrue(options.enabled);
        CollectionAssert.Contains(options.advancedUpscalerNames, "DLSS");
        Assert.IsTrue(options.DLSSUseOptimalSettings);
    }
}
