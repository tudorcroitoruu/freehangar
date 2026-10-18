using LogicStories.BackgroundBlurUI;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIElementAnimation))]
[CanEditMultipleObjects]
public sealed class UIElementAnimationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("Preview Animation", GUILayout.Height(28f)))
        {
            foreach (Object selectedTarget in targets)
            {
                ((UIElementAnimation)selectedTarget).PreviewInEditor();
            }
        }

        if (GUILayout.Button("Preview All Active", GUILayout.Height(28f)))
        {
            UIElementAnimation[] activeAnimations =
                FindObjectsByType<UIElementAnimation>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            foreach (UIElementAnimation animation in activeAnimations)
            {
                if (animation.isActiveAndEnabled)
                {
                    animation.PreviewInEditor();
                }
            }
        }

        if (GUILayout.Button("Stop Preview"))
        {
            foreach (Object selectedTarget in targets)
            {
                ((UIElementAnimation)selectedTarget).StopEditorPreview();
            }
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            return;
        }

        foreach (Object selectedTarget in targets)
        {
            if (selectedTarget)
            {
                ((UIElementAnimation)selectedTarget).StopEditorPreview();
            }
        }
    }
}
