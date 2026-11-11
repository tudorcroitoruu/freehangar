using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// Reads serialized values as well as shader properties so missing URP shaders can be recovered.
[InitializeOnLoad]
internal static class HdrpMaterialMigration
{
    const string Output = "Assets/HDRPConverted";
    const string ReportPath = "Docs/AI/HdrpMaterialMigration.tsv";
    static Queue<string> pending;
    static readonly StringBuilder report = new StringBuilder();
    static string backup;
    static int converted, preserved, failed;
    static readonly Type Combiner = typeof(HDShaderUtils).Assembly.GetType("UnityEditor.Rendering.TextureCombiner");

    static HdrpMaterialMigration() { EditorApplication.delayCall += CheckRequest; }
    static void CheckRequest()
    {
        if (!File.Exists("Temp/HdrpMigration.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete("Temp/HdrpMigration.request");
        ConvertProject();
    }

    [MenuItem("Tools/Rendering/Convert Project Materials to HDRP")]
    static void ConvertProject()
    {
        if (pending != null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (Shader.Find("HDRP/Lit") == null || Shader.Find("Freehangar/HDRP/Particles Unlit") == null || ShaderUtil.ShaderHasError(Shader.Find("Freehangar/HDRP/Particles Unlit")) || Combiner == null) throw new InvalidOperationException("HDRP 17 material tools are required.");
        var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/HDRPDefaultResources/HDRenderPipelineAsset.asset");
        if (pipeline == null) throw new InvalidOperationException("Project HDRP asset was not found.");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = quality.FindProperty("m_QualitySettings");
        for (int i = 0; i < levels.arraySize; i++) levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipeline;
        quality.ApplyModifiedPropertiesWithoutUndo();
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory("Docs/AI");
        backup = Directory.Exists("Library/HdrpMaterialBackup") ? Directory.GetDirectories("Library/HdrpMaterialBackup").OrderByDescending(p => p).FirstOrDefault() : null;
        if (backup == null) backup = "Library/HdrpMaterialBackup/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        report.Clear(); report.AppendLine("Asset\tSource shader\tDestination shader\tStatus / limitations");
        converted = preserved = failed = 0;
        pending = new Queue<string>(AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p));
        EditorApplication.update += Step;
    }

    static void Step()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        for (int i = 0; i < 4 && pending.Count > 0; i++)
        {
            var path = pending.Dequeue();
            try
            {
                foreach (var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()) Process(material, path);
            }
            catch (Exception e) { failed++; report.AppendLine(path + "\t\t\tFAILED: " + e.GetBaseException().Message.Replace('\n', ' ')); Debug.LogException(e); }
        }
        File.WriteAllText("Temp/HdrpMigrationProgress.txt", $"Converted={converted}, preserved={preserved}, failed={failed}, remaining={pending.Count}");
        if (pending.Count != 0) return;
        EditorApplication.update -= Step;
        pending = null;
        AssetDatabase.SaveAssets();
        File.WriteAllText(ReportPath, report.ToString());
        File.WriteAllText("Temp/HdrpMigrationComplete.txt", $"Converted={converted}, preserved={preserved}, failed={failed}\nBackup={backup}");
        Debug.Log($"HDRP material migration complete: {converted} converted, {preserved} retained, {failed} failed. Report: {ReportPath}");
    }

    sealed class Snapshot
    {
        public readonly Material source;
        public readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        public readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
        public readonly Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        public readonly Dictionary<string, Vector2> scales = new Dictionary<string, Vector2>();
        public readonly Dictionary<string, Vector2> offsets = new Dictionary<string, Vector2>();
        public Snapshot(Material m)
        {
            source = m;
            var so = new SerializedObject(m);
            Read(so, "m_Floats", p => floats[p.FindPropertyRelative("first").stringValue] = p.FindPropertyRelative("second").floatValue);
            Read(so, "m_Ints", p => floats[p.FindPropertyRelative("first").stringValue] = p.FindPropertyRelative("second").intValue);
            Read(so, "m_Colors", p => colors[p.FindPropertyRelative("first").stringValue] = p.FindPropertyRelative("second").colorValue);
            Read(so, "m_TexEnvs", p => {
                string n = p.FindPropertyRelative("first").stringValue;
                textures[n] = p.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                scales[n] = p.FindPropertyRelative("second.m_Scale").vector2Value;
                offsets[n] = p.FindPropertyRelative("second.m_Offset").vector2Value;
            });
        }
        static void Read(SerializedObject so, string name, Action<SerializedProperty> read)
        {
            var p = so.FindProperty("m_SavedProperties." + name);
            if (p == null) return;
            for (int i = 0; i < p.arraySize; i++) read(p.GetArrayElementAtIndex(i));
        }
        public float F(float fallback, params string[] names)
        {
            foreach (string n in names) if (source.HasProperty(n)) return source.GetFloat(n);
            foreach (string n in names) if (floats.TryGetValue(n, out float v)) return v;
            return fallback;
        }
        public Color C(Color fallback, params string[] names)
        {
            foreach (string n in names) if (source.HasProperty(n)) return source.GetColor(n);
            foreach (string n in names) if (colors.TryGetValue(n, out Color v)) return v;
            return fallback;
        }
        public string TextureName(params string[] names)
        {
            foreach (string n in names) if (source.HasProperty(n) && textures.TryGetValue(n, out var t) && t != null) return n;
            foreach (string n in names) if (textures.TryGetValue(n, out var t) && t != null) return n;
            return null;
        }
        public Texture T(params string[] names) { string n = TextureName(names); return n == null ? null : textures[n]; }
        public void Map(Material dst, string target, params string[] names)
        {
            if (!dst.HasProperty(target)) return;
            string n = TextureName(names);
            if (n == null) return;
            dst.SetTexture(target, textures[n]); dst.SetTextureScale(target, scales[n]); dst.SetTextureOffset(target, offsets[n]);
        }
    }

    static void Process(Material m, string path)
    {
        string shader = m.shader ? m.shader.name : "MISSING";
        if (shader == "HDRP/Lit" && new Snapshot(m).T("_Icon") != null) shader = "Archanor VFX/Epic Toon FX/ETFX_PowerboxUnlit";
        if (shader.StartsWith("HDRP/") || shader.StartsWith("Freehangar/HDRP/") || (!shader.Contains("PowerboxUnlit") && m.shader && m.GetTag("RenderPipeline", false) == "HDRenderPipeline"))
        { preserved++; report.AppendLine(path + "\t" + shader + "\t" + shader + "\tAlready HDRP"); return; }
        if (shader.Contains("Skybox") || shader.StartsWith("UI/") || shader.StartsWith("TextMeshPro/") || shader.StartsWith("Custom/Outline"))
        { preserved++; report.AppendLine(path + "\t" + shader + "\t" + shader + "\tSpecial-purpose shader retained; sky materials are source assets (HDRP uses sky volumes), UI/outline require their own rendering passes"); return; }
        if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
        { preserved++; report.AppendLine(path + "\t" + shader + "\t" + shader + "\tEmbedded material; model importer owns this asset"); return; }
        string backupPath = backup + "/" + path;
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
        if (!File.Exists(backupPath)) File.Copy(path, backupPath);
        if (File.Exists(path + ".meta")) File.Copy(path + ".meta", backupPath + ".meta", true);
        var s = new Snapshot(m);
        bool particle = shader.Contains("Particles/") || (shader == "Hidden/InternalErrorShader" && (s.floats.ContainsKey("_SoftParticlesEnabled") || path.Contains("/Fx/")));
        var dst = new Material(m);
        try
        {
            dst.shader = Shader.Find(shader.Contains("PowerboxUnlit") ? "Freehangar/HDRP/Powerbox Unlit" : particle ? "Freehangar/HDRP/Particles Unlit" : "HDRP/Lit");
            if (dst.shader == null) throw new InvalidOperationException("Destination shader missing.");
            dst.shaderKeywords = Array.Empty<string>();
            string notes = shader.Contains("PowerboxUnlit") ? "Ported icon/background compositing, tint, contrast, brightness, scale, offset and vertex color to HDRP." : particle ? ConvertParticle(s, dst, shader) : ConvertLit(s, dst, shader, path);
            if (dst.shader.name == "HDRP/Lit") HDShaderUtils.ResetMaterialKeywords(dst);
            m.shader = dst.shader;
            m.CopyPropertiesFromMaterial(dst);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            converted++;
            report.AppendLine(path + "\t" + shader + "\t" + dst.shader.name + "\t" + notes);
        }
        finally { UnityEngine.Object.DestroyImmediate(dst); }
    }

    static string ConvertParticle(Snapshot s, Material dst, string shader)
    {
        bool legacy = shader.StartsWith("Legacy");
        s.Map(dst, "_MainTex", "_BaseMap", "_MainTex");
        dst.SetColor("_Color", legacy ? s.C(Color.gray, "_TintColor", "_Color") * 2f : s.C(Color.white, "_BaseColor", "_Color"));
        s.Map(dst, "_EmissionMap", "_EmissionMap");
        dst.SetColor("_EmissionColor", s.C(Color.black, "_EmissionColor"));
        string[] copy = { "_EmissionEnabled", "_Cutoff", "_FlipbookMode", "_ColorMode", "_SoftParticlesEnabled", "_SoftParticlesNearFadeDistance", "_SoftParticlesFarFadeDistance", "_CameraFadingEnabled", "_CameraNearFadeDistance", "_CameraFarFadeDistance", "_Cull", "_ZWrite", "_BlendOp" };
        foreach (string n in copy) if (s.floats.ContainsKey(n)) dst.SetFloat(n, s.F(dst.GetFloat(n), n));
        float mode = s.F(2f, "_Mode");
        bool additive = shader.Contains("Additive") || mode == 4 || (s.floats.ContainsKey("_Surface") && s.F(0, "_Blend") == 2);
        dst.SetFloat("_SrcBlend", s.F(5f, "_SrcBlend"));
        dst.SetFloat("_DstBlend", s.F(additive ? 1f : 10f, "_DstBlend"));
        dst.SetFloat("_Premultiply", mode == 3 || (s.floats.ContainsKey("_Surface") && s.F(0, "_Blend") == 1) ? 1 : 0);
        dst.SetFloat("_Modulate", mode == 6 ? 1 : 0);
        dst.SetFloat("_AlphaClip", s.F(mode == 1 ? 1 : 0, "_AlphaClip"));
        dst.renderQueue = s.source.renderQueue >= 0 ? s.source.renderQueue : 3000;
        dst.SetOverrideTag("RenderType", "Transparent");
        return "Mapped texture/tint, vertex colors, blending, flipbook, emission, soft particles and camera fade. Lit particle lighting/distortion and HSV color modes have no equivalent in this unlit shader.";
    }

    static string ConvertLit(Snapshot s, Material dst, string shader, string path)
    {
        bool bk = shader.StartsWith("BK/") || shader.StartsWith("Custom/");
        bool foliage = shader.Contains("Leaves") || shader.Contains("Impostor") || shader.Contains("Grass");
        bool water = shader.Contains("Water");
        bool specular = shader.Contains("Specular") || s.F(1, "_WorkflowMode") == 0;
        s.Map(dst, "_BaseColorMap", "_BaseMap", "_MainTex", "_Diffuse", "_Albedo", "_Background");
        Color color = s.C(Color.white, "_BaseColor", "_Color", "_MainColor", "_Color01", "_ShallowColor", "_DepthColor", "_ScatteringColor", "_BackgroundTint");
        if (foliage) color.a = 1; // BK uses MainColor.rgb only; its serialized alpha is often zero.
        dst.SetColor("_BaseColor", color);
        s.Map(dst, "_NormalMap", "_BumpMap", "_Normal", "_NormalMap");
        dst.SetFloat("_NormalScale", s.F(1, "_NormalPower", "_BumpScale"));
        float metallic = s.F(0, "_Metallic", "_MetallicPower");
        float smoothness = s.F(0.5f, "_Smoothness", "_Glossiness", "_SmoothnessPower");
        dst.SetFloat("_Metallic", metallic);
        dst.SetFloat("_Smoothness", smoothness);
        if (specular)
        {
            dst.SetFloat("_MaterialID", 4);
            s.Map(dst, "_SpecularColorMap", "_SpecGlossMap");
            dst.SetColor("_SpecularColor", s.T("_SpecGlossMap") != null ? Color.white : s.C(Color.gray, "_SpecColor"));
        }
        else dst.SetFloat("_MaterialID", 1);
        Texture metal = s.T("_MetallicGlossMap", "_MetallicROcclusionGSmoothnessA");
        Texture ao = s.T("_OcclusionMap");
        if (bk && ao == null) ao = metal;
        Texture smooth = s.F(0, "_SmoothnessTextureChannel") == 1 ? s.T("_BaseMap", "_MainTex") : specular ? s.T("_SpecGlossMap") : metal;
        Texture detailMask = s.T("_DetailMask");
        if (metal != null || ao != null || smooth != null || detailMask != null)
        {
            dst.SetTexture("_MaskMap", Pack(path, "Mask", metal ?? Solid(new Color(metallic, 0, 0, 1)), 0, ao ?? Texture2D.whiteTexture, 1, detailMask ?? Texture2D.whiteTexture, 3, smooth ?? Solid(new Color(0, 0, 0, smoothness)), 3));
            dst.SetFloat("_MetallicRemapMin", 0); dst.SetFloat("_MetallicRemapMax", bk ? metallic : 1);
            dst.SetFloat("_SmoothnessRemapMin", 0); dst.SetFloat("_SmoothnessRemapMax", smooth != null ? (bk ? smoothness : s.F(smoothness, "_Smoothness", "_GlossMapScale")) : 1);
            dst.SetFloat("_AORemapMin", 1 - s.F(1, "_OcclusionStrength", "_OcclusionPower")); dst.SetFloat("_AORemapMax", 1);
        }
        s.Map(dst, "_HeightMap", "_ParallaxMap");
        if (s.T("_ParallaxMap") != null)
        {
            dst.SetFloat("_DisplacementMode", 2);
            dst.SetFloat("_HeightPoMAmplitude", s.F(0.02f, "_Parallax") * 100f);
        }
        s.Map(dst, "_EmissiveColorMap", "_EmissionMap");
        Color emission = s.C(Color.black, "_EmissionColor");
        if (bk || (shader == "Standard" && !s.source.IsKeywordEnabled("_EMISSION"))) emission = Color.black;
        if (shader.Contains("Self-Illumin")) { s.Map(dst, "_EmissiveColorMap", "_MainTex"); emission = color; }
        dst.SetColor("_EmissiveColor", emission);
        dst.SetFloat("_UseEmissiveIntensity", 0);
        Texture detailAlbedo = s.T("_DetailAlbedoMap");
        Texture detailNormal = s.T("_DetailNormalMap");
        if (!bk && (detailAlbedo != null || detailNormal != null))
        {
            dst.SetTexture("_DetailMap", Pack(path, "Detail", detailAlbedo ?? Solid(Color.gray), 4, detailNormal ?? Solid(Color.gray), 1, Solid(Color.gray), 0, detailNormal ?? Solid(Color.gray), 0));
            string n = s.TextureName("_DetailAlbedoMap", "_DetailNormalMap");
            dst.SetTextureScale("_DetailMap", s.scales[n]); dst.SetTextureOffset("_DetailMap", s.offsets[n]);
            dst.SetFloat("_DetailNormalScale", s.F(1, "_DetailNormalMapScale"));
            dst.SetFloat("_DetailAlbedoScale", s.F(1, "_DetailAlbedoMapScale"));
            dst.SetFloat("_UVDetail", s.F(0, "_UVSec")); dst.SetFloat("_LinkDetailsWithBase", 0);
        }
        float mode = s.F(0, "_Mode");
        bool transparent = water || shader.Contains("Transparent") || s.F(0, "_Surface") == 1 || mode >= 2 || s.source.GetTag("RenderType", false) == "Transparent";
        bool cutout = foliage || s.F(0, "_AlphaClip") > 0 || mode == 1 || s.source.IsKeywordEnabled("_ALPHATEST_ON");
        dst.SetFloat("_SurfaceType", transparent ? 1 : 0);
        dst.SetFloat("_BlendMode", s.F(0, "_Blend") == 2 ? 1 : 0);
        dst.SetFloat("_AlphaCutoffEnable", cutout ? 1 : 0);
        dst.SetFloat("_AlphaCutoff", s.F(0.5f, "_AlphaClipThreshold", "_Cutoff", "_AlphaCutoff"));
        dst.SetFloat("_DoubleSidedEnable", foliage || s.F(2, "_Cull") == 0 ? 1 : 0);
        dst.SetFloat("_DoubleSidedNormalMode", 1);
        dst.SetFloat("_CullMode", s.F(2, "_Cull"));
        dst.SetFloat("_CullModeForward", s.F(2, "_Cull"));
        dst.renderQueue = transparent ? 3000 : cutout ? 2450 : 2000;
        dst.SetOverrideTag("RenderType", transparent ? "Transparent" : cutout ? "TransparentCutout" : "Opaque");
        if (water) dst.SetFloat("_TransparentZWrite", 0);
        return bk || shader.Contains("Powerbox") ? "Mapped compatible surface properties. Custom wind, vertex gradients/layers, billboard logic, procedural water/clouds and Powerbox compositing need shader-specific ports; original properties retained in material and backed up." : "Mapped compatible albedo, normal, mask, specular, height, detail, emission, UV, alpha and culling properties.";
    }

    static Texture Solid(Color color) => (Texture)Combiner.GetMethod("TextureFromColor", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { color });
    static Texture Pack(string materialPath, string suffix, Texture r, int rc, Texture g, int gc, Texture b, int bc, Texture a, int ac)
    {
        string path = Output + "/" + AssetDatabase.AssetPathToGUID(materialPath) + "_" + suffix + ".png";
        var inputs = new[] { r, g, b, a };
        var channels = new[] { rc, gc, bc, ac };
        var loaded = new Dictionary<Texture, Texture2D>();
        var packer = new Material(Shader.Find("Hidden/SRP_Core/TextureCombiner"));
        RenderTexture target = null;
        Texture2D result = null;
        var previous = RenderTexture.active;
        try
        {
            int width = 4, height = 4;
            for (int i = 0; i < inputs.Length; i++)
            {
                Texture input = inputs[i];
                string sourcePath = AssetDatabase.GetAssetPath(input);
                var importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
                if (importer != null && (sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || sourcePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!loaded.TryGetValue(input, out var raw))
                    {
                        raw = new Texture2D(2, 2, TextureFormat.RGBA32, false, !importer.sRGBTexture);
                        if (!ImageConversion.LoadImage(raw, File.ReadAllBytes(sourcePath), false)) throw new InvalidOperationException("Could not read " + sourcePath);
                        raw.wrapMode = input.wrapMode;
                        raw.filterMode = FilterMode.Bilinear;
                        loaded.Add(input, raw);
                    }
                    input = raw;
                }
                else if (importer != null && importer.textureType == TextureImporterType.NormalMap)
                {
                    // Unity's converter handles non-PNG normal sources (e.g. TGA).
                    var fallback = Activator.CreateInstance(Combiner, new object[] { r, rc, g, gc, b, bc, a, ac, true });
                    return (Texture)Combiner.GetMethod("Combine").Invoke(fallback, new object[] { path });
                }
                width = Mathf.Max(width, inputs[i].width); height = Mathf.Max(height, inputs[i].height);
                string channel = "RGBA"[i].ToString();
                packer.SetTexture("_" + channel + "Source", input);
                packer.SetFloat("_" + channel + "Channel", channels[i]);
            }
            target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(Texture2D.whiteTexture, target, packer);
            RenderTexture.active = target;
            result = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0); result.Apply();
            File.WriteAllBytes(path, result.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var outputImporter = (TextureImporter)AssetImporter.GetAtPath(path);
            outputImporter.sRGBTexture = false;
            outputImporter.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(width, height));
            outputImporter.textureCompression = TextureImporterCompression.Uncompressed;
            outputImporter.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) RenderTexture.ReleaseTemporary(target);
            if (result != null) UnityEngine.Object.DestroyImmediate(result);
            UnityEngine.Object.DestroyImmediate(packer);
            foreach (var texture in loaded.Values) UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}



