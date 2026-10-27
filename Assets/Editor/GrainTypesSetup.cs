using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authors assets and scene bindings through Unity, with Undo for scene changes.</summary>
public static class GrainTypesSetup
{
    private const string Folder = "Assets/GrainPrototype/Grains";
    public static Material Material(bool kernels)
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/GrainPrototype", "Grains");
        string path = Folder + (kernels ? "/Grain Kernels.mat" : "/Grain Substrate.mat");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Freehangar/HDRP/Grain Kernels")) { name = kernels ? "Grain Kernels" : "Grain Substrate", enableInstancing = true };
            material.SetFloat("_KernelMode", kernels ? 1 : 0);
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    [MenuItem("Tools/Grain/Install grain types and progression")]
    public static void Install()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Leave Play Mode before authoring grain assets.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var piles = Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(p => p.gameObject.scene == scene).ToArray();
        if (piles.Length == 0) throw new System.InvalidOperationException("Open the gameplay scene first.");
        Material kernels = Material(true), substrate = Material(false);
        foreach (var pile in piles)
        {
            Undo.RecordObject(pile.GetComponent<MeshRenderer>(), "Grain substrate");
            pile.GetComponent<MeshRenderer>().sharedMaterial = substrate;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pile.GetComponent<MeshRenderer>());
        }
        var components = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var component in components)
        {
            if (component.gameObject.scene != scene || !(component is CornSurfaceKernels || component is CornGroundScatter || component is BagCornSurface || component is ShovelGrainPhysics)) continue;
            using var data = new SerializedObject(component);
            data.FindProperty("kernelMaterial").objectReferenceValue = kernels;
            var variants = data.FindProperty("colorVariants");
            if (variants != null) variants.arraySize = 0;
            var surface = data.FindProperty("grainSurface")?.objectReferenceValue as SkinnedMeshRenderer;
            if (surface != null) { Undo.RecordObject(surface, "Bag grain substrate"); surface.sharedMaterial = substrate; PrefabUtility.RecordPrefabInstancePropertyModifications(surface); }
            data.ApplyModifiedProperties();
        }
        var progression = Object.FindObjectsByType<GrainProgression>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(p => p.gameObject.scene == scene);
        if (progression == null)
        {
            var root = new GameObject("Grain progression"); Undo.RegisterCreatedObjectUndo(root, "Grain progression");
            progression = Undo.AddComponent<GrainProgression>(root);
            using var data = new SerializedObject(progression);
            var refs = data.FindProperty("piles"); refs.arraySize = piles.Length;
            for (int i = 0; i < piles.Length; i++) refs.GetArrayElementAtIndex(i).objectReferenceValue = piles[i];
            // Later stage: left hangar rice, right hangar wheat, central pile three-way mix.
            var overrides = data.FindProperty("stages").GetArrayElementAtIndex(3).FindPropertyRelative("overrides");
            overrides.arraySize = 3;
            GrainType[][] recipes = { new[] { GrainType.Rice }, new[] { GrainType.Wheat }, new[] { GrainType.Corn, GrainType.Rice, GrainType.Soybeans } };
            var assigned = new[] { piles.FirstOrDefault(p => p.name.StartsWith("Hangar 3")), piles.FirstOrDefault(p => p.name.StartsWith("Hangar 2")), piles.FirstOrDefault(p => !p.name.StartsWith("Hangar")) };
            for (int i = 0; i < 3; i++)
            {
                var item = overrides.GetArrayElementAtIndex(i); item.FindPropertyRelative("pile").objectReferenceValue = assigned[i];
                var layers = item.FindPropertyRelative("layers"); layers.arraySize = 1;
                layers.GetArrayElementAtIndex(0).FindPropertyRelative("kilograms").floatValue = 52;
                var parts = layers.GetArrayElementAtIndex(0).FindPropertyRelative("mixture").FindPropertyRelative("parts"); parts.arraySize = recipes[i].Length;
                for (int j = 0; j < recipes[i].Length; j++) { parts.GetArrayElementAtIndex(j).FindPropertyRelative("grain").enumValueIndex = (int)recipes[i][j]; parts.GetArrayElementAtIndex(j).FindPropertyRelative("weight").floatValue = 1; }
            }
            data.ApplyModifiedProperties();
        }
        foreach (var visibility in Object.FindObjectsByType<HangarGrainVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (visibility.gameObject.scene != scene) continue;
            using var data = new SerializedObject(visibility);
            data.FindProperty("previewInEditor").boolValue = true; data.ApplyModifiedProperties(); visibility.ApplyState();
        }
        InstallSortingBags(scene);
        InstallBagSupplies(scene);
        InstallHUD(progression, scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        SceneView.RepaintAll();
    }
    private static void InstallSortingBags(UnityEngine.SceneManagement.Scene scene)
    {
        var source = Object.FindObjectsByType<GrainContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.scene == scene);
        if (source == null) return;
        for (int i = 1; i <= 2; i++)
        {
            string name = "Sorting sack " + (i + 1);
            if (GameObject.Find(name) != null) continue;
            var copy = Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = name; copy.transform.position = source.transform.position + Vector3.right * (i * 1.05f);
            Undo.RegisterCreatedObjectUndo(copy, "Add sorting sack");
        }
    }
    private static void InstallHUD(GrainProgression progression, UnityEngine.SceneManagement.Scene scene)
    {
        if (Object.FindObjectsByType<GrainSortingHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(h => h.gameObject.scene == scene)) return;
        var root = new GameObject("Grain cargo HUD", typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(root, "Grain cargo HUD");
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 15;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var panel = new GameObject("Cargo", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(root.transform, false);
        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(0, 0); rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(25, 25); rect.sizeDelta = new Vector2(850, 112);
        panel.GetComponent<Image>().color = new Color(.035f,.045f,.055f,.83f); panel.GetComponent<Image>().raycastTarget = false;
        var label = new GameObject("Contents", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(panel.transform, false);
        var text = label.GetComponent<TextMeshProUGUI>(); text.fontSize = 20; text.color = new Color(.95f,.9f,.73f); text.raycastTarget = false;
        var labelRect = text.rectTransform; labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = new Vector2(18,10); labelRect.offsetMax = new Vector2(-18,-10);
        var hud = Undo.AddComponent<GrainSortingHUD>(root);
        using var data = new SerializedObject(hud);
        data.FindProperty("tool").objectReferenceValue = Object.FindObjectsByType<ShovelTool>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.gameObject.scene == scene);
        data.FindProperty("progression").objectReferenceValue = progression; data.FindProperty("description").objectReferenceValue = text; data.ApplyModifiedProperties();
    }

    private static void InstallBagSupplies(UnityEngine.SceneManagement.Scene scene)
    {
        const string path = Folder + "/Sorting Sack.prefab";
        var bags = Object.FindObjectsByType<GrainContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(b => b.gameObject.scene == scene).ToArray();
        if (bags.Length == 0) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var source = bags.FirstOrDefault(b => b.Kilograms == 0 && b.IsOpen);
            if (source == null) return;
            prefab = PrefabUtility.SaveAsPrefabAsset(source.gameObject, path);
        }
        foreach (var bag in bags)
        {
            string name = bag.name + " supply";
            if (GameObject.Find(name) != null) continue;
            var station = new GameObject(name); station.transform.SetPositionAndRotation(bag.transform.position, bag.transform.rotation);
            Undo.RegisterCreatedObjectUndo(station, "Add replacement sack supply");
            var supply = Undo.AddComponent<GrainBagSupply>(station);
            using var data = new SerializedObject(supply);
            data.FindProperty("emptyBagPrefab").objectReferenceValue = prefab.GetComponent<GrainContainer>();
            data.FindProperty("currentBag").objectReferenceValue = bag;
            data.ApplyModifiedProperties();
        }
    }
}
