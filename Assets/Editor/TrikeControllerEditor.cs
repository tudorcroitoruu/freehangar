using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrikeController))]
public sealed class TrikeControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Three-wheel drive uses the shared motor settings and steering rig. Four bed anchors accept cargo through the existing E interaction.", MessageType.Info);
        if (!Application.isPlaying && GUILayout.Button("Adjust Driver Position"))
            BicycleRidingPositionWindow.Open(((TrikeController)target).Movement);
    }
}
