using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.HighDefinition;
public static class PreviewSellingBlocker
{
    public static object Main()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var oldTarget = RenderTexture.active;
        var outlines = new List<Outline>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var target = new RenderTexture(1280, 720, 24);
        Texture2D image = null;
        try
        {
            var vehicle = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Vehicles/5_Pick_up_Driveable"));
            SceneManager.MoveGameObjectToScene(vehicle, scene);
            vehicle.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var rig = vehicle.GetComponentInChildren<BicycleController>(true);
            var highlight = rig.GetComponent<InteractionHighlight>();
            if (highlight == null) highlight = rig.gameObject.AddComponent<InteractionHighlight>();
            highlight.SetWarning(highlight, true);
            foreach (var outline in vehicle.GetComponentsInChildren<Outline>(true))
            {
                if (!outline.enabled) continue;
                typeof(Outline).GetMethod("Awake", flags).Invoke(outline, null);
                typeof(Outline).GetMethod("OnEnable", flags).Invoke(outline, null);
                typeof(Outline).GetMethod("Update", flags).Invoke(outline, null);
                outlines.Add(outline);
            }
            var cameraObject = new GameObject("Blocker preview", typeof(Camera), typeof(HDAdditionalCameraData));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            var hd = cameraObject.GetComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = new Color(.08f, .1f, .12f);
            hd.allowDynamicResolution = false;
            Bounds bounds = default;
            bool found = false;
            foreach (var renderer in vehicle.GetComponentsInChildren<MeshRenderer>())
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            camera.transform.position = bounds.center + new Vector3(1, .65f, -1).normalized * bounds.size.magnitude * 1.2f;
            camera.transform.LookAt(bounds.center);
            camera.targetTexture = target;
            target.Create();
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes("output/SellingBlockers/red-ping.png", image.EncodeToPNG());
            return new { outlines = outlines.Count, warning = highlight.IsWarningVisible, screenshot = "output/SellingBlockers/red-ping.png" };
        }
        finally
        {
            foreach (var outline in outlines)
            {
                typeof(Outline).GetMethod("OnDisable", flags).Invoke(outline, null);
                foreach (var field in new[] { "outlineMaskMaterial", "outlineFillMaterial" })
                    if (typeof(Outline).GetField(field, flags).GetValue(outline) is Material material) UnityEngine.Object.DestroyImmediate(material);
            }
            RenderTexture.active = oldTarget;
            EditorSceneManager.ClosePreviewScene(scene);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
            if (image) UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
