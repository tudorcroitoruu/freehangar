using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrainProgression))]
public sealed class GrainProgressionEditor : Editor
{
    private int preview;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var progression = (GrainProgression)target;
        EditorGUILayout.HelpBox("Thresholds count grain successfully put in containers. Spatial hangars keep their authored crops at every milestone; edit their GrainPile regions instead. Stage recipes affect only legacy non-spatial piles. Keep thresholds in ascending order.", MessageType.Info);
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("Bagged", progression.BaggedKilograms.ToString("0.00") + " kg");
            EditorGUILayout.LabelField("Stage", progression.StageTitle);
            return;
        }
        preview = EditorGUILayout.IntSlider("Preview stage", preview, 0, Mathf.Max(0, progression.StageCount - 1));
        if (GUILayout.Button("Preview stage on scene piles"))
        {
            var piles = serializedObject.FindProperty("piles");
            for (int i = 0; i < piles.arraySize; i++)
            {
                var pile = piles.GetArrayElementAtIndex(i).objectReferenceValue;
                if (pile != null) { Undo.RecordObject(pile, "Preview grain stage"); EditorUtility.SetDirty(pile); }
            }
            progression.ApplyStage(preview);
            SceneView.RepaintAll();
        }
    }
}
