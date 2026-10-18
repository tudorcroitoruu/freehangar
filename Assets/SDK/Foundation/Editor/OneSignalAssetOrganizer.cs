using System.IO;
using UnityEditor;

[InitializeOnLoad]
internal static class OneSignalAssetOrganizer
{
    private const string GeneratedRoot = "Assets/OneSignal";
    private const string VendorRoot = "Assets/SDK/Vendor/OneSignal";

    static OneSignalAssetOrganizer()
    {
        EditorApplication.delayCall += Organize;
    }

    private static void Organize()
    {
        if (!Directory.Exists(GeneratedRoot)) return;
        Directory.CreateDirectory(VendorRoot);
        MoveDirectoryContents(GeneratedRoot, VendorRoot);
        AssetDatabase.DeleteAsset(GeneratedRoot);
        AssetDatabase.Refresh();
    }

    private static void MoveDirectoryContents(string source, string destination)
    {
        foreach (string directory in Directory.GetDirectories(source))
        {
            string target = Path.Combine(destination, Path.GetFileName(directory));
            Directory.CreateDirectory(target);
            MoveDirectoryContents(directory, target);
        }

        foreach (string file in Directory.GetFiles(source))
        {
            if (file.EndsWith(".meta")) continue;
            string target = Path.Combine(destination, Path.GetFileName(file));
            if (File.Exists(target)) File.Delete(target);
            File.Move(file, target);
        }
    }
}
