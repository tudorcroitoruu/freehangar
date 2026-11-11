#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class ItemImageRendererWindow : EditorWindow
{
    [Serializable] private sealed class Item
    {
        public bool enabled = true;
        public string category;
        public GameObject source;
        public Vector3 rotation;
        public bool customRotation;
        public bool customCamera;
        public Vector2 cameraAngles;
    }
    [SerializeField] private List<Item> items = new List<Item>();
    [SerializeField] private int resolution = 1024;
    [SerializeField] private float padding = 1.15f;
    [SerializeField] private Vector2 viewAngles = new Vector2(25f, 135f);
    [SerializeField] private int cameraPresetVersion;
    [SerializeField] private string outputFolder = "Renders/Items";
    [SerializeField] private bool overwrite;
    private Vector2 scroll;
    private string resultMessage;

    private const string OpenedThisSession = "Freehangar.ItemImageRenderer.ManuallyOpened";

    [MenuItem("Tools/Freehangar/Render Item Images")]
    public static void Open()
    {
        // SessionState survives script reloads, but resets when Unity exits.
        SessionState.SetBool(OpenedThisSession, true);
        GetWindow<ItemImageRendererWindow>("Item PNG Renderer");
    }

    private void OnEnable()
    {
        if (!SessionState.GetBool(OpenedThisSession, false))
        {
            // A saved editor layout can recreate this window without using its menu command.
            // Wait until layout restoration finishes before removing the restored tab.
            EditorApplication.delayCall += () =>
            {
                if (this != null && !SessionState.GetBool(OpenedThisSession, false)) Close();
            };
            return;
        }
        if (cameraPresetVersion != 0) return;
        if (Mathf.Approximately(viewAngles.x, 18f) && Mathf.Approximately(viewAngles.y, 145f))
            viewAngles = new Vector2(25f, 135f);
        cameraPresetVersion = 1;
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Collect items from the open scene, review their rotation, then render transparent PNGs. Only visual meshes are copied into an isolated preview scene.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Collect game items")) Collect();
                if (GUILayout.Button("Add selected")) foreach (var go in Selection.gameObjects) Add(go, "Selected");
                if (GUILayout.Button("Clear")) items.Clear();
            }
            resolution = EditorGUILayout.IntPopup("PNG size", resolution, new[] { "256", "512", "1024", "2048" }, new[] { 256, 512, 1024, 2048 });
            padding = EditorGUILayout.Slider("Frame padding", padding, 1.02f, 1.6f);
            viewAngles = EditorGUILayout.Vector2Field("Default camera pitch / yaw", viewAngles);
            outputFolder = EditorGUILayout.TextField("Output folder", outputFolder);
            if (GUILayout.Button("Choose output folder"))
            {
                var chosen = EditorUtility.OpenFolderPanel("Transparent item images", AbsoluteOutput(), "");
                if (!string.IsNullOrEmpty(chosen)) outputFolder = chosen;
            }
            overwrite = EditorGUILayout.Toggle("Overwrite existing PNGs", overwrite);
            EditorGUILayout.LabelField(items.Count + " items", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        entry.enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(20));
                        EditorGUILayout.LabelField(entry.category ?? "Item", GUILayout.Width(75));
                        entry.source = (GameObject)EditorGUILayout.ObjectField(entry.source, typeof(GameObject), true);
                        if (GUILayout.Button("X", GUILayout.Width(24))) { items.RemoveAt(i--); continue; }
                    }
                    bool hasRotationOverride = entry.customRotation || entry.rotation != Vector3.zero;
                    bool customRotation = EditorGUILayout.Toggle("Custom rotation", hasRotationOverride);
                    if (customRotation && !hasRotationOverride) entry.rotation = ItemRotation(entry);
                    if (!customRotation && hasRotationOverride) entry.rotation = Vector3.zero;
                    entry.customRotation = customRotation;
                    if (customRotation) entry.rotation = EditorGUILayout.Vector3Field("Item rotation", entry.rotation);
                    else EditorGUILayout.LabelField("Item rotation", ItemRotation(entry).ToString());
                    bool customCamera = EditorGUILayout.Toggle("Custom camera", entry.customCamera);
                    if (customCamera && !entry.customCamera) entry.cameraAngles = CameraAngles(entry);
                    entry.customCamera = customCamera;
                    if (entry.customCamera) entry.cameraAngles = EditorGUILayout.Vector2Field("Camera pitch / yaw", entry.cameraAngles);
                    else EditorGUILayout.LabelField("Camera pitch / yaw", CameraAngles(entry).ToString());
                }
            }
            EditorGUILayout.EndScrollView();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Render first enabled item")) RenderItems(true);
                if (GUILayout.Button("Render all enabled items")) RenderItems(false);
                if (GUILayout.Button("Open folder")) { Directory.CreateDirectory(AbsoluteOutput()); EditorUtility.RevealInFinder(AbsoluteOutput()); }
            }
        }
        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Leave Play mode to render.", MessageType.Info);
        if (!string.IsNullOrEmpty(resultMessage)) EditorGUILayout.HelpBox(resultMessage, MessageType.None);
    }

    private string AbsoluteOutput() => Path.GetFullPath(string.IsNullOrWhiteSpace(outputFolder) ? "Renders/Items" : outputFolder);
    private static IEnumerable<T> SceneObjects<T>() where T : Component =>
        Resources.FindObjectsOfTypeAll<T>().Where(x => !EditorUtility.IsPersistent(x) && x.gameObject.scene.IsValid());

    private void Add(GameObject source, string category)
    {
        if (source == null || items.Any(x => x.source == source)) return;
        items.Add(new Item { source = source, category = category });
    }

    private void Collect()
    {
        items.Clear();
        foreach (var levels in SceneObjects<HeldGrainToolLevels>())
            if (levels.levels != null) foreach (var level in levels.levels) Add(level, "Tools");
        foreach (var product in SceneObjects<ShopProduct>().Cast<Component>().Concat(SceneObjects<InventoryProduct>()))
        {
            var so = new SerializedObject(product);
            var prop = so.FindProperty("objectPrefab");
            var prefab = prop != null ? prop.objectReferenceValue as GameObject : null;
            if (prefab == null) continue;
            if (prefab.GetComponentInChildren<GrainContainer>(true) != null)
                Add(prefab, "Containers");
            else if (prefab.name.IndexOf("box",StringComparison.OrdinalIgnoreCase) >= 0 ||
                prefab.name.IndexOf("bag",StringComparison.OrdinalIgnoreCase) >= 0 ||
                prefab.name.IndexOf("sack",StringComparison.OrdinalIgnoreCase) >= 0) Add(prefab,"Containers");
            else if (prefab.GetComponentInChildren<BicycleController>(true) != null || prefab.GetComponentInChildren<WheelbarrowCargo>(true) != null)
                Add(prefab, "Vehicles");
        }
        // Include unlisted container prefabs and scene-only vehicle rigs.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Gameplay/Containers" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null) Add(prefab, "Containers");
        }
        foreach (var container in SceneObjects<GrainContainer>())
            if (container.GetComponentInParent<PlayerController>() == null &&
                container.GetComponentInParent<BicycleController>() == null) Add(container.gameObject,"Containers");
        foreach (var vehicle in SceneObjects<BicycleController>()) Add(vehicle.gameObject, "Vehicles");
        foreach (var wheelbarrow in SceneObjects<WheelbarrowCargo>()) Add(wheelbarrow.gameObject, "Vehicles");
        resultMessage = "Collected " + items.Count + " items. Use Add selected for any extra models.";
    }

    private void RenderItems(bool firstOnly)
    {
        var selected = items.Where(x => x.enabled && x.source != null).ToArray();
        if (firstOnly) selected = selected.Take(1).ToArray();
        if (selected.Length == 0) { resultMessage = "Collect or add an item first."; return; }
        int success = 0, failed = 0;
        try
        {
            var folder = AbsoluteOutput();
            Directory.CreateDirectory(folder);
            for (int i = 0; i < selected.Length; i++)
            {
                var entry = selected[i];
                if (EditorUtility.DisplayCancelableProgressBar("Rendering item images", entry.source.name, i / (float)selected.Length)) break;
                try
                {
                    string category = SafeName(entry.category ?? "Items");
                    string directory = Path.Combine(folder, category);
                    Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, SafeName(entry.source.name) + ".png");
                    if (!overwrite)
                    {
                        int suffix = 2;
                        string basePath = path;
                        while (File.Exists(path)) path = Path.Combine(directory, Path.GetFileNameWithoutExtension(basePath) + "_" + suffix++ + ".png");
                    }
                    Render(entry, path);
                    success++;
                }
                catch (Exception ex) { failed++; Debug.LogError("Render failed for " + entry.source.name + ": " + ex); }
            }
            resultMessage = success + " PNGs saved to " + folder + (failed > 0 ? ". " + failed + " failed; see Console." : ".");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static string SafeName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "Item" : name;
    }

    private Vector2 CameraAngles(Item entry)
    {
        if (entry.customCamera) return entry.cameraAngles;
        if (entry.category == "Containers" && entry.source != null)
        {
            string name = entry.source.name;
            if (IsBagOrSack(name)) return new Vector2(25f, 45f);
            if (name.IndexOf("barrel", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Vector2(25f, 120f);
            if (name.IndexOf("crate", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Vector2(25f, 135f);
            return new Vector2(viewAngles.x, viewAngles.y - 90f);
        }
        if (IsShovel(entry)) return new Vector2(35f, viewAngles.y);
        return viewAngles;
    }

    private static bool IsBagOrSack(string name) =>
        name.IndexOf("bag", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("sack", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool IsShovel(Item entry) =>
        entry.category == "Tools" && entry.source != null &&
        entry.source.name.StartsWith("LVL_", StringComparison.OrdinalIgnoreCase) &&
        int.TryParse(entry.source.name.Substring(4), out int level) && level >= 1 && level <= 4;

    private static Vector3 ItemRotation(Item entry)
    {
        if (entry.customRotation || entry.rotation != Vector3.zero) return entry.rotation;
        if (entry.category == "Containers" && entry.source != null && IsBagOrSack(entry.source.name))
            return new Vector3(-90f, 0f, 0f);
        if (IsShovel(entry)) return new Vector3(0f, 0f, -50f);
        return Vector3.zero;
    }

    private void Render(Item entry, string path)
    {
        var preview = new PreviewRenderUtility();
        var temporaryMeshes = new List<Mesh>();
        VolumeProfile profile = null;
        Texture2D black = null, white = null, output = null;
        try
        {
            var visual = new GameObject("Item rendering copy") { hideFlags = HideFlags.HideAndDontSave };
            preview.AddSingleGO(visual);
            visual.transform.rotation = Quaternion.Euler(ItemRotation(entry));
            CopyMeshes(entry.source, visual.transform, temporaryMeshes, entry.category == "Containers");
            var renderers = visual.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("No enabled visual meshes found.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var camera = preview.camera;
            var hd = camera.GetUniversalAdditionalCameraData();
            hd.renderPostProcessing = false; hd.volumeLayerMask = 0;
            camera.orthographic = true;
            camera.aspect = 1;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = Mathf.Max(100f, bounds.size.magnitude * 10f);
            Vector2 angles = CameraAngles(entry);
            Quaternion facing = Quaternion.Euler(angles.x, angles.y, 0);
            camera.transform.SetPositionAndRotation(bounds.center - facing * Vector3.forward * Mathf.Max(5f,bounds.size.magnitude * 3f), facing);
            Vector3 size = bounds.extents;
            float half = .01f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(size,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 p = Quaternion.Inverse(facing) * (corner - bounds.center);
                half = Mathf.Max(half, Mathf.Abs(p.x), Mathf.Abs(p.y));
            }
            camera.orthographicSize = half * padding;
            camera.clearFlags = CameraClearFlags.SolidColor;
            for (int i = 0; i < preview.lights.Length; i++)
            {
                var light = preview.lights[i];
                light.type = LightType.Directional;
                bool container = entry.category == "Containers";
                light.intensity = container ? (i == 0 ? 1.4f : .7f) : (i == 0 ? 1.8f : .9f);
                light.color = container ?
                    (i == 0 ? new Color(1f,.98f,.95f) : new Color(.92f,.96f,1f)) :
                    (i == 0 ? new Color(1,.94f,.84f) : new Color(.75f,.85f,1));
                light.shadows = LightShadows.None;
                light.transform.rotation = container ?
                    facing * Quaternion.Euler(i == 0 ? 25f : 10f, i == 0 ? -35f : 50f, 0f) :
                    Quaternion.Euler(i == 0 ? 40 : 25,i == 0 ? 35 : 210,0);
            }
            var volumeObject = new GameObject("Item studio exposure") { layer = 31, hideFlags = HideFlags.HideAndDontSave };
            preview.AddSingleGO(volumeObject);
            var volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10000;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.hideFlags = HideFlags.HideAndDontSave; volume.sharedProfile = profile;
            // Black/white background matting works even when the URP buffer does not preserve alpha.
            black = Capture(preview, hd, Color.black);
            white = Capture(preview, hd, Color.white);
            var bp = black.GetPixels(); var wp = white.GetPixels();
            output = new Texture2D(resolution,resolution,TextureFormat.RGBA32,false,false);
            var pixels = new Color[bp.Length];
            Color plateBlack = bp[0], plateWhite = wp[0];
            Vector3 plateDifference = new Vector3(plateWhite.r-plateBlack.r,plateWhite.g-plateBlack.g,plateWhite.b-plateBlack.b);
            if (Mathf.Max(plateDifference.x,plateDifference.y,plateDifference.z) < .0001f)
                throw new InvalidOperationException("The preview background did not change. Check URP preview rendering support.");
            int visiblePixels = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                float difference = Mathf.Clamp01(Mathf.Max(
                    (wp[i].r-bp[i].r)/Mathf.Max(.0001f,plateDifference.x),
                    (wp[i].g-bp[i].g)/Mathf.Max(.0001f,plateDifference.y),
                    (wp[i].b-bp[i].b)/Mathf.Max(.0001f,plateDifference.z)));
                float alpha = 1f-difference;
                if (alpha < .004f) pixels[i] = Color.clear;
                else
                {
                    var color = new Color(Mathf.Clamp01((bp[i].r-plateBlack.r*(1-alpha))/alpha),
                        Mathf.Clamp01((bp[i].g-plateBlack.g*(1-alpha))/alpha),
                        Mathf.Clamp01((bp[i].b-plateBlack.b*(1-alpha))/alpha),alpha);
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear) { color = color.gamma; color.a = alpha; }
                    pixels[i] = color;
                    if (alpha > .05f) visiblePixels++;
                }
            }
            if (visiblePixels == 0) throw new InvalidOperationException("The item rendered empty. Check the source model and enabled renderers.");
            output.SetPixels(pixels); output.Apply();
            File.WriteAllBytes(path, output.EncodeToPNG());
        }
        finally
        {
            if (black != null) DestroyImmediate(black);
            if (white != null) DestroyImmediate(white);
            if (output != null) DestroyImmediate(output);
            if (profile != null) { foreach (var component in profile.components) DestroyImmediate(component); DestroyImmediate(profile); }
            preview.Cleanup();
            foreach (var mesh in temporaryMeshes) DestroyImmediate(mesh);
        }
    }

    private Texture2D Capture(PreviewRenderUtility preview, UniversalAdditionalCameraData hd, Color color)
    {
        var previous = RenderTexture.active;
        bool begun = false;
        try
        {
            preview.BeginPreview(new Rect(0,0,resolution,resolution), GUIStyle.none); begun = true;
            preview.camera.backgroundColor = color;
            preview.Render(true);
            var texture = preview.EndPreview(); begun = false;
            var target = texture as RenderTexture;
            if (target == null) throw new InvalidOperationException("Preview renderer returned no render texture.");
            RenderTexture.active = target;
            var image = new Texture2D(resolution,resolution,TextureFormat.RGBAFloat,false,true);
            image.ReadPixels(new Rect(0,0,resolution,resolution),0,0); image.Apply();
            if (target.sRGB)
            {
                var colors = image.GetPixels();
                for (int i=0;i<colors.Length;i++) { float alpha=colors[i].a; colors[i]=colors[i].linear; colors[i].a=alpha; }
                image.SetPixels(colors); image.Apply();
            }
            return image;
        }
        finally { if (begun) preview.EndPreview(); RenderTexture.active = previous; }
    }

    private static void CopyMeshes(GameObject source, Transform parent, List<Mesh> temporary, bool isContainer)
    {
        bool hideContents = isContainer &&
            (source.name.IndexOf("barrel", StringComparison.OrdinalIgnoreCase) >= 0 ||
             source.name.IndexOf("crate", StringComparison.OrdinalIgnoreCase) >= 0);
        var lowerLods = new HashSet<Renderer>();
        var sourceMeshRenderers = source.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        Bounds mainBounds = default;
        if (sourceMeshRenderers.Length > 0)
        {
            mainBounds = sourceMeshRenderers[0].bounds;
            foreach (var renderer in sourceMeshRenderers) mainBounds.Encapsulate(renderer.bounds);
        }
        foreach (var lod in source.GetComponentsInChildren<LODGroup>(true))
        {
            var levels = lod.GetLODs();
            for (int i = 1; i < levels.Length; i++) foreach (var renderer in levels[i].renderers) lowerLods.Add(renderer);
        }
        foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
        {
            if (lowerLods.Contains(renderer) || !renderer.enabled || renderer is ParticleSystemRenderer || renderer.GetComponent<TMPro.TMP_Text>() != null) continue;
            if (hideContents && (renderer.name.IndexOf("Grain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 renderer.name.IndexOf("Net", StringComparison.OrdinalIgnoreCase) >= 0)) continue;
            bool visible = true;
            for (var t = renderer.transform; t != source.transform && t != null; t = t.parent) if (!t.gameObject.activeSelf) { visible = false; break; }
            if (!visible) continue;
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = new Mesh { name = renderer.name + " render bake" }; skinned.BakeMesh(mesh); mesh.RecalculateBounds(); temporary.Add(mesh);
            }
            else if (renderer is MeshRenderer && renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
            if (mesh == null) continue;
            var go = new GameObject(renderer.name) { layer = 31, hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(parent,false);
            Matrix4x4 matrix = Matrix4x4.Scale(source.transform.lossyScale) * source.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            go.transform.localPosition = matrix.GetColumn(3); go.transform.localRotation = matrix.rotation; go.transform.localScale = matrix.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var copy = go.AddComponent<MeshRenderer>(); copy.sharedMaterials = renderer.sharedMaterials;
            // Some animated cables have a stale skin pose. Their baked bounds can dwarf
            // the actual item and cause the camera to frame only that cable.
            if (renderer is SkinnedMeshRenderer && sourceMeshRenderers.Length > 0 &&
                copy.bounds.size.magnitude > mainBounds.size.magnitude * 3f)
            {
                UnityEngine.Object.DestroyImmediate(go);
                temporary.Remove(mesh);
                UnityEngine.Object.DestroyImmediate(mesh);
                continue;
            }
            copy.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            if (isContainer && renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial != null)
            {
                string materialName = renderer.sharedMaterial.name;
                if (materialName == "Sack_Atlas") block.SetColor("_BaseColor", new Color(.42f,.40f,.36f,1f));
                else if (materialName == "LargeBag") block.SetColor("_BaseColor", new Color(.05f,.05f,.05f,1f));
            }
            copy.SetPropertyBlock(block);
        }
    }
}
#endif
