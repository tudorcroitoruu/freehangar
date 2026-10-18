using UnityEditor;
using UnityEngine;

namespace Stanislaw.FigmaUnityImporter.Editor
{
    internal sealed class FigmaImporterWindow : EditorWindow
    {
        [MenuItem("Tools/Figma Frame Importer")]
        private static void Open() => GetWindow<FigmaImporterWindow>("Figma Importer");

        private void OnEnable() => FigmaLocalBridge.Changed += Repaint;
        private void OnDisable() => FigmaLocalBridge.Changed -= Repaint;

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Figma → Unity", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(FigmaLocalBridge.LastMessage, FigmaLocalBridge.IsRunning ? MessageType.Info : MessageType.Warning);
            if (FigmaLocalBridge.IsImporting)
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 18f), FigmaLocalBridge.Progress, "Importing one Frame at a time");
            if (FigmaLocalBridge.PendingCount > 0)
                EditorGUILayout.LabelField("Queued Frames", FigmaLocalBridge.PendingCount.ToString());
            EditorGUILayout.LabelField("Port", FigmaBridgeSettings.Port.ToString());

            var folder = EditorGUILayout.TextField("Shared asset folder", FigmaBridgeSettings.AssetFolder);
            if (folder != FigmaBridgeSettings.AssetFolder)
                FigmaBridgeSettings.AssetFolder = folder;

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(FigmaLocalBridge.IsRunning))
                if (GUILayout.Button("Start local bridge")) FigmaLocalBridge.Start();
            using (new EditorGUI.DisabledScope(!FigmaLocalBridge.IsRunning))
                if (GUILayout.Button("Stop local bridge")) FigmaLocalBridge.Stop();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "In Figma Desktop: Plugins → Development → Import plugin from manifest, choose Assets/FigmaUnityImporter/FigmaPlugin/manifest.json, select one or more Frames, then run ‘Figma Frame → Unity’.",
                MessageType.None);
        }
    }
}
