using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

/// <summary>Explicit, reversible material conversion. Does not change the project's pipeline.</summary>
internal sealed class MaterialPipelineConverter : EditorWindow
{
    internal enum Target { URP, HDRP }
    [SerializeField] Target target;
    [SerializeField] bool selectedOnly;
    [SerializeField] bool includeCustom = true;
    readonly List<string> candidates = new List<string>();
    readonly StringBuilder report = new StringBuilder();
    Vector2 scroll;
    string status = "Scan to preview materials before conversion.";
    string backup;
    Queue<string> pending;
    int undoGroup, converted, failed;

    [MenuItem("Tools/Rendering/Material Pipeline Converter")]
    static void Open() => GetWindow<MaterialPipelineConverter>("Material Converter");

    void OnGUI()
    {
        using (new EditorGUI.DisabledScope(pending != null))
        {
            EditorGUI.BeginChangeCheck();
            target = (Target)EditorGUILayout.EnumPopup("Convert to", target);
            selectedOnly = EditorGUILayout.Toggle("Selected assets / folders only", selectedOnly);
            includeCustom = EditorGUILayout.Toggle("Try custom / built-in shaders", includeCustom);
            if (EditorGUI.EndChangeCheck()) candidates.Clear();
            EditorGUILayout.HelpBox("Converts material assets in place, preserving references. Custom shaders become standard Lit/Unlit approximations; shader code and special effects are not ported. UI, sky, terrain, decals and embedded model materials are skipped and reported. Pipeline settings are unchanged.", MessageType.Info);
            if (GUILayout.Button("Scan / Preview")) Scan();
            using (new EditorGUI.DisabledScope(candidates.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button($"Convert {candidates.Count} materials to {target}")) Begin();
        }
        if (pending != null && GUILayout.Button("Stop after current material")) Finish();
        EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report.ToString());
        EditorGUILayout.EndScrollView();
    }

