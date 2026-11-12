using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Reject misconfigured Steam builds before spending time compiling IL2CPP.
public sealed class SteamBuildValidation : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => -9000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (BuildPipeline.GetBuildTargetGroup(report.summary.platform) != BuildTargetGroup.Standalone)
            return;

#if DISABLESTEAMWORKS
        throw new BuildFailedException("Steam protection requires Steamworks.NET. Remove DISABLESTEAMWORKS for Steam builds.");
#else
        ValidateSettings(Resources.Load<SteamAchievementSettings>("SteamAchievementSettings"));
        ValidateOutput(report.summary.outputPath);
#endif
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (BuildPipeline.GetBuildTargetGroup(report.summary.platform) == BuildTargetGroup.Standalone)
            ValidateOutput(report.summary.outputPath);
    }

    internal static void ValidateSettings(SteamAchievementSettings settings)
    {
        if (settings == null || !settings.HasRealAppId)
            throw new BuildFailedException("Set the real Steam App ID in Assets/Resources/SteamAchievementSettings.asset before building. App IDs 0 and 480 cannot protect this game. Editor playtesting is still available.");
    }

    private static void ValidateOutput(string outputPath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        foreach (string file in Directory.Exists(directory)
                     ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     : System.Array.Empty<string>())
        {
            if (string.Equals(Path.GetFileName(file), "steam_appid.txt", System.StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("Do not ship the development-only steam_appid.txt. Remove it from the build output: " + file);
        }
    }
}
