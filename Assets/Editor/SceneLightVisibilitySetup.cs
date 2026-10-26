using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneLightVisibilitySetup
{
    [MenuItem("Tools/Lighting/Add Scene Light Visibility Controller")]
    public static void AddToScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Exit Play Mode before installing the scene light controller.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        SceneLightVisibilityController controller = null;
        foreach (var existing in Object.FindObjectsByType<SceneLightVisibilityController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (existing.gameObject.scene == scene) { controller = existing; break; }
        if (!controller)
        {
            var go = new GameObject("Scene Light Visibility");
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Add scene light visibility");
            controller = Undo.AddComponent<SceneLightVisibilityController>(go);
        }
        if (!controller.ViewCamera)
        {
            Camera camera = null;
            foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player.gameObject.scene != scene) continue;
                Camera candidate = player.characterCamera ? player.characterCamera : player.GetComponentInChildren<Camera>(true);
                if (!candidate) continue;
                if (!camera) camera = candidate;
                if (player.isActiveAndEnabled && candidate.isActiveAndEnabled) { camera = candidate; break; }
            }
            if (!camera && Camera.main && Camera.main.gameObject.scene == scene) camera = Camera.main;
            if (camera)
            {
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("viewCamera").objectReferenceValue = camera;
                serialized.ApplyModifiedProperties();
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = controller.gameObject;
    }
}

[CustomEditor(typeof(SceneLightVisibilityController))]
public sealed class SceneLightVisibilityControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Play Mode only. Directional, baked and mixed lights are untouched. Disabling this component restores the lights' original settings.", MessageType.Info);
        var controller = (SceneLightVisibilityController)target;
        if (!Application.isPlaying) return;
        EditorGUILayout.LabelField("Managed realtime lights", controller.ManagedLightCount.ToString());
        EditorGUILayout.LabelField("Currently enabled", controller.EnabledLightCount.ToString());
        EditorGUILayout.LabelField("Currently shadowed", controller.ShadowedLightCount.ToString());
        if (GUILayout.Button("Rescan and update now"))
        {
            controller.RefreshLights();
            controller.EvaluateNow();
        }
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
