using UnityEditor;

namespace Stanislaw.FigmaUnityImporter.Editor
{
    internal static class FigmaBridgeSettings
    {
        public const int Port = 38457;
        public const string DefaultAssetFolder = "Assets/FigmaImports/Shared";
        private const string AssetFolderKey = "FigmaUnityImporter.AssetFolder";

        public static string AssetFolder
        {
            get => EditorPrefs.GetString(AssetFolderKey, DefaultAssetFolder);
            set => EditorPrefs.SetString(AssetFolderKey, value);
        }
    }
}
