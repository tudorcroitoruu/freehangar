using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class UrpQualitySettingsTests
{
    [Test]
    public void PresetsUseRuntimeUrpCloneAndRestoreAuthoredPipeline()
    {
        var source = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        Assert.IsNotNull(source);
        var originalQuality = QualitySettings.renderPipeline;
        float originalScale = source.renderScale;
        var previousSettings = SaveSystem.settingsData;
        var root = new GameObject("URP quality test"); root.SetActive(false);
        var apply = typeof(GameSettingsManager).GetMethod("ApplyQuality", BindingFlags.NonPublic | BindingFlags.Instance);
        var release = typeof(GameSettingsManager).GetMethod("ReleaseRuntimePipeline", BindingFlags.NonPublic | BindingFlags.Static);
        try
        {
            var manager = root.AddComponent<GameSettingsManager>();
            SaveSystem.settingsData = new SettingsData { qualityIndex = 0 };
            apply.Invoke(manager, null);
            var runtime = GameSettingsManager.ActiveRenderPipeline;
            Assert.IsNotNull(runtime); Assert.AreNotSame(source, runtime);
            Assert.AreSame(runtime, QualitySettings.renderPipeline);
            Assert.AreEqual(.65f, runtime.renderScale, .001f);
            Assert.AreEqual(1, runtime.msaaSampleCount);
            SaveSystem.settingsData.qualityIndex = 3;
            apply.Invoke(manager, null);
            Assert.AreSame(runtime, GameSettingsManager.ActiveRenderPipeline);
            Assert.AreEqual(1f, runtime.renderScale);
            Assert.AreEqual(4, runtime.shadowCascadeCount);
            Assert.AreEqual(originalScale, source.renderScale);
        }
        finally
        {
            release.Invoke(null, null);
            SaveSystem.settingsData = previousSettings;
            Object.DestroyImmediate(root);
        }
        Assert.AreSame(originalQuality, QualitySettings.renderPipeline);
    }
}
