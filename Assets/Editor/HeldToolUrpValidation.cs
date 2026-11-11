using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class HeldToolUrpValidation
{
    public static void Apply()
    {
        const string path = "Assets/Resources/Player.prefab";
        Directory.CreateDirectory("MaterialConversionBackups/URP-HeldCamera");
        string backup = "MaterialConversionBackups/URP-HeldCamera/Player.prefab";
        if (!File.Exists(backup)) File.Copy(path, backup);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var driver in root.GetComponentsInChildren<HeldToolCamera>(true)) Configure(driver);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        foreach (var driver in Resources.FindObjectsOfTypeAll<HeldToolCamera>().Where(d => d.gameObject.scene.IsValid() && d.gameObject.scene.isLoaded))
        {
            Configure(driver);
            EditorSceneManager.MarkSceneDirty(driver.gameObject.scene);
        }
        Debug.Log("Saved URP held-tool camera stack in Player.prefab; configured loaded instances. Unsaved scene edits preserved.");
    }
    static void Configure(HeldToolCamera driver)
    {
        driver.ConfigureCameras();
        var fields = new SerializedObject(driver);
        var tool = (Camera)fields.FindProperty("toolCamera").objectReferenceValue;
        var data = new SerializedObject(tool.GetUniversalAdditionalCameraData());
        data.FindProperty("m_ClearDepth").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(driver);
    }
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Held camera validation: " + message); }

    public static void CapturePlayer()
    {
        var driver = Resources.FindObjectsOfTypeAll<HeldToolCamera>().First(d => d.gameObject.scene.IsValid() && d.gameObject.activeInHierarchy);
        driver.ConfigureCameras();
        var fields = new SerializedObject(driver);
        var world = (Camera)fields.FindProperty("worldCamera").objectReferenceValue;
        var tool = (Camera)fields.FindProperty("toolCamera").objectReferenceValue;
        var rt = new RenderTexture(1200, 850, 24); Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            rt.Create(); RenderPipeline.SubmitRenderRequest(world, new RenderPipeline.StandardRequest { destination = rt });
            RenderTexture.active = rt; image = new Texture2D(1200,850,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1200,850),0,0); image.Apply();
            Directory.CreateDirectory("output/Rendering/URPHeldCamera");
            File.WriteAllBytes("output/Rendering/URPHeldCamera/Player.png",image.EncodeToPNG());
            Debug.Log($"Player camera: {world.name}; tool={tool.name}; enabled={tool.enabled}; type={tool.GetUniversalAdditionalCameraData().renderType}; stack entries={world.GetUniversalAdditionalCameraData().cameraStack.Count(c => c == tool)}");
        }
        finally { RenderTexture.active = previous; if(image)Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt); }
    }

    public static void Validate()
    {
        var root = new GameObject("URP held camera validation"); root.SetActive(false);
        var worldGo = new GameObject("World"); worldGo.transform.SetParent(root.transform);
        var toolGo = new GameObject("Tool"); toolGo.transform.SetParent(worldGo.transform);
        var world = worldGo.AddComponent<Camera>(); var tool = toolGo.AddComponent<Camera>();
        var driver = worldGo.AddComponent<HeldToolCamera>();
        var data = new SerializedObject(driver);
        data.FindProperty("worldCamera").objectReferenceValue = world;
        data.FindProperty("toolCamera").objectReferenceValue = tool;
        data.FindProperty("toolLayer").intValue = 1 << 31; data.ApplyModifiedPropertiesWithoutUndo();
        var red = new Material(Shader.Find("Universal Render Pipeline/Unlit")); red.SetColor("_BaseColor", Color.red);
        var green = new Material(red); green.SetColor("_BaseColor", Color.green);
        var rt = new RenderTexture(128,128,24);
        Texture2D image = null;
        try
        {
            world.transform.position = new Vector3(10000,10000,10000);
            world.clearFlags = CameraClearFlags.SolidColor; world.backgroundColor = Color.blue;
            world.cullingMask = (1 << 30) | (1 << 31); world.nearClipPlane = .1f; world.farClipPlane = 20;
            world.fieldOfView = 65; world.enabled = false;
            var occluder = GameObject.CreatePrimitive(PrimitiveType.Cube); occluder.transform.SetParent(root.transform);
            occluder.layer = 30; occluder.transform.position = world.transform.position + Vector3.forward * 2;
            occluder.GetComponent<Renderer>().sharedMaterial = green;
            var held = GameObject.CreatePrimitive(PrimitiveType.Cube); held.transform.SetParent(root.transform);
            held.layer = 31; held.transform.position = world.transform.position + Vector3.forward * 4;
            held.GetComponent<Renderer>().sharedMaterial = red;
            root.SetActive(true);
            driver.ConfigureCameras(); driver.ConfigureCameras();
            var baseData = world.GetUniversalAdditionalCameraData(); var overlay = tool.GetUniversalAdditionalCameraData();
            Check(baseData.renderType == CameraRenderType.Base && overlay.renderType == CameraRenderType.Overlay, "base/overlay types");
            Check(baseData.cameraStack.Count(c => c == tool) == 1, "stack must contain overlay exactly once");
            Check(overlay.clearDepth && tool.enabled, "independent tool depth and enabled overlay");
            Check((world.cullingMask & (1 << 31)) == 0 && tool.cullingMask == 1 << 31, "layer isolation");
            Check(tool.fieldOfView == world.fieldOfView && Mathf.Approximately(tool.nearClipPlane,.01f), "projection synchronization");
            rt.Create();
            RenderPipeline.SubmitRenderRequest(world, new RenderPipeline.StandardRequest { destination = rt });
            var previous = RenderTexture.active;
            try { RenderTexture.active = rt; image = new Texture2D(128,128,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,128,128),0,0); image.Apply(); }
            finally { RenderTexture.active = previous; }
            var center = image.GetPixel(64,64);
            Directory.CreateDirectory("output/Rendering/URPHeldCamera");
            File.WriteAllBytes("output/Rendering/URPHeldCamera/Overlay.png",image.EncodeToPNG());
            Check(center.r > .6f && center.g < .2f, "held object must render over nearer world geometry: " + center);
            var shovelGo = new GameObject("Inactive test shovel"); shovelGo.SetActive(false); shovelGo.transform.SetParent(worldGo.transform);
            var shovel = shovelGo.AddComponent<ShovelTool>();
            var pileGo = new GameObject("Inactive test pile"); pileGo.SetActive(false); pileGo.transform.SetParent(root.transform);
            var pile = pileGo.AddComponent<GrainPile>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ShovelTool).GetField("pendingPile", flags).SetValue(shovel, pile);
            typeof(ShovelTool).GetField("<IsBusy>k__BackingField", flags).SetValue(shovel, true);
            driver.ConfigureCameras();
            Check(shovel.IsScooping && !tool.enabled && (world.cullingMask & (1 << 31)) != 0, "scooping uses world depth");
            RenderPipeline.SubmitRenderRequest(world, new RenderPipeline.StandardRequest { destination = rt });
            previous = RenderTexture.active;
            try { RenderTexture.active = rt; image.ReadPixels(new Rect(0,0,128,128),0,0); image.Apply(); }
            finally { RenderTexture.active = previous; }
            center = image.GetPixel(64,64);
            File.WriteAllBytes("output/Rendering/URPHeldCamera/Scooping.png",image.EncodeToPNG());
            Check(center.g > .6f && center.r < .2f, "world must occlude inserted tool: " + center);
            typeof(ShovelTool).GetField("<IsBusy>k__BackingField", flags).SetValue(shovel, false);
            driver.ConfigureCameras();
            Check(tool.enabled && (world.cullingMask & (1 << 31)) == 0, "carrying restores overlay after scooping");
            driver.enabled = false;
            Check(!baseData.cameraStack.Contains(tool) && !tool.enabled, "disable removes overlay");
            driver.enabled = true;
            Check(baseData.cameraStack.Count(c => c == tool) == 1 && tool.enabled, "reenable restores stack");
            File.WriteAllText("output/Rendering/URPHeldCamera/validation.txt", "PASS: camera types, unique stack membership, depth clear, enabled overlay, layer masks, FOV/near plane, rendered overlay and scoop occlusion, return to carrying, disable/reenable cleanup.");
            Debug.Log("URP held camera validation PASS, including rendered overlay and scooping occlusion.");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(red); Object.DestroyImmediate(green); if(image)Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt); }
    }
}
