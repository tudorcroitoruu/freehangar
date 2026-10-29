#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameManager))]
public sealed class GameManagerTestEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hangar encounter testing", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("In Play mode, teleport to a hangar with temporary access to its key and the broom. Opening a hangar normally still saves progress.", MessageType.Info);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Test Hangar 3 — Rats")) ((GameManager)target).TestTeleportToHangar(3);
            if (GUILayout.Button("Test Hangar 4 — Crows")) ((GameManager)target).TestTeleportToHangar(4);
        }
    }
}
#endif
