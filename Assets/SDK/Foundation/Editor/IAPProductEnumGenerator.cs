using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class IAPProductEnumGenerator
{
    internal const string ConfigPath = "Assets/sdk_config.json";
    internal const string OutputPath = "Assets/SDK/Game/IAPProducts.cs";

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

    static IAPProductEnumGenerator() => EditorApplication.delayCall += Generate;

    [MenuItem("SDK/Regenerate IAP Product IDs")]
    internal static void Generate()
    {
        if (!File.Exists(ConfigPath)) return;

        SDKConfig config;
        try { config = JsonUtility.FromJson<SDKConfig>(File.ReadAllText(ConfigPath)); }
        catch { return; }

        IAPProductConfig[] products = config?.iap?.products;
        if (products == null || products.Length == 0) return;

        var entries = products
            .Where(product => product != null && !string.IsNullOrWhiteSpace(product.id))
            .Select(product => new Entry(
                ToIdentifier(string.IsNullOrWhiteSpace(product.scope) ? product.id : product.scope),
                product.id))
            .ToArray();

        if (entries.Length == 0 || entries.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            return;

        string source = BuildSource(entries);
        if (File.Exists(OutputPath) && File.ReadAllText(OutputPath) == source) return;

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, source, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
    }

    private static string BuildSource(IEnumerable<Entry> entries)
    {
        Entry[] values = entries.ToArray();
        var source = new StringBuilder();
        source.AppendLine("// This file is generated from Assets/sdk_config.json. Do not edit it manually.");
        source.AppendLine("public readonly struct IAPProduct");
        source.AppendLine("{");
        source.AppendLine("    internal string Id { get; }");
        source.AppendLine();
        source.AppendLine("    private IAPProduct(string id) => Id = id;");
        source.AppendLine();
        foreach (Entry entry in values)
            source.AppendLine("    public static readonly IAPProduct " + entry.Name + " = new IAPProduct(\"" + Escape(entry.Id) + "\");");
        source.AppendLine();
        source.AppendLine("    public override string ToString() => Id;");
        source.AppendLine("}");
        return source.ToString();
    }

    private static string ToIdentifier(string id)
    {
        var value = new StringBuilder(id.Length + 8);
        foreach (char character in id)
            if (char.IsLetterOrDigit(character) || character == '_') value.Append(character);
        if (value.Length == 0 || char.IsDigit(value[0])) value.Insert(0, "product_");
        string identifier = value.ToString();
        return Keywords.Contains(identifier) ? "product_" + identifier : identifier;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private readonly struct Entry
    {
        internal Entry(string name, string id) { Name = name; Id = id; }
        internal string Name { get; }
        internal string Id { get; }
    }
}

internal sealed class IAPProductConfigPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (importedAssets.Contains(IAPProductEnumGenerator.ConfigPath) ||
            movedAssets.Contains(IAPProductEnumGenerator.ConfigPath))
            EditorApplication.delayCall += IAPProductEnumGenerator.Generate;
    }
}
