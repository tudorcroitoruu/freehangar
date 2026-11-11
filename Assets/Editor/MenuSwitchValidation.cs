using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Switch;

public static class MenuSwitchValidation
{
    public static void RunBatch()
    {
        InspectBuild();
        NintendoSaveValidation.CompileSwitchScripts();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
        var cameras = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Camera>(true)).ToArray();
        foreach (var camera in cameras)
        {
            camera.gameObject.SetActive(true);
            var rt = new RenderTexture(960, 540, 24);
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                rt.Create();
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt });
                RenderTexture.active = rt;
                image = new Texture2D(960, 540, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                File.WriteAllBytes("output/MenuSwitch/" + camera.name + ".png", image.EncodeToPNG());
                Debug.Log("Captured " + camera.name + "; enabled=" + camera.enabled + "; mask=" + camera.cullingMask);
            }
            finally { RenderTexture.active = previous; if (image) UnityEngine.Object.DestroyImmediate(image); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
        Debug.Log("MENU_SWITCH_VALIDATION_COMPLETE");
        ValidateButtons();
    }
    public static void InspectBuild()
    {
        var report = BuildReport.GetLatestReport();
        var lines = new System.Collections.Generic.List<string>();
        lines.Add("Project=" + Application.dataPath + "; Unity=" + Application.unityVersion);
        lines.Add("Playing=" + EditorApplication.isPlaying + "; pipeline=" + UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline);
        if (report != null)
        {
            lines.Add("Build=" + report.summary.platform + "; result=" + report.summary.result + "; started=" + report.summary.buildStartedAt);
            lines.Add("Output=" + report.summary.outputPath);
            foreach (var asset in report.packedAssets.SelectMany(p => p.contents).Select(p => p.sourceAssetPath).Distinct())
                if (asset.EndsWith(".asset") && (asset.Contains("/Settings/") || asset.Contains("HDRP"))) lines.Add("Included: " + asset);
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception || message.content.IndexOf("shader", StringComparison.OrdinalIgnoreCase) >= 0)
                        lines.Add(message.type + ": " + message.content);
        }
        Directory.CreateDirectory("output/MenuSwitch");
        File.WriteAllLines("output/MenuSwitch/build-inspection.txt", lines);
        Debug.Log(string.Join("\n", lines.Take(70)));
    }
    public static void ValidateButtons()
    {
        var desktop = InputSystem.AddDevice<Gamepad>();
        var nintendo = InputSystem.AddDevice<SwitchProControllerHID>();
        try
        {
            NUnit.Framework.Assert.AreSame(desktop.buttonSouth, MenuPadInput.ConfirmButton(desktop));
            NUnit.Framework.Assert.AreSame(desktop.buttonEast, MenuPadInput.CancelButton(desktop));
            NUnit.Framework.Assert.AreSame(nintendo.aButton, MenuPadInput.ConfirmButton(nintendo));
            NUnit.Framework.Assert.AreSame(nintendo.bButton, MenuPadInput.CancelButton(nintendo));
            Directory.CreateDirectory("output/MenuSwitch");
            File.WriteAllText("output/MenuSwitch/input-tests.txt", "PASS: desktop and Nintendo confirm/cancel control mappings. Device button-click test still required.");
            Debug.Log("PASS: desktop and Nintendo confirm/cancel control mappings.");
        }
        finally { InputSystem.RemoveDevice(nintendo); InputSystem.RemoveDevice(desktop); }
    }
}
