using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GameAnalyticsSDK;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal sealed class SDKBuildValidator : IPreprocessBuildWithReport, IActiveBuildTargetChanged
{
    private const string ConfigPath = "Assets/sdk_config.json";
    private const string PlacementsPath = "Assets/SDK/Game/AdPlacements.cs";
    private const string SettingsPath = "Assets/SDK/Resources/SDKSettings.asset";
    private const string FacebookSettingsPath = "Assets/SDK/Vendor/FacebookSDK/SDK/Resources/FacebookSettings.asset";
    private const string AdMobSettingsPath = "ProjectSettings/Packages/com.unity.services.levelplay/AdMobConfigurations.json";
    private static bool buildRequestInProgress;
    public int callbackOrder => -10000;

    public void OnPreprocessBuild(BuildReport report)
    {
        ForceLz4HC(report.summary.platform);
        ValidateBuildOrThrow(report.summary.platform);
    }

    public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
    {
        ForceLz4HC(newTarget);
    }

    internal static void ValidateBuildOrThrow(BuildTarget target)
    {
        var config = LoadAndValidate();
        SyncSettings(config);
        var errors = new List<string>();
        ValidateTarget(config, target, errors);
        ValidateSDKFiles(errors);
        ValidateLevelPlayNetworks(config, errors);
        ValidateAdPlacements(errors);
        ValidateMandatoryCalls(errors);
        ValidateReceiptTangle(config, target, errors);
        if (errors.Count > 0) throw new BuildFailedException(FormatIssues("SDK BUILD REQUIREMENTS NOT MET", errors));
        SyncVendorSettings(config);
    }

    [MenuItem("SDK/Validate Configuration")]
    private static void ValidateMenu()
    {
        try
        {
            var config = LoadAndValidate();
            SyncSettings(config);
            var errors = new List<string>();
            ValidateTarget(config, EditorUserBuildSettings.activeBuildTarget, errors);
            ValidateSDKFiles(errors);
            ValidateLevelPlayNetworks(config, errors);
            ValidateAdPlacements(errors);
            ValidateMandatoryCalls(errors);
            ValidateReceiptTangle(config, EditorUserBuildSettings.activeBuildTarget, errors);
            if (errors.Count > 0) throw new Exception(FormatIssues("SDK BUILD REQUIREMENTS NOT MET", errors));
            SyncVendorSettings(config);
            EditorUtility.DisplayDialog("SDK validation", "Configuration and SDK files are valid.", "OK");
        }
        catch (Exception exception)
        {
            EditorUtility.DisplayDialog("SDK validation failed", exception.Message, "OK");
        }
    }

    [MenuItem("SDK/Open sdk_config.json")]
    private static void OpenConfig() => AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath));

    [InitializeOnLoadMethod]
    private static void ConfigureEditorHooks()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(BuildWithImmediateValidation);
        ForceLz4HC(EditorUserBuildSettings.activeBuildTarget);
        EditorApplication.delayCall += () =>
        {
            ForceLz4HC(EditorUserBuildSettings.activeBuildTarget);
            try
            {
                var config = LoadAndValidate();
                SyncSettings(config);
                SyncVendorSettings(config);
            }
            catch { /* Build validation and the runtime overlay report actionable errors. */ }
        };
    }

    private static async void BuildWithImmediateValidation(BuildPlayerOptions options)
    {
        if (buildRequestInProgress)
        {
            Debug.LogWarning("[SDK] A build request is already being validated.");
            return;
        }
        buildRequestInProgress = true;
        ForceLz4HC(options.target);
        try
        {
            ValidateBuildOrThrow(options.target);
        }
        catch (Exception exception)
        {
            Debug.LogError(exception.Message);
            EditorUtility.DisplayDialog(
                "SDK BUILD BLOCKED",
                exception.Message + "\n\nBUILD STATUS\nThe build was not started. Fix every item above and press Build again.",
                "OK");
            buildRequestInProgress = false;
            return;
        }

        try
        {
            await SDKVersionChecker.CheckVersionsAndLogAsync();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[SDK Version Checker] Unexpected failure; build continues: " + exception);
        }

        options.options &= ~BuildOptions.CompressWithLz4;
        options.options |= BuildOptions.CompressWithLz4HC;
        Debug.Log("[SDK Build] Compression method forced to LZ4HC.");

        try { BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options); }
        finally { buildRequestInProgress = false; }
    }

    private static void ForceLz4HC(BuildTarget target)
    {
        BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(target);
        if (targetGroup == BuildTargetGroup.Unknown) return;

        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic;
        Type settingsType = typeof(EditorUserBuildSettings);
        Type compressionType = settingsType.Assembly.GetType("UnityEditor.Compression");
        System.Reflection.MethodInfo getCompression = settingsType.GetMethod("GetCompressionType", flags);
        System.Reflection.MethodInfo setCompression = settingsType.GetMethod("SetCompressionType", flags);
        if (compressionType == null || getCompression == null || setCompression == null)
        {
            Debug.LogWarning("[SDK Build] Unity Editor compression setting API is unavailable. LZ4HC will still be forced when the build starts.");
            return;
        }

        object lz4HC = Enum.Parse(compressionType, "Lz4HC");
        object current = getCompression.Invoke(null, new object[] { targetGroup });
        if (Equals(current, lz4HC)) return;

        setCompression.Invoke(null, new[] { (object)targetGroup, lz4HC });
        Debug.Log("[SDK Build] Saved compression method changed to LZ4HC for " + targetGroup + ".");
    }

    private static SDKConfig LoadAndValidate()
    {
        if (!File.Exists(ConfigPath))
            throw new BuildFailedException("SDK CONFIGURATION IS MISSING\n\nFILE TO CREATE\n" + ConfigPath);
        SDKConfig config;
        try { config = JsonUtility.FromJson<SDKConfig>(File.ReadAllText(ConfigPath)); }
        catch (Exception exception)
        {
            throw new BuildFailedException(
                "SDK CONFIGURATION CONTAINS INVALID JSON\n\nFILE TO FIX\n" + ConfigPath +
                "\n\nPARSER ERROR\n" + exception.Message);
        }
        var errors = SDKConfigLoader.Validate(config);
        if (errors.Count > 0) throw new BuildFailedException(FormatIssues("SDK CONFIGURATION IS INVALID", errors, ConfigPath));
        return config;
    }

    private static string FormatIssues(string title, IReadOnlyList<string> issues, string file = null)
    {
        var lines = new List<string> { title };
        if (!string.IsNullOrEmpty(file))
        {
            lines.Add(string.Empty);
            lines.Add("FILE TO FIX");
            lines.Add(file);
        }
        lines.Add(string.Empty);
        lines.Add("PROBLEMS");
        for (int index = 0; index < issues.Count; index++)
            lines.Add((index + 1) + ". " + issues[index]);
        return string.Join("\n", lines);
    }

    private static void ValidateTarget(SDKConfig config, BuildTarget target, List<string> errors)
    {
        if (target != BuildTarget.iOS) errors.Add("This SDK template supports iOS builds only.");
    }

    private static void ValidateSDKFiles(List<string> errors)
    {
        RequireDirectory("Assets/SDK/Vendor/FacebookSDK", errors);
        RequireDirectory("Assets/SDK/Vendor/Tenjin", errors);
        RequireDirectory("Assets/SDK/Vendor/GameAnalytics", errors);
        RequireDirectory("Assets/SDK/Vendor/LevelPlay", errors);
        RequireDirectory("Assets/SDK/Vendor/OneSignal", errors);
        RequireDirectory("Assets/SDK/Vendor/ExternalDependencyManager", errors);
        RequireDirectory("Assets/SDK/Vendor/DOTween", errors);

        RequireFile("Assets/SDK/Vendor/Tenjin/Scripts/TenjinIronSourceIntegration.cs", errors);
        RequireFile("Assets/SDK/Vendor/Tenjin/Scripts/TenjinAdNetworkRegistrar.cs", errors);
        RequireFile("Assets/SDK/Vendor/DOTween/DOTween.dll", errors);
        RequireFile("Assets/SDK/Resources/DOTweenSettings.asset", errors);
        RequireFile("Assets/SDK/Foundation/Runtime/Plugins/iOS/SDKHaptics.mm", errors);
        ValidateSkAdNetworkFile(errors);
        if (!File.Exists("Packages/manifest.json") || !File.ReadAllText("Packages/manifest.json").Contains("com.unity.purchasing")) errors.Add("Unity IAP package is missing.");
        if (!File.ReadAllText("Packages/manifest.json").Contains("com.unity.services.levelplay")) errors.Add("LevelPlay package is missing.");
        if (!File.ReadAllText("Packages/manifest.json").Contains("com.onesignal.unity.ios")) errors.Add("OneSignal iOS package is missing.");
    }

    private static void ValidateMandatoryCalls(List<string> errors)
    {
        string[] requiredMethods =
        {
            "StartLevel",
            "CompleteLevel",
            "FailLevel",
            "RequestRateUs",
            "OpenTerms",
            "OpenPrivacyPolicy"
        };
        string source = string.Join("\n", Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .Where(path => !path.StartsWith("Assets/SDK/Foundation/", StringComparison.Ordinal))
            .Where(path => !path.StartsWith("Assets/SDK/Vendor/", StringComparison.Ordinal))
            .Where(path => !path.Contains("/Editor/"))
            .Where(path => !string.Equals(path, "Assets/Examples.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText));
        source = Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", string.Empty, RegexOptions.Singleline);
        foreach (string method in requiredMethods)
            if (!Regex.IsMatch(source, @"\bSDKManager\s*\.\s*" + Regex.Escape(method) + @"\s*\("))
                errors.Add("Mandatory SDK call is not used outside Examples.cs: SDKManager." + method + "(...).");
    }

    private static void ValidateAdPlacements(List<string> errors)
    {
        if (!File.Exists(PlacementsPath))
        {
            errors.Add("Ad placement definitions are missing. Create " + PlacementsPath + ".");
            return;
        }

        string source = File.ReadAllText(PlacementsPath);
        source = Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", string.Empty, RegexOptions.Singleline);
        ValidatePlacementEnum(source, "RewardPlacement", false, errors);
        ValidatePlacementEnum(source, "InterstitialPlacement", true, errors);
        ValidatePlacementEnum(source, "BannerPlacement", false, errors);
    }

    private static void ValidatePlacementEnum(string source, string enumName, bool requiresValue, List<string> errors)
    {
        Match declaration = Regex.Match(
            source,
            @"\bpublic\s+enum\s+" + Regex.Escape(enumName) + @"\s*\{(?<body>[^}]*)\}",
            RegexOptions.Singleline);
        if (!declaration.Success)
        {
            errors.Add("Missing public enum " + enumName + " in " + PlacementsPath + ".");
            return;
        }

        string[] values = declaration.Groups["body"].Value
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();
        if (requiresValue && values.Length == 0)
        {
            errors.Add(enumName + " must contain at least one LevelPlay placement ID.");
            return;
        }

        foreach (string value in values)
        {
            if (!Regex.IsMatch(value, @"^[a-z][a-z0-9_]{0,63}$"))
                errors.Add(enumName + "." + value + " is invalid. Placement IDs must be lowercase, start with a letter, use only a-z, 0-9 or underscore, and contain at most 64 characters.");
        }
    }

    private static void ValidateLevelPlayNetworks(SDKConfig config, List<string> errors)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["UnityAds"] = "ISUnityAdsAdapterDependencies.xml",
            ["AdMob"] = "ISAdMobAdapterDependencies.xml",
            ["AppLovin"] = "ISAppLovinAdapterDependencies.xml",
            ["Chartboost"] = "ISChartboostAdapterDependencies.xml",
            ["Meta"] = "ISFacebookAdapterDependencies.xml",
            ["DTExchange"] = "ISFyberAdapterDependencies.xml",
            ["InMobi"] = "ISInMobiAdapterDependencies.xml",
            ["LiftoffMonetize"] = "ISVungleAdapterDependencies.xml"
        };

        foreach (string network in config.levelPlay.networks ?? Array.Empty<string>())
        {
            if (!files.TryGetValue(network, out string fileName)) continue;
            string installedPath = "Assets/LevelPlay/Editor/" + fileName;
            if (!File.Exists(installedPath))
            {
                errors.Add("LevelPlay network is not installed: " + network + " (missing " + installedPath + ").");
            }
        }

        var configuredFiles = new HashSet<string>((config.levelPlay.networks ?? Array.Empty<string>())
            .Where(files.ContainsKey)
            .Select(network => files[network]), StringComparer.Ordinal);
        if (!Directory.Exists("Assets/LevelPlay/Editor")) return;
        foreach (string path in Directory.GetFiles("Assets/LevelPlay/Editor", "IS*AdapterDependencies.xml"))
        {
            string fileName = Path.GetFileName(path);
            if (!configuredFiles.Contains(fileName)) errors.Add("Unexpected LevelPlay adapter installed but absent from sdk_config.json: " + fileName + ".");
        }
    }

    private static void ValidateReceiptTangle(SDKConfig config, BuildTarget target, List<string> errors)
    {
        string className = target == BuildTarget.Android ? "GooglePlayTangle" : target == BuildTarget.iOS ? "AppleTangle" : null;
        if (className == null) return;
        bool found = Directory.GetFiles("Assets", className + ".cs", SearchOption.AllDirectories).Length > 0;
        if (!found) errors.Add(className + ".cs is missing. Generate receipt validation obfuscation data from Unity IAP before building.");
    }

    private static void ValidateSkAdNetworkFile(List<string> errors)
    {
        const string path = "Assets/SDK/skad.txt";
        if (!File.Exists(path))
        {
            errors.Add("Missing " + path + ".");
            return;
        }

        string content = File.ReadAllText(path);
        var ids = Regex.Matches(content, @"<string>([a-z0-9]+\.skadnetwork)</string>")
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToArray();
        if (ids.Length == 0) errors.Add(path + " does not contain any valid SKAdNetwork identifiers.");
        if (ids.Length != ids.Distinct(StringComparer.Ordinal).Count()) errors.Add(path + " contains duplicate SKAdNetwork identifiers.");
    }

    private static void SyncVendorSettings(SDKConfig config)
    {
        SyncFacebook(config);
        SyncGameAnalytics(config);
        SyncAdMob(config);
        AssetDatabase.SaveAssets();
    }

    private static void SyncSettings(SDKConfig config)
    {
        IAPProductEnumGenerator.Generate();
        AdPlacementEnumGenerator.Generate();

        var settings = AssetDatabase.LoadAssetAtPath<SDKSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<SDKSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        settings.Config = config;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static void SyncFacebook(SDKConfig config)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(FacebookSettingsPath);
        if (asset == null) return;
        var serialized = new SerializedObject(asset);
        SetSingleString(serialized.FindProperty("appIds"), config.facebook.appId);
        SetSingleString(serialized.FindProperty("clientTokens"), config.facebook.clientToken);
        SetSingleString(serialized.FindProperty("appLabels"), "sdk_config");
        serialized.FindProperty("autoLogAppEventsEnabled").boolValue = true;
        serialized.FindProperty("advertiserIDCollectionEnabled").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void SyncGameAnalytics(SDKConfig config)
    {
        var settings = GameAnalytics.SettingsGA;
        if (settings == null) return;
        settings.Platforms.Clear();
        settings.Build.Clear();
        settings.SelectedPlatformOrganization.Clear();
        settings.SelectedPlatformStudio.Clear();
        settings.SelectedPlatformGame.Clear();
        settings.SelectedPlatformGameID.Clear();
        settings.SelectedOrganization.Clear();
        settings.SelectedStudio.Clear();
        settings.SelectedGame.Clear();
        Add(RuntimePlatform.IPhonePlayer, config.gameAnalytics.ios);
        settings.UsePlayerSettingsBuildNumber = true;
        settings.SubmitErrors = true;
        EditorUtility.SetDirty(settings);

        void Add(RuntimePlatform platform, AnalyticsPlatformConfig value)
        {
            int index = settings.Platforms.Count;
            settings.AddPlatform(platform);
            settings.UpdateGameKey(index, value.gameKey);
            settings.UpdateSecretKey(index, value.secretKey);
        }
    }

    private static void SyncAdMob(SDKConfig config)
    {
        string directory = Path.GetDirectoryName(AdMobSettingsPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        string json = "{\n" +
                      "  \"AndroidAdMobAppId\": \"\",\n" +
                      "  \"IOSAdMobAppId\": \"" + config.levelPlay.ios.adMobAppId + "\"\n" +
                      "}\n";
        if (!File.Exists(AdMobSettingsPath) || File.ReadAllText(AdMobSettingsPath) != json)
            File.WriteAllText(AdMobSettingsPath, json);
    }

    private static void SetSingleString(SerializedProperty property, string value)
    {
        if (property == null) return;
        property.arraySize = 1;
        property.GetArrayElementAtIndex(0).stringValue = value;
    }

    private static void RequireDirectory(string path, List<string> errors)
    {
        if (!Directory.Exists(path) || Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length == 0)
            errors.Add("Missing SDK directory: " + path);
    }

    private static void RequireFile(string path, List<string> errors)
    {
        if (!File.Exists(path)) errors.Add("Required SDK integration file is missing: " + path + ".");
    }
}
