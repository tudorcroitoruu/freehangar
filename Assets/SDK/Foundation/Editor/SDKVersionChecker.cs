#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

internal static class SDKVersionChecker
{
    private const string Green = "#00C853";
    private const string Yellow = "#FFD600";
    private const string Red = "#FF5252";
    private const string LevelPlayVersionsUrl = "https://s3.amazonaws.com/ssa.public/Unity/Package/V2/LevelPlayVersions.json";
    private static Task<string> levelPlayVersionsRequest;

    private sealed class SDKCheck
    {
        public string Name;
        public Func<string> LocalVersion;
        public Func<Task<string>> RemoteVersion;
        public string Notes;
    }

    [MenuItem("SDK/Check SDK Versions")]
    public static async void CheckVersionsMenu() => await CheckVersionsAndLogAsync();

    internal static async Task CheckVersionsAndLogAsync()
    {
        Debug.Log("<b>[SDK Version Checker]</b> Checking installed SDK and mediation adapter versions...");
        levelPlayVersionsRequest = null;
        var checks = CoreChecks();
        checks.AddRange(LevelPlayAdapterChecks());
        await Task.WhenAll(checks.ConvertAll(RunCheck));
        Debug.Log("<b>[SDK Version Checker]</b> Done. Version warnings do not block the build.");
    }

    private static List<SDKCheck> CoreChecks() => new()
    {
        new SDKCheck
        {
            Name = "Facebook Unity SDK",
            LocalVersion = () => ReadIosPodVersion("Assets/SDK/Vendor/FacebookSDK/Plugins/Editor/Dependencies.xml", "FBSDKCoreKit"),
            RemoteVersion = () => GetGithubLatestVersion("facebook", "facebook-sdk-for-unity"),
            Notes = "local: FBSDKCoreKit iOS pod"
        },
        new SDKCheck
        {
            Name = "Tenjin Unity SDK",
            LocalVersion = GetTenjinLocalVersion,
            RemoteVersion = () => GetGithubLatestVersion("tenjin", "tenjin-unity-sdk"),
            Notes = "local: BaseTenjin wrapper version"
        },
        new SDKCheck
        {
            Name = "GameAnalytics SDK",
            LocalVersion = () => ReadJsonString("Assets/SDK/Vendor/GameAnalytics/package.json", "version"),
            RemoteVersion = () => GetGithubLatestVersion("GameAnalytics", "GA-SDK-UNITY"),
            Notes = "local: GameAnalytics package.json"
        },
        new SDKCheck
        {
            Name = "Unity IAP SDK",
            LocalVersion = () => ReadManifestPackageVersion("com.unity.purchasing"),
            RemoteVersion = () => GetUnityPackageLatestVersion("com.unity.purchasing"),
            Notes = "local: Packages/manifest.json"
        },
        new SDKCheck
        {
            Name = "LevelPlay SDK",
            LocalVersion = GetLevelPlayLocalIosVersion,
            RemoteVersion = () => GetLevelPlayLatestVersion("IronSourceSdk"),
            Notes = "local: IronSourceSDK iOS pod"
        },
        new SDKCheck
        {
            Name = "OneSignal SDK",
            LocalVersion = () => ReadManifestPackageVersion("com.onesignal.unity.ios"),
            RemoteVersion = GetOneSignalLatestVersion,
            Notes = "local: com.onesignal.unity.ios"
        },
        new SDKCheck
        {
            Name = "DOTween",
            LocalVersion = GetDotweenLocalVersion,
            RemoteVersion = GetDotweenLatestVersion,
            Notes = "local: DOTween.Version"
        }
    };

    private static IEnumerable<SDKCheck> LevelPlayAdapterChecks()
    {
        yield return Adapter("Unity Ads", "UnityAds", "ISUnityAdsAdapterDependencies.xml");
        yield return Adapter("Google AdMob", "AdMob", "ISAdMobAdapterDependencies.xml");
        yield return Adapter("AppLovin", "AppLovin", "ISAppLovinAdapterDependencies.xml");
        yield return Adapter("Chartboost", "Chartboost", "ISChartboostAdapterDependencies.xml");
        yield return Adapter("Meta Audience Network", "Meta", "ISFacebookAdapterDependencies.xml");
        yield return Adapter("DT Exchange", "Fyber", "ISFyberAdapterDependencies.xml");
        yield return Adapter("InMobi", "InMobi", "ISInMobiAdapterDependencies.xml");
        yield return Adapter("Liftoff Monetize", "Liftoff", "ISVungleAdapterDependencies.xml");
    }

