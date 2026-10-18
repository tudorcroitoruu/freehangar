#if UNITY_IOS
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

internal static class SDKIOSPostprocessor
{
    [PostProcessBuild(1000)]
    private static void Process(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;
        SDKConfigLoader.TryLoad(out var config, out var error);
        if (config == null) throw new BuildFailedException("Unable to load SDK config during iOS post-process: " + error);
        UpdatePlist(buildPath, config);
        EmbedTenjin(buildPath);
    }

    private static void UpdatePlist(string buildPath, SDKConfig config)
    {
        string path = Path.Combine(buildPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(path);
        plist.root.SetString("NSUserTrackingUsageDescription", config.app.privacyTrackingDescription);
        plist.root.SetString("GADApplicationIdentifier", config.levelPlay.ios.adMobAppId);
        plist.root.SetString("NSAdvertisingAttributionReportEndpoint", "https://tenjin-skan.com");
        var items = plist.root.CreateArray("SKAdNetworkItems");
        string skad = File.ReadAllText("Assets/SDK/skad.txt");
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (Match match in Regex.Matches(skad, @"<string>([^<]+\.skadnetwork)</string>"))
        {
            string id = match.Groups[1].Value;
            if (!seen.Add(id)) continue;
            items.AddDict().SetString("SKAdNetworkIdentifier", id);
        }
        plist.WriteToFile(path);
    }

    private static void EmbedTenjin(string buildPath)
    {
        const string sourceZip = "Assets/SDK/Vendor/Tenjin/Plugins/iOS/TenjinSDK.xcframework.zip";
        string frameworkRoot = Path.Combine(buildPath, "Frameworks");
        string frameworkPath = Path.Combine(frameworkRoot, "TenjinSDK.xcframework");
        if (Directory.Exists(frameworkPath)) Directory.Delete(frameworkPath, true);
        ZipFile.ExtractToDirectory(sourceZip, frameworkRoot);
        string metadata = Path.Combine(frameworkRoot, "__MACOSX");
        if (Directory.Exists(metadata)) Directory.Delete(metadata, true);

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        string main = project.GetUnityMainTargetGuid();
        string framework = project.GetUnityFrameworkTargetGuid();
        string file = project.AddFile(frameworkPath, "Frameworks/TenjinSDK.xcframework");
        project.AddFileToEmbedFrameworks(main, file);
        project.AddFileToBuildSection(framework, project.GetFrameworksBuildPhaseByTarget(framework), file);
        foreach (string name in new[] { "AdServices.framework", "AdSupport.framework", "AppTrackingTransparency.framework", "StoreKit.framework" })
            project.AddFrameworkToProject(framework, name, true);
        project.AddFrameworkToProject(framework, "CoreHaptics.framework", false);
        project.AddFrameworkToProject(framework, "AudioToolbox.framework", false);
        project.AddBuildProperty(framework, "OTHER_LDFLAGS", "-ObjC");
        project.WriteToFile(projectPath);
    }

}
#endif
