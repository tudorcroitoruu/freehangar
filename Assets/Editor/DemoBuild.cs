using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class DemoBuild
{
    [MenuItem("Tools/Steam/Build Windows Demo (5602740)")]
    public static void BuildDemo()
    {
        string folder = EditorUtility.OpenFolderPanel("Demo build output", "", "");
        if (string.IsNullOrEmpty(folder)) return;
        // Keep company/product unchanged: demo and retail share the existing PlayerPrefs save.
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = System.IO.Path.Combine(folder, "Grain Cleanup Co.exe"),
            target = BuildTarget.StandaloneWindows64,
            extraScriptingDefines = new[] { "DEMO_BUILD" }
        });
        Debug.Log("Demo build: " + report.summary.result);
    }
}