    private static SDKCheck Adapter(string displayName, string remoteKey, string fileName) => new()
    {
        Name = "LevelPlay / " + displayName,
        LocalVersion = () => ReadXmlTag("Assets/LevelPlay/Editor/" + fileName, "unityversion"),
        RemoteVersion = () => GetLevelPlayLatestVersion(remoteKey),
        Notes = "local: Assets/LevelPlay/Editor/" + fileName
    };

    private static async Task RunCheck(SDKCheck check)
    {
        string local = SafeRead(check.LocalVersion);
        if (string.IsNullOrEmpty(local))
        {
            LogWarning(check.Name, "not found", "unknown", Red, "local version was not found. " + check.Notes);
            return;
        }

        string remote;
        try { remote = NormalizeVersion(await check.RemoteVersion()); }
        catch (Exception exception)
        {
            LogWarning(check.Name, local, "unknown", Yellow, "latest-version request failed; build continues: " + exception.Message);
            return;
        }

        if (string.IsNullOrEmpty(remote))
        {
            LogWarning(check.Name, local, "unknown", Yellow, "latest version was not found in the remote response; build continues.");
            return;
        }

        int comparison = CompareVersions(local, remote);
        if (comparison >= 0)
        {
            Log(check.Name, local, remote, Green, "up to date. " + check.Notes);
            return;
        }

        bool stronglyOutdated = IsStronglyOutdated(local, remote);
        LogWarning(check.Name, local, remote, stronglyOutdated ? Red : Yellow,
            (stronglyOutdated ? "strongly outdated" : "update available") + ". Build continues. " + check.Notes);
    }

    private static string SafeRead(Func<string> read)
    {
        try { return NormalizeVersion(read()); }
        catch (Exception exception)
        {
            Debug.LogWarning("[SDK Version Checker] Failed to read local version: " + exception.Message);
            return null;
        }
    }

    private static void Log(string name, string local, string latest, string color, string message)
        => Debug.Log(Format(name, local, latest, color, message));

    private static void LogWarning(string name, string local, string latest, string color, string message)
        => Debug.LogWarning(Format(name, local, latest, color, message));

    private static string Format(string name, string local, string latest, string color, string message)
        => string.Format("<color={0}><b>[{1}]</b> local: {2}, latest: {3} - {4}</color>",
            color, name, local ?? "unknown", latest ?? "unknown", message);

    private static string GetTenjinLocalVersion()
    {
        string wrapper = ReadRegex("Assets/SDK/Vendor/Tenjin/Scripts/BaseTenjin.cs", @"SdkVersion\s*\{\s*get;\s*\}\s*=\s*""([^""]+)""");
        return !string.IsNullOrEmpty(wrapper)
            ? wrapper
            : ReadRegex("Assets/SDK/Vendor/Tenjin/Plugins/iOS/TenjinSDK.h", @"Version\s+([0-9]+(?:\.[0-9]+)+)");
    }

    private static string GetLevelPlayLocalIosVersion()
    {
        const string path = "Assets/LevelPlay/Editor/IronSourceSDKDependencies.xml";
        string podVersion = ReadIosPodVersion(path, "IronSourceSDK");
        return !string.IsNullOrEmpty(podVersion) ? podVersion : ReadXmlTag(path, "unityversion");
    }

    private static async Task<string> GetLevelPlayLatestVersion(string sectionKey)
    {
        levelPlayVersionsRequest ??= GetText(LevelPlayVersionsUrl);
        string json = await levelPlayVersionsRequest;
        int sectionStart = json.IndexOf("\"" + sectionKey + "\"", StringComparison.Ordinal);
        if (sectionStart < 0) return null;
        string section = json.Substring(sectionStart);
        return Match(section, @"""versions""\s*:\s*\{\s*""([^""]+)""");
    }

    private static async Task<string> GetGithubLatestVersion(string owner, string repository)
    {
        string json = await GetText("https://api.github.com/repos/" + owner + "/" + repository + "/releases/latest");
        return ReadJsonStringFromText(json, "tag_name");
    }

