using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

// The GDK sandbox switcher is used only by editor windows. Its transitive
// ServiceProcess/Windows.Forms dependencies must not enter an IL2CPP player.
public sealed class GdkEditorPluginBuildFilter : IPreprocessBuildWithReport
{
    private const string PluginPath =
        "Packages/com.unity.microsoft.gdk.tools/Tools/SandboxSwitcher/XblPCSandbox.dll";

    public int callbackOrder => -10000;

    [InitializeOnLoadMethod]
    private static void Initialize() => EditorApplication.delayCall += ExcludeEditorUtility;

    public void OnPreprocessBuild(BuildReport report) => ExcludeEditorUtility();

    private static void ExcludeEditorUtility()
    {
        if (AssetImporter.GetAtPath(PluginPath) is PluginImporter importer)
            importer.SetIncludeInBuildDelegate(_ => false);
    }
}
