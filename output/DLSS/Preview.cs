using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
public static class PreviewDlss
{
    public static async System.Threading.Tasks.Task<object> Main()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var oldSettings = SaveSystem.settingsData;
        var oldLoaded = SaveSystem.savesLoaded;
        var oldManager = GameSettingsManager.Instance;
        var oldPipeline = QualitySettings.renderPipeline;
        var oldRT = RenderTexture.active;
        RenderTexture rt = null;
        HDRenderPipelineAsset pipeline = null;
        Texture2D image = null;
        try
        {
            SaveSystem.savesLoaded = false;
            SaveSystem.settingsData = new SettingsData { dlss = true, qualityIndex = 0 };
            typeof(GameSettingsManager).GetProperty("Instance").SetValue(null, null);
            var cameraObject = new GameObject("DLSS Preview Camera", typeof(Camera), typeof(HDAdditionalCameraData));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.08f, .09f, .1f);
            var hd = cameraObject.GetComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = camera.backgroundColor;
            hd.allowDynamicResolution = true;
            hd.allowDeepLearningSuperSampling = true;
            var canvasObject = new GameObject("DLSS Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SettingsScreen.prefab"), canvasObject.transform);
            root.SetActive(true);
            var manager = root.GetComponent<GameSettingsManager>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GameSettingsManager).GetField("initialized", flags).SetValue(manager, true);
            manager.ShowSection(0);
            typeof(GameSettingsManager).GetMethod("UpdateVideoValues", flags).Invoke(manager, null);
            pipeline = UnityEngine.Object.Instantiate((QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline) as HDRenderPipelineAsset);
            var options = pipeline.currentPlatformRenderPipelineSettings;
            options.dynamicResolutionSettings = GameSettingsManager.ConfigureUpscaling(options.dynamicResolutionSettings, .65f, true, true);
            pipeline.currentPlatformRenderPipelineSettings = options;
            QualitySettings.renderPipeline = pipeline;
            rt = new RenderTexture(1920, 1080, 24) { useDynamicScale = true };
            rt.Create(); camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
            for (int i = 0; i < 12; i++)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                await System.Threading.Tasks.Task.Delay(80);
                camera.Render();
            }
            RenderTexture.active = rt;
            image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            File.WriteAllBytes("output/DLSS/settings-preview.png", image.EncodeToPNG());
            bool active = (bool)typeof(HDAdditionalCameraData).GetField("cameraCanRenderDLSS", flags).GetValue(hd);
            var hdCamera = HDCamera.GetOrCreate(camera);
            int dlssWidth = hdCamera.actualWidth, dlssHeight = hdCamera.actualHeight;
            options = pipeline.currentPlatformRenderPipelineSettings;
            options.dynamicResolutionSettings = GameSettingsManager.ConfigureUpscaling(options.dynamicResolutionSettings, .65f, false, true);
            pipeline.currentPlatformRenderPipelineSettings = options;
            hd.allowDeepLearningSuperSampling = false;
            for (int i = 0; i < 3; i++)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                await System.Threading.Tasks.Task.Delay(80);
                camera.Render();
            }
            return new { dlssSupported = GameSettingsManager.IsDLSSSupported, cameraDLSSActive = active,
                dlssWidth, dlssHeight, offWidth = HDCamera.GetOrCreate(camera).actualWidth,
                offHeight = HDCamera.GetOrCreate(camera).actualHeight,
                offDLSSActive = (bool)typeof(HDAdditionalCameraData).GetField("cameraCanRenderDLSS", flags).GetValue(hd),
                screenshot = "output/DLSS/settings-preview.png" };
        }
        finally
        {
            QualitySettings.renderPipeline = oldPipeline;
            RenderTexture.active = oldRT;
            EditorSceneManager.ClosePreviewScene(scene);
            if (pipeline) UnityEngine.Object.DestroyImmediate(pipeline);
            if (rt) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            if (image) UnityEngine.Object.DestroyImmediate(image);
            SaveSystem.settingsData = oldSettings; SaveSystem.savesLoaded = oldLoaded;
            typeof(GameSettingsManager).GetProperty("Instance").SetValue(null, oldManager);
        }
    }
}
