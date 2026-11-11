using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class MaterialPipelineConverterValidation
{
    // Tests use temporary materials only; no scene or existing material is changed.
    [MenuItem("Tools/Rendering/Validate Material Pipeline Converter")]
    public static void Run()
    {
        string folder = "Assets/MaterialConverterTest-" + Guid.NewGuid().ToString("N");
        var objects = new List<UnityEngine.Object>();
        int checks = 0;
        try
        {
            var src = new Material(Shader.Find("Hidden/Tests/Legacy Lit Properties")); objects.Add(src);
            src.SetColor("_BaseColor", new Color(.2f, .3f, .4f, .7f));
            src.SetFloat("_Metallic", .3f);
            src.SetFloat("_Smoothness", .65f);
            src.SetFloat("_SurfaceType", 1);
            src.SetFloat("_DoubleSidedEnable", 1);
            var urp = MaterialPipelineConverter.ConvertMaterial(src, MaterialPipelineConverter.Target.URP, folder, "plain"); objects.Add(urp);
            Check(urp.shader.name == "Universal Render Pipeline/Lit", "HDRP to URP shader", ref checks);
            Check(Mathf.Abs(urp.GetFloat("_Smoothness") - .65f) < .001f, "scalar smoothness", ref checks);
            Check(urp.GetColor("_BaseColor") == src.GetColor("_BaseColor"), "base tint", ref checks);
            Check(urp.GetFloat("_Surface") == 1 && urp.renderQueue == 3000 && urp.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), "transparent render state", ref checks);
            Check(urp.GetFloat("_Cull") == 0, "double sided", ref checks);
            Check(src.shader.name == "Hidden/Tests/Legacy Lit Properties", "source untouched", ref checks);
            NUnit.Framework.Assert.Throws<NotSupportedException>(() => MaterialPipelineConverter.ConvertMaterial(urp, MaterialPipelineConverter.Target.HDRP, folder, "return"));
            var mask = new Texture2D(2, 2, TextureFormat.RGBA32, false, true); objects.Add(mask);
            mask.SetPixels(new[] { new Color(.2f, .4f, 1, .6f), new Color(.2f, .4f, 1, .6f), new Color(.2f, .4f, 1, .6f), new Color(.2f, .4f, 1, .6f) }); mask.Apply();
            src.SetTexture("_MaskMap", mask);
            src.SetFloat("_MetallicRemapMin", .1f); src.SetFloat("_MetallicRemapMax", .9f);
            src.SetFloat("_SmoothnessRemapMin", .2f); src.SetFloat("_SmoothnessRemapMax", .8f);
            var packed = MaterialPipelineConverter.ConvertMaterial(src, MaterialPipelineConverter.Target.URP, folder, "packed"); objects.Add(packed);
            string path = AssetDatabase.GetAssetPath(packed.GetTexture("_MetallicGlossMap"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = true; importer.SaveAndReimport();
            Color pixel = AssetDatabase.LoadAssetAtPath<Texture2D>(path).GetPixel(0, 0);
            Check(Mathf.Abs(pixel.r - .26f) < .015f && Mathf.Abs(pixel.g - .4f) < .015f && Mathf.Abs(pixel.a - .56f) < .015f, "mask channel remapping", ref checks);
            Check(!importer.sRGBTexture && packed.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), "linear map and shader keyword", ref checks);
            src.SetFloat("_MaterialID", 4);
            var spec = MaterialPipelineConverter.ConvertMaterial(src, MaterialPipelineConverter.Target.URP, folder, "spec"); objects.Add(spec);
            Check(spec.GetFloat("_WorkflowMode") == 0 && spec.GetTexture("_SpecGlossMap") != null, "specular conversion", ref checks);
            var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit")); objects.Add(unlit);
            unlit.SetColor("_BaseColor", Color.cyan);
            var unlitUrp = MaterialPipelineConverter.ConvertMaterial(unlit, MaterialPipelineConverter.Target.URP, folder, "unlit"); objects.Add(unlitUrp);
            Check(unlitUrp.shader.name.EndsWith("/Unlit") && unlitUrp.GetColor("_BaseColor") == Color.cyan, "HDRP unlit mapping", ref checks);
            var legacy = new Material(Shader.Find("Standard")); objects.Add(legacy);
            legacy.SetFloat("_Mode", 1); legacy.SetFloat("_Cutoff", .42f);
            var fallback = MaterialPipelineConverter.ConvertMaterial(legacy, MaterialPipelineConverter.Target.URP, folder, "legacy"); objects.Add(fallback);
            Check(fallback.IsKeywordEnabled("_ALPHATEST_ON") && fallback.renderQueue == 2450, "fallback alpha clipping", ref checks);
            var grain = new Material(Shader.Find("Freehangar/HDRP/Grain Kernels")); objects.Add(grain);
            grain.SetFloat("_KernelMode", 0); grain.SetVector("_GrainWeightsB", new Vector4(0,0,1,0));
            var grainUrp = MaterialPipelineConverter.ConvertMaterial(grain, MaterialPipelineConverter.Target.URP, folder, "grain"); objects.Add(grainUrp);
            Check(grainUrp.shader.name == "Freehangar/URP/Grain Kernels" && grainUrp.GetFloat("_KernelMode") == 0 && grainUrp.GetVector("_GrainWeightsB") == new Vector4(0,0,1,0), "custom grain shader and crop properties", ref checks);
            var tree = new Material(Shader.Find("FreeHangar/Tree Cutout Toon")); objects.Add(tree);
            tree.SetTexture("_TextureSample", mask); tree.SetFloat("_AlphaClipping", 1); tree.SetFloat("_AlphaCutoff", .42f);
            var treeUrp = MaterialPipelineConverter.ConvertMaterial(tree, MaterialPipelineConverter.Target.URP, folder, "tree"); objects.Add(treeUrp);
            Check(treeUrp.shader.name == "Freehangar/URP/Tree Cutout Toon" && treeUrp.GetTexture("_TextureSample") == mask && Mathf.Abs(treeUrp.GetFloat("_AlphaCutoff") - .42f) < .001f, "tree atlas and cutout preservation", ref checks);
            Debug.Log($"Material pipeline converter validation PASS: {checks} checks.");
        }
        finally
        {
            foreach (var obj in objects) if (obj) UnityEngine.Object.DestroyImmediate(obj);
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }
    }

    static void Check(bool condition, string name, ref int checks)
    {
        if (!condition) throw new InvalidOperationException("Material converter validation failed: " + name);
        checks++;
    }
}