    private static async Task<string> GetOneSignalLatestVersion()
    {
        try
        {
            string githubVersion = await GetGithubLatestVersion("OneSignal", "OneSignal-Unity-SDK");
            if (!string.IsNullOrEmpty(githubVersion)) return githubVersion;
        }
        catch { }
        string json = await GetText("https://registry.npmjs.org/com.onesignal.unity.ios/latest");
        return ReadJsonStringFromText(json, "version");
    }

    private static string GetDotweenLocalVersion()
    {
        Type dotween = Type.GetType("DG.Tweening.DOTween, DOTween");
        return dotween?.GetField("Version")?.GetRawConstantValue()?.ToString();
    }

    private static async Task<string> GetDotweenLatestVersion()
    {
        string page = await GetText("https://dotween.demigiant.com/download.php");
        return Match(page, @"lastVersion\s*=\s*""([^""]+)""");
    }

    private static async Task<string> GetUnityPackageLatestVersion(string packageName)
    {
        string json = await GetText("https://packages.unity.com/" + packageName);
        return Match(json, @"""latest""\s*:\s*""([^""]+)""");
    }

    private static async Task<string> GetText(string url)
    {
        using var request = UnityWebRequest.Get(url);
        request.timeout = 8;
        request.SetRequestHeader("User-Agent", "Unity-SDK-Version-Checker");
        var operation = request.SendWebRequest();
        while (!operation.isDone) await Task.Delay(50);
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            throw new Exception(request.error + " (" + url + ")");
        return request.downloadHandler.text;
    }

    private static string ReadManifestPackageVersion(string packageName)
        => Match(ReadFile("Packages/manifest.json"), @"""" + Regex.Escape(packageName) + @"""\s*:\s*""([^""]+)""");

    private static string ReadIosPodVersion(string path, string podName)
        => Match(ReadFile(path), @"<iosPod\s+name=""" + Regex.Escape(podName) + @"""\s+version=""([^""]+)""");

    private static string ReadXmlTag(string path, string tagName)
        => Match(ReadFile(path), @"<" + Regex.Escape(tagName) + @">\s*([^<]+)\s*</" + Regex.Escape(tagName) + @">");

    private static string ReadJsonString(string path, string key) => ReadJsonStringFromText(ReadFile(path), key);
    private static string ReadJsonStringFromText(string text, string key)
        => Match(text, @"""" + Regex.Escape(key) + @"""\s*:\s*""([^""]+)""");
    private static string ReadRegex(string path, string pattern) => Match(ReadFile(path), pattern);
    private static string ReadFile(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static string Match(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var match = Regex.Match(text, pattern, RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string NormalizeVersion(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        value = value.Trim();
        var match = Regex.Match(value, @"[0-9]+(?:\.[0-9]+)+");
        return match.Success ? match.Value : value.TrimStart('v', 'V');
    }

    private static int CompareVersions(string left, string right)
    {
        int[] first = ParseVersion(left);
        int[] second = ParseVersion(right);
        int length = Math.Max(first.Length, second.Length);
        for (int index = 0; index < length; index++)
        {
            int firstValue = index < first.Length ? first[index] : 0;
            int secondValue = index < second.Length ? second[index] : 0;
            if (firstValue != secondValue) return firstValue.CompareTo(secondValue);
        }
        return 0;
    }

    private static bool IsStronglyOutdated(string local, string latest)
    {
        int[] first = ParseVersion(local);
        int[] second = ParseVersion(latest);
        int localMajor = first.Length > 0 ? first[0] : 0;
        int latestMajor = second.Length > 0 ? second[0] : 0;
        if (latestMajor - localMajor >= 1) return true;
        int localMinor = first.Length > 1 ? first[1] : 0;
        int latestMinor = second.Length > 1 ? second[1] : 0;
        return latestMinor - localMinor >= 2;
    }

    private static int[] ParseVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return Array.Empty<int>();
        var matches = Regex.Matches(version, @"\d+");
        var parts = new int[matches.Count];
        for (int index = 0; index < matches.Count; index++) int.TryParse(matches[index].Value, out parts[index]);
        return parts;
    }
}
#endif
