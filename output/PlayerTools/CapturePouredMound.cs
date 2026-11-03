using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using System.Reflection;
using System.IO;
public static class CapturePouredMound
{
    public static string Run()
    {
        var source = Object.FindFirstObjectByType<GrainPile>();
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = new Vector3(10000, 999.9f, 10000);
        ground.transform.localScale = new Vector3(20, .2f, 20);
        ground.transform.rotation = Quaternion.Euler(0, 0, 12);
        var material = new Material(Shader.Find("HDRP/Lit"));
        material.SetColor("_BaseColor", new Color(.25f, .13f, .055f));
        ground.GetComponent<Renderer>().sharedMaterial = material;
        Physics.SyncTransforms();
        ground.GetComponent<Collider>().Raycast(new Ray(new Vector3(10000, 1010, 10000), Vector3.down), out var groundHit, 20);
        var pile = GrainPile.CreateDroppedPile(groundHit.point, source, 6.5f);
        var go = new GameObject("Mound preview camera");
        var camera = go.AddComponent<Camera>(); camera.CopyFrom(SceneView.lastActiveSceneView.camera); camera.enabled = false;
        go.AddComponent<HDAdditionalCameraData>();
        var rt = RenderTexture.GetTemporary(900, 700, 24);
        var old = RenderTexture.active;
        var image = new Texture2D(900, 700, TextureFormat.RGB24, false);
        try
        {
            var cargo = new GrainCargo(); cargo.Add(GrainType.Corn, 6.5f);
            pile.Deposit(pile.transform.position, cargo, GrainPile.DropRadius(6.5f));
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = (GrainHeightField)typeof(GrainPile).GetField("field", flags).GetValue(pile);
            for (int i = 0; i < 600; i++) field.Settle(32, .45f);
            typeof(GrainPile).GetMethod("RefreshSurface", flags).Invoke(pile, null);
            pile.GetComponent<CornSurfaceKernels>().Rebuild();
            camera.transform.position = pile.transform.position + new Vector3(-3, 1.4f, -4);
            camera.transform.LookAt(pile.transform.position + Vector3.up * .4f); camera.fieldOfView = 48;
            camera.targetTexture = rt; camera.Render(); camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 900, 700), 0, 0); image.Apply();
            File.WriteAllBytes("output/PlayerTools/grounded-mound.png", image.EncodeToPNG());
            return "Rendered settled 6.5 kg mound; collectible mass: " + pile.CollectibleKilograms;
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(ground); Object.DestroyImmediate(material); Object.DestroyImmediate(image); Object.DestroyImmediate(go); Object.DestroyImmediate(pile.gameObject);
        }
    }
}
