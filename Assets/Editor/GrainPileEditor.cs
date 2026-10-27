using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrainPile)), CanEditMultipleObjects]
public sealed class GrainPileEditor : Editor
{
    private GrainType previewGrain;
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Scale X/Z to widen the pile and Y to raise it. Kernels keep their world size. Wall Bank rises toward local +Z (blue arrow); place that edge against a wall and rotate around Y. Mound centers and radii use -1..1 footprint coordinates.", MessageType.Info);
        if (Application.isPlaying) EditorGUILayout.HelpBox("Edit shape and scale outside Play Mode. Shape changes regenerate the pile; gameplay excavation is preserved during play.", MessageType.Info);
        serializedObject.Update();
        bool spatial = serializedObject.FindProperty("useSpatialLayout").boolValue;
        if (spatial) DrawPropertiesExcluding(serializedObject, "m_Script", "grainLayers", "previewHarvestedKg");
        else DrawPropertiesExcluding(serializedObject, "m_Script", "regions", "arrangement", "regionWaviness", "grainLayoutSeed", "sectionCenter", "sectionRotation", "previewClearedPercent");
        serializedObject.ApplyModifiedProperties();
        if (!Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grain preview & recipe presets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(spatial ? "Regions are physical shares of starting grain mass, arranged across the pile. Each region has its own top-to-bottom percentage layers and mixtures. Digging and settling move these crops. Preview Cleared Percent removes the front regions in Edit Mode only. Bagging milestones never repaint spatial stock." : "Legacy whole-pile recipe: kg budgets change all surface grain. Enable Use Spatial Layout for real grain areas and local depth layers.", MessageType.Info);
            previewGrain = (GrainType)EditorGUILayout.EnumPopup("Single grain", previewGrain);
            if (GUILayout.Button("100% selected grain")) SetRegions(new[] { new GrainRegion(100, previewGrain) });
            if (GUILayout.Button("Five separate 20% grain regions")) SetRegions(new[] { new GrainRegion(20, GrainType.Corn), new GrainRegion(20, GrainType.Rice), new GrainRegion(20, GrainType.Soybeans), new GrainRegion(20, GrainType.Wheat), new GrainRegion(20, GrainType.Barley) });
            if (GUILayout.Button("Curved sections meeting in the middle")) SetRegions(GrainSpatialSetup.Preset(3), GrainSpatialField.Arrangement.CurvedSections);
            if (GUILayout.Button("Irregular chunks with straight and curved borders")) SetRegions(GrainSpatialSetup.Preset(3), GrainSpatialField.Arrangement.ChunksAndCurves);
            if (GUILayout.Button("Repeating 10% wheat / 5% rice / 10% wheat …")) SetRegions(GrainSpatialSetup.Preset(4));
            if (GUILayout.Button("Late hangar patches — only 5% corn")) SetRegions(GrainSpatialSetup.Preset(5));
            if (GUILayout.Button("Three-crop mixed region")) SetRegions(new[] { new GrainRegion(100, GrainType.Corn, GrainType.Rice, GrainType.Soybeans) });
            if (GUILayout.Button("Reset layer preview"))
            {
                serializedObject.FindProperty("previewHarvestedKg").floatValue = 0;
                serializedObject.FindProperty("previewClearedPercent").floatValue = 0;
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }
        }
        if (targets.Length == 1) EditorGUILayout.LabelField("Detected wall faces", ((GrainPile)target).DetectedWallCount.ToString());
        if (targets.Length == 1 && ((GrainPile)target).HasSpatialLayout)
        {
            var inventory = (GrainPile)target;
            EditorGUILayout.LabelField("Actual remaining stock", EditorStyles.boldLabel);
            for (int type = 0; type < GrainCargo.TypeCount; type++)
            {
                double kg = inventory.GrainKilograms((GrainType)type);
                if (kg > .001) EditorGUILayout.LabelField(GrainNames.Display((GrainType)type),
                    (100 * kg / System.Math.Max(.001, inventory.Kilograms)).ToString("0.##") + "%  ·  " + kg.ToString("N1") + " kg");
            }
        }
        var detail = ((GrainPile)target).GetComponent<CornSurfaceKernels>();
        if (detail != null && detail.ExceedsKernelBudget)
            EditorGUILayout.HelpBox("This pile exceeds its advisory kernel budget. Coverage is preserved; profile rendering cost before increasing its size further.", MessageType.Warning);
        foreach (Object item in targets)
        {
            var pile = (GrainPile)item;
            Vector3 s = pile.WorldScale;
            if (s.x <= 0 || s.y <= 0 || s.z <= 0 || Vector3.Dot(pile.transform.up, Vector3.up) < .9999f)
                EditorGUILayout.HelpBox("Use positive scale and keep the pile upright. Only Y rotation is supported; this is loose grain banked against a wall, not a vertical coating.", MessageType.Warning);
        }
    }

    private void SetRegions(GrainRegion[] regions, GrainSpatialField.Arrangement arrangement = GrainSpatialField.Arrangement.FrontToBack)
    {
        foreach (var item in targets)
        {
            Undo.RecordObject(item, "Change grain recipe");
            ((GrainPile)item).ConfigureRegions(regions, arrangement);
            EditorUtility.SetDirty(item);
        }
        serializedObject.Update();
        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        var pile = (GrainPile)target;
        if (Application.isPlaying) return;
        using var data = new SerializedObject(pile);
        data.Update();
        var mounds = data.FindProperty("mounds");
        if (pile.Shape != GrainPile.PileShape.Cone)
        for (int i = 0; i < mounds.arraySize; i++)
        {
            var mound = mounds.GetArrayElementAtIndex(i);
            var center = mound.FindPropertyRelative("center");
            Vector2 p = center.vector2Value;
            Vector3 local = new Vector3(p.x * pile.Size * .5f, 0, p.y * pile.Size * .5f);
            local.y = pile.SampleLocalHeight(local.x, local.z);
            Vector3 world = pile.transform.TransformPoint(local);
            Handles.Label(world + Vector3.up * .12f, "Mound " + (i + 1));
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(world, Quaternion.Euler(0, pile.transform.eulerAngles.y, 0));
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 changed = pile.transform.InverseTransformPoint(moved);
                center.vector2Value = new Vector2(Mathf.Clamp(changed.x / (pile.Size * .5f), -1, 1), Mathf.Clamp(changed.z / (pile.Size * .5f), -1, 1));
                if (Mathf.Abs(changed.y - local.y) > .001f)
                {
                    var height = mound.FindPropertyRelative("height");
                    height.floatValue = Mathf.Max(.05f, height.floatValue + (changed.y - local.y) / Mathf.Max(.1f, data.FindProperty("initialHeight").floatValue));
                }
                data.ApplyModifiedProperties();
            }
        }
        if (pile.Shape == GrainPile.PileShape.WallBank)
        {
            Handles.color = Color.cyan;
            Vector3 a = pile.transform.TransformPoint(new Vector3(-pile.Size * .5f, 0, pile.Size * .5f));
            Vector3 b = pile.transform.TransformPoint(new Vector3(pile.Size * .5f, 0, pile.Size * .5f));
            Handles.DrawAAPolyLine(4, a, b);
            Handles.Label((a + b) * .5f, "Wall edge (+Z)");
        }
    }
}
