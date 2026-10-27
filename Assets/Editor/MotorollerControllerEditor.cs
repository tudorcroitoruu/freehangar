using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MotorollerController))]
public sealed class MotorollerControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var scooter = (MotorollerController)target;
        EditorGUILayout.HelpBox("W/S drive and reverse · A/D steer · Space brake · E mount / dismount. Engine power uses no stamina. Wheel and steering references live on the shared movement rig below.", MessageType.Info);
        if (!Application.isPlaying && GUILayout.Button("Adjust Riding Position"))
            BicycleRidingPositionWindow.Open(scooter.Movement);
    }
}
