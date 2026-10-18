using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class AdPlacementEnumGenerator
{
    internal const string ConfigPath = "Assets/sdk_config.json";
    internal const string OutputPath = "Assets/SDK/Game/AdPlacements.cs";

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };

    static AdPlacementEnumGenerator() => EditorApplication.delayCall += Generate;

    [MenuItem("SDK/Regenerate Ad Placements")]
    internal static void Generate()
    {
        if (!File.Exists(ConfigPath)) return;

        SDKConfig config;
        try { config = JsonUtility.FromJson<SDKConfig>(File.ReadAllText(ConfigPath)); }
        catch { return; }

        AdPlacementsConfig placements = config?.levelPlay?.placements;
        if (placements == null ||
            !TryGetValues(placements.rewarded, out string[] rewarded) ||
            !TryGetValues(placements.interstitial, out string[] interstitial) ||
            !TryGetValues(placements.banner, out string[] banner))
            return;

        string source = BuildSource(rewarded, interstitial, banner);
        if (File.Exists(OutputPath) && File.ReadAllText(OutputPath) == source) return;

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, source, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
    }

    private static bool TryGetValues(string[] configured, out string[] values)
    {
        values = configured ?? Array.Empty<string>();
        return values.All(IsValidIdentifier) &&
               values.Distinct(StringComparer.Ordinal).Count() == values.Length;
    }

    private static bool IsValidIdentifier(string value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Length <= 64 &&
           Regex.IsMatch(value, "^[a-z][a-z0-9_]*$") &&
           !Keywords.Contains(value);

    private static string BuildSource(
        IEnumerable<string> rewarded,
        IEnumerable<string> interstitial,
        IEnumerable<string> banner)
    {
        var source = new StringBuilder();
        source.AppendLine("// This file is generated from Assets/sdk_config.json. Do not edit it manually.");
        AppendEnum(source, "RewardPlacement", rewarded);
        source.AppendLine();
        AppendEnum(source, "InterstitialPlacement", interstitial);
        source.AppendLine();
        AppendEnum(source, "BannerPlacement", banner);
        return source.ToString();
    }

    private static void AppendEnum(StringBuilder source, string enumName, IEnumerable<string> values)
    {
        source.AppendLine("public enum " + enumName);
        source.AppendLine("{");
        foreach (string value in values)
            source.AppendLine("    " + value + ",");
        source.AppendLine("}");
    }
}

internal sealed class AdPlacementConfigPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (importedAssets.Contains(AdPlacementEnumGenerator.ConfigPath) ||
            movedAssets.Contains(AdPlacementEnumGenerator.ConfigPath))
            EditorApplication.delayCall += AdPlacementEnumGenerator.Generate;
    }
}