    void Scan()
    {
        candidates.Clear();
        report.Clear();
        report.AppendLine("Asset\tSource shader\tResult");
        var paths = new HashSet<string>();
        if (selectedOnly)
        {
            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                    foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { path })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                else if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
        }
        else
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        foreach (string path in paths.OrderBy(p => p))
            foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                string reason = SkipReason(mat, path);
                if (reason == null) candidates.Add(path);
                report.AppendLine($"{path}\t{mat.shader?.name}\t{reason ?? "Ready (review appearance after conversion)"}");
            }
        status = $"{candidates.Count} convertible materials. See preview for skipped assets.";
    }

    string SkipReason(Material mat, string path)
    {
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || !AssetDatabase.IsMainAsset(mat)) return "Skipped: package / embedded material; extract from model first";
        if (!AssetDatabase.IsOpenForEdit(mat) || (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) return "Skipped: read-only asset";
        if (mat.isVariant) return "Skipped: material variant; flatten explicitly first";
        string name = mat.shader ? mat.shader.name : "";
        if (name == "" || name == "Hidden/InternalErrorShader") return "Skipped: missing shader; restore source shader first";
        string lower = name.ToLowerInvariant();
        if (lower.StartsWith("ui/") || lower.Contains("textmeshpro") || lower.Contains("skybox") || lower.Contains("terrain") || lower.Contains("decal") || lower.Contains("outline") || lower.StartsWith("hidden/")) return "Skipped: special-purpose shader";
        string tag = mat.GetTag("RenderPipeline", false);
        bool hdrp = tag == "HDRenderPipeline" || name.StartsWith("HDRP/") || name.StartsWith("Freehangar/HDRP/");
        bool urp = tag == "UniversalPipeline" || name.StartsWith("Universal Render Pipeline/");
        if ((target == Target.HDRP && hdrp) || (target == Target.URP && urp)) return "Skipped: already target pipeline";
        bool standard = name == "HDRP/Lit" || name == "HDRP/Unlit" || name == "Universal Render Pipeline/Lit" || name == "Universal Render Pipeline/Unlit";
        return !standard && !includeCustom ? "Skipped: custom / built-in conversion disabled" : null;
    }

    void Begin()
    {
        backup = "MaterialConversionBackups/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        Directory.CreateDirectory(backup);
        pending = new Queue<string>(candidates.Distinct());
        converted = failed = 0;
        Undo.IncrementCurrentGroup();
        undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Convert material pipeline");
        report.AppendLine("--- Conversion results ---");
        EditorApplication.update += Step;
    }

    void Step()
    {
        if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) { Finish(); return; }
        if (EditorApplication.isUpdating) return;
        if (pending == null || pending.Count == 0) { Finish(); return; }
        string path = pending.Dequeue();
        Material dst = null;
        try
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) throw new InvalidOperationException("Material no longer exists");
            string reason = SkipReason(mat, path);
            if (reason != null) { report.AppendLine(path + "\t" + reason); return; }
            // Save only this asset so the on-disk backup includes any pending Inspector changes.
            AssetDatabase.SaveAssetIfDirty(mat);
            string copy = backup + "/" + path;
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            File.Copy(path, copy);
            File.Copy(path + ".meta", copy + ".meta");
            string generated = "Assets/MaterialConversionGenerated/" + Path.GetFileName(backup);
            dst = ConvertMaterial(mat, target, generated, AssetDatabase.AssetPathToGUID(path));
            Undo.RegisterCompleteObjectUndo(mat, "Convert material pipeline");
            mat.shader = dst.shader;
            mat.CopyPropertiesFromMaterial(dst);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat);
            converted++;
            report.AppendLine(path + "\tConverted to " + dst.shader.name + "; advanced/custom effects require visual review");
        }
        catch (Exception e) { failed++; report.AppendLine(path + "\tFAILED: " + e.Message); Debug.LogException(e); }
        finally
        {
            if (dst) DestroyImmediate(dst);
            status = $"Converted {converted}, failed {failed}, remaining {pending?.Count ?? 0}.";
            Repaint();
        }
    }

    void Finish()
    {
        if (pending == null) return;
        EditorApplication.update -= Step;
        int remaining = pending.Count;
        pending = null;
        Undo.CollapseUndoOperations(undoGroup);
        status = $"Converted {converted}, failed {failed}, unprocessed {remaining}. Backup/report: {backup}";
        report.AppendLine(status);
        File.WriteAllText(backup + "/report.tsv", report.ToString());
        candidates.Clear();
        Repaint();
        Debug.Log(status);
    }

    void OnDisable() => Finish();

    internal static Material ConvertMaterial(Material src, Target target, string outputFolder, string id)
    {
        bool hd = target == Target.HDRP;
        // These shaders generate their appearance from custom properties. Plain Lit
        // would silently discard crop weights or the tree atlas and render white.
        string customShader = null;
        if (src.shader.name == "Freehangar/HDRP/Grain Kernels" || src.shader.name == "Freehangar/URP/Grain Kernels")
            customShader = hd ? "Freehangar/HDRP/Grain Kernels" : "Freehangar/URP/Grain Kernels";
        else if (src.shader.name == "FreeHangar/Tree Cutout Toon" || src.shader.name == "Freehangar/URP/Tree Cutout Toon")
            customShader = hd ? "FreeHangar/Tree Cutout Toon" : "Freehangar/URP/Tree Cutout Toon";
        if (customShader != null)
        {
            var custom = Shader.Find(customShader);
            if (!custom) throw new InvalidOperationException("Missing custom pipeline shader: " + customShader);
            var result = new Material(src) { shader = custom };
            result.shaderKeywords = Array.Empty<string>();
            return result;
        }
        bool unlit = src.shader.name.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0;
        string shaderName = (hd ? "HDRP/" : "Universal Render Pipeline/") + (unlit ? "Unlit" : "Lit");
        var shader = Shader.Find(shaderName);
        if (!shader) throw new InvalidOperationException("Missing destination shader: " + shaderName);
        var dst = new Material(shader);
        try
        {
            dst.enableInstancing = src.enableInstancing;
            dst.doubleSidedGI = src.doubleSidedGI;
            dst.globalIlluminationFlags = src.globalIlluminationFlags;
            Map(src, dst, hd ? (unlit ? "_UnlitColorMap" : "_BaseColorMap") : "_BaseMap", "_UnlitColorMap", "_BaseColorMap", "_BaseMap", "_MainTex", "_TextureSample", "_Albedo", "_Diffuse", "_Background");
            dst.SetColor(hd && unlit ? "_UnlitColor" : "_BaseColor", C(src, Color.white, "_UnlitColor", "_BaseColor", "_Color", "_MainColor", "_TintColor", "_BackgroundTint"));
            bool transparent = F(src, 0, "_SurfaceType", "_Surface") == 1 || src.GetTag("RenderType", false) == "Transparent" || F(src, 0, "_Mode") >= 2;
            bool clip = F(src, 0, "_AlphaCutoffEnable", "_AlphaClip") > 0 || src.IsKeywordEnabled("_ALPHATEST_ON") || F(src, 0, "_Mode") == 1;
            dst.SetFloat(hd ? "_SurfaceType" : "_Surface", transparent ? 1 : 0);
            dst.SetFloat(hd ? "_AlphaCutoffEnable" : "_AlphaClip", clip ? 1 : 0);
            dst.SetFloat(hd ? "_AlphaCutoff" : "_Cutoff", F(src, .5f, "_AlphaCutoff", "_Cutoff", "_AlphaClipThreshold"));
            float cull = F(src, 2, "_CullMode", "_Cull");
            bool doubleSided = F(src, 0, "_DoubleSidedEnable") > 0 || cull == 0;
            if (hd) { dst.SetFloat("_DoubleSidedEnable", doubleSided ? 1 : 0); dst.SetFloat("_DoubleSidedNormalMode", 1); }
            else dst.SetFloat("_Cull", doubleSided ? 0 : cull);
            float blend = src.HasProperty("_BlendMode") ? (F(src, 0, "_BlendMode") == 1 ? 2 : F(src, 0, "_BlendMode") == 4 ? 1 : 0) : F(src, 0, "_Blend");
            dst.SetFloat(hd ? "_BlendMode" : "_Blend", hd ? (blend == 2 ? 1 : blend == 1 ? 4 : 0) : blend);
            Map(src, dst, hd ? "_EmissiveColorMap" : "_EmissionMap", "_EmissiveColorMap", "_EmissionMap");
            SetColor(dst, hd ? "_EmissiveColor" : "_EmissionColor", C(src, Color.black, "_EmissiveColor", "_EmissionColor"));
            SetFloat(dst, "_UseEmissiveIntensity", 0);
            if (!unlit)
            {
                Map(src, dst, hd ? "_NormalMap" : "_BumpMap", "_NormalMap", "_BumpMap", "_Normal");
                dst.SetFloat(hd ? "_NormalScale" : "_BumpScale", F(src, 1, "_NormalScale", "_BumpScale", "_NormalPower"));
                dst.SetFloat("_Metallic", F(src, 0, "_Metallic", "_MetallicPower"));
                dst.SetFloat("_Smoothness", F(src, .5f, "_Smoothness", "_Glossiness", "_SmoothnessPower"));
                bool specular = F(src, 1, "_WorkflowMode") == 0 || F(src, 1, "_MaterialID") == 4 || src.shader.name.Contains("Specular");
                dst.SetFloat(hd ? "_MaterialID" : "_WorkflowMode", hd ? (specular ? 4 : 1) : (specular ? 0 : 1));
                SetColor(dst, hd ? "_SpecularColor" : "_SpecColor", C(src, Color.gray, "_SpecularColor", "_SpecColor"));
                Map(src, dst, hd ? "_SpecularColorMap" : "_SpecGlossMap", "_SpecularColorMap", "_SpecGlossMap");
                ConvertMask(src, dst, hd, specular, outputFolder, id);
            }
            dst.renderQueue = transparent ? 3000 : clip ? 2450 : 2000;
            if (hd) HDShaderUtils.ResetMaterialKeywords(dst);
            else BaseShaderGUI.SetMaterialKeywords(dst, unlit ? (Action<Material>)null : LitGUI.SetMaterialKeywords);
            return dst;
        }
        catch { DestroyImmediate(dst); throw; }
    }

    static float F(Material m, float fallback, params string[] names)
    { foreach (string n in names) if (m.HasProperty(n)) return m.GetFloat(n); return fallback; }
    static Color C(Material m, Color fallback, params string[] names)
    { foreach (string n in names) if (m.HasProperty(n)) return m.GetColor(n); return fallback; }
    static string TextureName(Material m, params string[] names)
    { foreach (string n in names) if (m.HasProperty(n) && m.GetTexture(n)) return n; return null; }
    static void SetFloat(Material m, string n, float value) { if (m.HasProperty(n)) m.SetFloat(n, value); }
    static void SetColor(Material m, string n, Color value) { if (m.HasProperty(n)) m.SetColor(n, value); }
    static void Map(Material src, Material dst, string target, params string[] names)
    {
        string n = TextureName(src, names);
        if (n == null || !dst.HasProperty(target)) return;
        dst.SetTexture(target, src.GetTexture(n));
        dst.SetTextureScale(target, src.GetTextureScale(n));
        dst.SetTextureOffset(target, src.GetTextureOffset(n));
    }

    // Native Lit shaders sample mask maps using the base-map UVs. Preserve those UVs and bake channels.
    static void ConvertMask(Material src, Material dst, bool hd, bool specular, string folder, string id)
    {
        string mask = TextureName(src, "_MaskMap");
        string metal = TextureName(src, "_MetallicGlossMap", "_MetallicROcclusionGSmoothnessA");
        string ao = TextureName(src, "_OcclusionMap");
        string spec = TextureName(src, "_SpecGlossMap", "_SpecularColorMap");
        string smooth = F(src, 0, "_SmoothnessTextureChannel") == 1 ? TextureName(src, "_BaseMap", "_MainTex") : specular ? spec : metal;
        if (mask == null && metal == null && ao == null && smooth == null) return;
        var maps = new Dictionary<string, Texture2D>();
        Texture2D packed = null;
        try
        {
            int width = 4, height = 4;
            foreach (string n in new[] { mask, metal, ao, smooth, spec }.Where(n => n != null).Distinct())
            {
                Texture t = src.GetTexture(n);
                width = Mathf.Max(width, t.width); height = Mathf.Max(height, t.height);
                maps[n] = ReadTexture(t);
            }
            width = Mathf.Min(width, 4096); height = Mathf.Min(height, 4096);
            packed = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var pixels = new Color[width * height];
            var specPixels = specular && !hd ? new Color[pixels.Length] : null;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Vector2 uv = new Vector2((x + .5f) / width, (y + .5f) / height);
                    Color sample = Sample(src, maps, mask, uv, Color.white);
                    float m = mask != null ? Mathf.Lerp(F(src, 0, "_MetallicRemapMin"), F(src, 1, "_MetallicRemapMax"), sample.r) : metal != null ? Sample(src, maps, metal, uv, Color.white).r : F(src, 0, "_Metallic");
                    float o = mask != null ? Mathf.Lerp(F(src, 0, "_AORemapMin"), F(src, 1, "_AORemapMax"), sample.g) : Mathf.Lerp(1, Sample(src, maps, ao, uv, Color.white).g, F(src, 1, "_OcclusionStrength"));
                    float s = mask != null ? Mathf.Lerp(F(src, 0, "_SmoothnessRemapMin"), F(src, 1, "_SmoothnessRemapMax"), sample.a) : Sample(src, maps, smooth, uv, Color.white).a * F(src, .5f, "_Smoothness", "_GlossMapScale", "_Glossiness");
                    pixels[y * width + x] = new Color(m, o, 1, s);
                    if (specPixels != null)
                    {
                        Color specColor = Sample(src, maps, spec, uv, Color.white);
                        if (src.HasProperty("_SpecularColor")) specColor *= src.GetColor("_SpecularColor");
                        else if (spec == null) specColor = C(src, Color.gray, "_SpecColor");
                        specColor.a = s;
                        specPixels[y * width + x] = specColor;
                    }
                }
            packed.SetPixels(pixels); packed.Apply();
            Directory.CreateDirectory(folder);
            string path = folder + "/" + id + "-mask.png";
            var texture = SavePacked(packed, path);
            dst.SetTexture(hd ? "_MaskMap" : "_MetallicGlossMap", texture);
            dst.SetFloat("_Smoothness", 1);
            if (!hd)
            {
                dst.SetTexture("_OcclusionMap", texture);
                dst.SetFloat("_OcclusionStrength", 1);
                if (specPixels != null)
                {
                    packed.SetPixels(specPixels); packed.Apply();
                    dst.SetTexture("_SpecGlossMap", SavePacked(packed, folder + "/" + id + "-specular.png"));
                    dst.SetColor("_SpecColor", Color.white);
                }
            }
        }
        finally
        {
            foreach (var texture in maps.Values) DestroyImmediate(texture);
            if (packed) DestroyImmediate(packed);
        }
    }

    static Color Sample(Material src, Dictionary<string, Texture2D> maps, string n, Vector2 uv, Color fallback)
    {
        if (n == null) return fallback;
        return maps[n].GetPixelBilinear(uv.x, uv.y);
    }

    static Texture2D SavePacked(Texture2D packed, string path)
    {
        File.WriteAllBytes(path, packed.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture = false;
        importer.maxTextureSize = Mathf.NextPowerOfTwo(Mathf.Max(packed.width, packed.height));
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Texture2D ReadTexture(Texture source)
    {
        var previous = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Texture2D result = null;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            result.wrapMode = source.wrapMode;
            result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            result.Apply();
            return result;
        }
        catch { if (result) DestroyImmediate(result); throw; }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); }
    }
}
