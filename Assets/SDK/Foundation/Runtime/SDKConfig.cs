using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class SDKConfig
{
    public int schemaVersion = 1;
    public AppConfig app = new();
    public FacebookConfig facebook = new();
    public TenjinConfig tenjin = new();
    public OneSignalConfig oneSignal = new();
    public StoreLinksConfig storeLinks = new();
    public GameAnalyticsConfig gameAnalytics = new();
    public LevelPlayConfig levelPlay = new();
    public IAPConfig iap = new();
}

[Serializable]
public sealed class AppConfig
{
    public string privacyTrackingDescription;
}

[Serializable]
public sealed class FacebookConfig
{
    public string appId;
    public string clientToken;
}

[Serializable]
public sealed class TenjinConfig
{
    public string iosApiKey;
}

[Serializable]
public sealed class OneSignalConfig
{
    public string appId;
}

[Serializable]
public sealed class StoreLinksConfig
{
    public string termsUrl;
    public string privacyPolicyUrl;
}

[Serializable]
public sealed class GameAnalyticsConfig
{
    public AnalyticsPlatformConfig ios = new();
}

[Serializable]
public sealed class AnalyticsPlatformConfig
{
    public string gameKey;
    public string secretKey;
}

[Serializable]
public sealed class LevelPlayConfig
{
    public AdPlatformConfig ios = new();
    public string[] networks = Array.Empty<string>();
    public AdPlacementsConfig placements = new();
}

[Serializable]
public sealed class AdPlacementsConfig
{
    public string[] rewarded = Array.Empty<string>();
    public string[] interstitial = Array.Empty<string>();
    public string[] banner = Array.Empty<string>();
}

[Serializable]
public sealed class AdPlatformConfig
{
    public string appKey;
    public string adMobAppId;
    public string rewardedAdUnitId;
    public string interstitialAdUnitId;
    public string bannerAdUnitId;
}

[Serializable]
public sealed class IAPConfig
{
    public IAPProductConfig[] products = Array.Empty<IAPProductConfig>();
}

[Serializable]
public sealed class IAPProductConfig
{
    public string id;
    public string type = "Consumable";
    public string scope;
    public int usdCents;
}

public static class SDKConfigLoader
{
    public static bool TryLoad(out SDKConfig config, out string error)
    {
        config = null;
#if UNITY_EDITOR
        string path = Path.Combine(Application.dataPath, "sdk_config.json");
        if (!File.Exists(path))
        {
            error = "SDK config was not found. The only source file must be Assets/sdk_config.json.";
            return false;
        }
        string json = File.ReadAllText(path);
#else
        var settings = Resources.Load<SDKSettings>(SDKSettings.ResourceName);
        if (settings == null || settings.Config == null)
        {
            error = "Compiled SDK settings are missing from the build. Reimport Assets/sdk_config.json in Unity.";
            return false;
        }
        config = settings.Config;
        var runtimeErrors = Validate(config);
        error = string.Join("\n", runtimeErrors);
        return runtimeErrors.Count == 0;
#endif

#if UNITY_EDITOR
        try
        {
            config = JsonUtility.FromJson<SDKConfig>(json);
        }
        catch (Exception exception)
        {
            error = "sdk_config.json contains invalid JSON: " + exception.Message;
            return false;
        }

        var errors = Validate(config);
        error = string.Join("\n", errors);
        return errors.Count == 0;
#endif
    }

