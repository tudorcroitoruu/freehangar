using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(BicycleController))]
public sealed class BicycleControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (((BicycleController)target).motorDriven)
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "crank", "leftPedal", "rightPedal", "maximumSpeed", "acceleration", "wheelRadius", "wheelbase", "brakingDeceleration", "boostSpeedMultiplier", "motorDriven", "pedalingPitchAmplitude", "pedalingPitchSmoothTime");
            serializedObject.ApplyModifiedProperties();
        }
        else DrawDefaultInspector();
        EditorGUILayout.Space();
        if (!EditorApplication.isPlaying && GUILayout.Button("Adjust Riding Position", GUILayout.Height(32)))
            BicycleRidingPositionWindow.Open((BicycleController)target);
        EditorGUILayout.HelpBox("Edit the seated camera with a live Scene view preview in Edit Mode.", MessageType.Info);
    }
}

public sealed class BicycleRidingPositionWindow : EditorWindow
{
    [SerializeField] BicycleController bicycle;
    SceneView previewView;
    Vector3 previousPivot;
    Quaternion previousRotation;
    float previousSize;
    bool previousOrtho;

    public static void Open(BicycleController bike)
    {
        var engine = bike.GetComponent<MotorollerController>();
        var window = GetWindow<BicycleRidingPositionWindow>(engine != null ? engine.VehicleName + " driving position" : "Bicycle riding position");
        window.RestorePreview();
        window.bicycle = bike;
        window.minSize = new Vector2(390, 360);
        window.Show();
    }
    void OnEnable() => SceneView.duringSceneGui += DrawSeatHandle;
    void OnDisable() { SceneView.duringSceneGui -= DrawSeatHandle; RestorePreview(); }
    void OnGUI()
    {
        if (EditorApplication.isPlaying)
        {
            RestorePreview();
            EditorGUILayout.HelpBox("Stop Play Mode to edit the riding position.", MessageType.Info);
            return;
        }
        bicycle = (BicycleController)EditorGUILayout.ObjectField("Bicycle", bicycle, typeof(BicycleController), true);
        if (bicycle == null || bicycle.riderView == null)
        {
            EditorGUILayout.HelpBox("Select a bicycle with a Rider View anchor assigned.", MessageType.Info);
            return;
        }
        EditorGUILayout.HelpBox("Adjust the seated eye position. The offset is measured from the bike's Rider View anchor. Use the Scene handle to move the anchor itself.", MessageType.None);
        EditorGUI.BeginChangeCheck();
        Vector3 offset = EditorGUILayout.Vector3Field("Camera offset (m)", bicycle.RidingCameraOffset);
        float pitch = EditorGUILayout.Slider("Camera pitch", bicycle.RidingCameraPitch, -35, 35);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(bicycle, "Adjust bicycle riding camera");
            bicycle.SetRidingCamera(offset, pitch);
            Changed(bicycle);
            if (previewView != null) Preview();
        }
        var anchor = bicycle.riderView;
        EditorGUI.BeginChangeCheck();
        Vector3 position = EditorGUILayout.Vector3Field("Seat view anchor (m)", anchor.localPosition);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(anchor, "Move bicycle seat view");
            anchor.localPosition = position;
            Changed(anchor);
            if (previewView != null) Preview();
        }
        EditorGUILayout.Space();
        if (GUILayout.Button("Preview from seat", GUILayout.Height(30))) Preview();
        if (GUILayout.Button("Return to previous Scene view")) RestorePreview();
        if (GUILayout.Button("Select seat anchor / move handle")) Selection.activeTransform = anchor;
        if (GUILayout.Button("Reset camera offset"))
        {
            Undo.RecordObject(bicycle, "Reset bicycle view");
            bicycle.SetRidingCamera(Vector3.zero, 0); Changed(bicycle);
            if (previewView != null) Preview();
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            if (GUILayout.Button("Save scene", GUILayout.Height(28)))
            {
                if (bicycle.gameObject.scene.IsValid()) EditorSceneManager.SaveScene(bicycle.gameObject.scene);
                else AssetDatabase.SaveAssetIfDirty(bicycle);
            }
    }
    static void Changed(Object target)
    {
        EditorUtility.SetDirty(target);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        var component = target as Component;
        if (!EditorApplication.isPlaying && component != null && component.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        SceneView.RepaintAll();
    }
    public void Preview()
    {
        if (bicycle == null || bicycle.riderView == null) return;
        if (previewView == null)
        {
            previewView = SceneView.lastActiveSceneView;
            if (previewView == null) previewView = GetWindow<SceneView>();
            previousPivot = previewView.pivot; previousRotation = previewView.rotation;
            previousSize = previewView.size; previousOrtho = previewView.orthographic;
        }
        Transform anchor = bicycle.riderView;
        Vector3 eye = anchor.TransformPoint(bicycle.RidingCameraOffset);
        Quaternion rotation = anchor.rotation * Quaternion.Euler(bicycle.RidingCameraPitch, 0, 0);
        previewView.orthographic = false;
        previewView.size = 3;
        previewView.LookAtDirect(eye + rotation * Vector3.forward * previewView.cameraDistance, rotation, 3);
        previewView.Repaint();
    }
    public void RestorePreview()
    {
        if (previewView != null)
            { previewView.orthographic = previousOrtho; previewView.LookAtDirect(previousPivot, previousRotation, previousSize); }
        previewView = null;
    }
    void DrawSeatHandle(SceneView sceneView)
    {
        if (bicycle == null || bicycle.riderView == null || EditorApplication.isPlaying) return;
        Transform anchor = bicycle.riderView;
        Handles.Label(anchor.position + Vector3.up * .12f, "Bicycle seated camera");
        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(anchor.position, bicycle.transform.rotation);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(anchor, "Move bicycle seat view"); anchor.position = position; Changed(anchor);
        Repaint();
    }
}

