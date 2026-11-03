using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
public static class ValidateSmoothGrain
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Set(object o, string key, object value) => o.GetType().GetField(key, Private).SetValue(o, value);
    public static string Run()
    {
        var root = new GameObject("Smooth grain preview"); root.SetActive(false);
        root.transform.position = new Vector3(10000, 1000, 10000);
        var cameraObject = new GameObject("Grain validation camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.CopyFrom(SceneView.lastActiveSceneView.camera);
        cameraObject.AddComponent<HDAdditionalCameraData>();
        var target = RenderTexture.GetTemporary(900, 700, 24);
        var previous = RenderTexture.active;
        var image = new Texture2D(900, 700, TextureFormat.RGB24, false);
        Mesh kernel = null;
        try
        {
            var pile = root.AddComponent<GrainPile>();
            Set(pile, "fitToWalls", false); Set(pile, "size", 2f); Set(pile, "initialHeight", .7f); Set(pile, "resolution", 33);
            var surface = root.AddComponent<CornSurfaceKernels>();
            kernel = (Mesh)typeof(CornSurfaceKernels).GetMethod("CreateLodMesh", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { 8 });
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/GrainPrototype/Grains/Grain Kernels.mat");
            Set(surface, "pile", pile); Set(surface, "kernelMesh", kernel); Set(surface, "kernelMaterial", material);
            root.SetActive(true);
            root.GetComponent<MeshRenderer>().enabled = false;
            camera.transform.position = root.transform.position + new Vector3(1.4f, 1.5f, -2.3f);
            camera.transform.LookAt(root.transform.position + Vector3.up * .3f);
            camera.fieldOfView = 45;
            camera.targetTexture = target;
            var visual = (GrainHeightField)typeof(GrainPile).GetField("visualField", Private).GetValue(pile);
            for (int frame = 0; frame < 2; frame++)
            {
                if (frame > 0)
                {
                    visual.Scoop(0, 0, .45f, .4f, 1, .45f);
                    Set(surface, "uploadedRevision", -1);
                    typeof(CornSurfaceKernels).GetMethod("UploadSurface", Private).Invoke(surface, null);
                }
                camera.Render(); camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 900, 700), 0, 0); image.Apply();
                File.WriteAllBytes("output/PlayerTools/grain-gpu-" + frame + ".png", image.EncodeToPNG());
            }
            var errors = ShaderUtil.GetShaderMessages(material.shader).Where(x => x.severity.ToString() == "Error").Select(x => x.message).ToArray();
            if (errors.Length > 0) throw new Exception(string.Join("\n", errors));
            return "Rendered both grain states; shader errors: 0; GPU surface: " + typeof(CornSurfaceKernels).GetField("gpuSurface", Private).GetValue(surface);
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(image); if (kernel != null) UnityEngine.Object.DestroyImmediate(kernel);
        }
    }
}