    public static List<string> Validate(SDKConfig config)
    {
        var errors = new List<string>();
        if (config == null) { errors.Add("The JSON is empty or does not match the SDKConfig structure."); return errors; }
        if (config.schemaVersion != 1) errors.Add("schemaVersion: Expected value 1.");
        if (config.app == null) errors.Add("app: Required section is missing.");
        Required(errors, config.app?.privacyTrackingDescription, "app.privacyTrackingDescription");

        if (config.facebook == null) errors.Add("facebook: Required section is missing.");
        else
        {
            Required(errors, config.facebook.appId, "facebook.appId");
            Required(errors, config.facebook.clientToken, "facebook.clientToken");
        }

        if (config.tenjin == null) errors.Add("tenjin: Required section is missing.");
        else
        {
            Required(errors, config.tenjin.iosApiKey, "tenjin.iosApiKey");
        }

        if (config.oneSignal == null) errors.Add("oneSignal: Required section is missing.");
        else Required(errors, config.oneSignal.appId, "oneSignal.appId");

        if (config.storeLinks == null) errors.Add("storeLinks: Required section is missing.");
        else
        {
            RequiredUrl(errors, config.storeLinks.termsUrl, "storeLinks.termsUrl");
            RequiredUrl(errors, config.storeLinks.privacyPolicyUrl, "storeLinks.privacyPolicyUrl");
        }

        if (config.gameAnalytics == null) errors.Add("gameAnalytics: Required section is missing.");
        else Analytics(errors, config.gameAnalytics.ios, "gameAnalytics.ios");

        if (config.levelPlay == null) errors.Add("levelPlay: Required section is missing.");
        else
        {
            Ads(errors, config.levelPlay.ios, "levelPlay.ios");
            ValidateLevelPlayNetworks(errors, config.levelPlay.networks);
        }

        if (config.iap == null) errors.Add("iap: Required section is missing.");
        else
        {
            if (config.iap.products == null || config.iap.products.Length == 0) errors.Add("iap.products: Add at least one product.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var product in config.iap.products ?? Array.Empty<IAPProductConfig>())
            {
                Required(errors, product?.id, "iap.products[].id");
                if (product != null && !string.IsNullOrWhiteSpace(product.id) && !ids.Add(product.id)) errors.Add("iap.products: Duplicate product ID '" + product.id + "'.");
                if (product != null && product.type != "Consumable" && product.type != "NonConsumable" && product.type != "Subscription") errors.Add("iap.products." + product.id + ".type: Use Consumable, NonConsumable, or Subscription.");
                if (product != null && product.usdCents <= 0) errors.Add("iap.products." + product.id + ".usdCents: Value must be greater than 0.");
            }

            var products = config.iap.products ?? Array.Empty<IAPProductConfig>();
            if (products.Length != 1 || products[0] == null || products[0].type != "NonConsumable")
                errors.Add("iap.products: This template requires exactly one NonConsumable No Ads product.");
        }
        return errors;
    }

    private static void Analytics(List<string> errors, AnalyticsPlatformConfig value, string path)
    {
        if (value == null) { errors.Add(path + ": Required section is missing."); return; }
        Required(errors, value.gameKey, path + ".gameKey");
        Required(errors, value.secretKey, path + ".secretKey");
        if (!string.IsNullOrEmpty(value.gameKey) && value.gameKey.Length != 32) errors.Add(path + ".gameKey: Expected exactly 32 characters.");
        if (!string.IsNullOrEmpty(value.secretKey) && value.secretKey.Length != 40) errors.Add(path + ".secretKey: Expected exactly 40 characters.");
    }

    private static void Ads(List<string> errors, AdPlatformConfig value, string path)
    {
        if (value == null) { errors.Add(path + ": Required section is missing."); return; }
        Required(errors, value.appKey, path + ".appKey");
        Required(errors, value.adMobAppId, path + ".adMobAppId");
        if (!string.IsNullOrWhiteSpace(value.adMobAppId) &&
            !System.Text.RegularExpressions.Regex.IsMatch(value.adMobAppId, @"^ca-app-pub-\d{16}~\d{10}$"))
            errors.Add(path + ".adMobAppId: Expected an AdMob iOS App ID in the format ca-app-pub-################~##########.");
        Required(errors, value.rewardedAdUnitId, path + ".rewardedAdUnitId");
        Required(errors, value.interstitialAdUnitId, path + ".interstitialAdUnitId");
        Required(errors, value.bannerAdUnitId, path + ".bannerAdUnitId");
    }

    private static void ValidateLevelPlayNetworks(List<string> errors, string[] networks)
    {
        if (networks == null || networks.Length == 0)
        {
            errors.Add("levelPlay.networks: Add the required mediation networks.");
            return;
        }

        var supported = new HashSet<string>(StringComparer.Ordinal)
        {
            "UnityAds", "AdMob", "AppLovin", "Chartboost", "Meta", "DTExchange", "InMobi", "LiftoffMonetize"
        };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string network in networks)
        {
            if (!supported.Contains(network)) errors.Add("levelPlay.networks: Unsupported network '" + network + "'.");
            else if (!seen.Add(network)) errors.Add("levelPlay.networks: Duplicate network '" + network + "'.");
        }
    }

    private static void Required(List<string> errors, string value, string path)
    {
        if (string.IsNullOrWhiteSpace(value)) errors.Add(path + ": Required value is empty.");
    }


    private static void RequiredUrl(List<string> errors, string value, string path)
    {
        Required(errors, value, path);
        if (!string.IsNullOrWhiteSpace(value) &&
            (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            errors.Add(path + ": Enter a complete HTTPS URL, for example https://your-domain.com/page.");
        else if (Uri.TryCreate(value, UriKind.Absolute, out var validUri) &&
                 (validUri.Host.Equals("example.com", StringComparison.OrdinalIgnoreCase) ||
                  validUri.Host.EndsWith(".example.com", StringComparison.OrdinalIgnoreCase)))
            errors.Add(path + ": Replace the example.com placeholder with the real published URL.");
    }
}
