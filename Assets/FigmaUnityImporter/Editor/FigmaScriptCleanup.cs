using System.Linq;
using Stanislaw.FigmaUnityImporter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Stanislaw.FigmaUnityImporter.Editor
{
    internal static class FigmaScriptCleanup
    {
        private const string MenuPath = "Tools/Remove Figma Scripts";

        [MenuItem(MenuPath, false, 2100)]
        private static void RemoveFromActiveSceneUi()
        {
            RemoveAllFromActiveSceneUi(true);
        }

        internal static int RemoveAllFromActiveSceneUi(bool logWhenEmpty = false)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return 0;

            var metadata = FindSceneUiMetadata(scene);
            if (metadata.Length == 0)
            {
                if (logWhenEmpty)
                    Debug.Log("[Figma Importer] Active scene has no Figma metadata on UI objects.");
                return 0;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Figma Scripts");
            foreach (var component in metadata)
            {
                if (component != null)
                    Undo.DestroyObjectImmediate(component);
            }
            Undo.CollapseUndoOperations(undoGroup);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Figma Importer] Removed FigmaNodeMetadata from {metadata.Length} UI object(s) " +
                      $"in scene '{scene.name}'. Use Undo to restore them.");
            return metadata.Length;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanRemoveFromActiveSceneUi()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return false;
            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() && scene.isLoaded && FindSceneUiMetadata(scene).Length > 0;
        }

        private static FigmaNodeMetadata[] FindSceneUiMetadata(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FigmaNodeMetadata>(true))
                .Where(component => component != null && component.transform is RectTransform)
                .Distinct()
                .ToArray();
        }
    }
}
