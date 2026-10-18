using System;
using UnityEditor;
using UnityEngine;

internal static class TextureCompressor
{
    private const string IOSPlatformName = "iPhone";

    [MenuItem("Tools/Compress Textures")]
    private static void CompressTextures()
    {
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int updated = 0;
        int unchanged = 0;
        int skipped = 0;

        try
        {
            for (int index = 0; index < textureGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[index]);
                EditorUtility.DisplayProgressBar(
                    "Compressing textures for iOS",
                    path,
                    textureGuids.Length == 0 ? 1f : (float)index / textureGuids.Length);

                if (ShouldSkip(path))
                {
                    skipped++;
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || !SupportsASTC(importer))
                {
                    skipped++;
                    continue;
                }

                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                int largestDimension = Mathf.Max(width, height);
                bool importantUI = IsImportantUI(importer, path);
                TextureImporterFormat format = SelectFormat(largestDimension, importantUI);
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(IOSPlatformName);

                if (settings.overridden &&
                    settings.format == format &&
                    settings.textureCompression == TextureImporterCompression.CompressedHQ &&
                    settings.compressionQuality == 100)
                {
                    unchanged++;
                    continue;
                }

                settings.name = IOSPlatformName;
                settings.overridden = true;
                settings.format = format;
                settings.textureCompression = TextureImporterCompression.CompressedHQ;
                settings.compressionQuality = 100;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                updated++;
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("[Texture Compressor] iOS ASTC compression failed: " + exception);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log(
            "[Texture Compressor] iOS ASTC compression complete. Updated: " + updated +
            ", already configured: " + unchanged +
            ", skipped: " + skipped + ".");
    }

    private static TextureImporterFormat SelectFormat(int largestDimension, bool importantUI)
    {
        if (importantUI)
        {
            if (largestDimension <= 512) return TextureImporterFormat.ASTC_5x5;
            if (largestDimension <= 1024) return TextureImporterFormat.ASTC_6x6;
            if (largestDimension <= 2048) return TextureImporterFormat.ASTC_8x8;
            return TextureImporterFormat.ASTC_10x10;
        }

        if (largestDimension <= 512) return TextureImporterFormat.ASTC_6x6;
        if (largestDimension < 1024) return TextureImporterFormat.ASTC_8x8;
        return TextureImporterFormat.ASTC_10x10;
    }

    private static bool IsImportantUI(TextureImporter importer, string path)
    {
        if (importer.textureType != TextureImporterType.Sprite) return false;

        string value = path.ToLowerInvariant();
        string[] markers =
        {
            "/ui/", "button", "btn_", "icon", "hud", "popup", "panel",
            "menu", "screen", "window", "dialog", "badge", "logo", "frame",
            "tab", "toggle"
        };

        foreach (string marker in markers)
            if (value.Contains(marker)) return true;
        return false;
    }

    private static bool SupportsASTC(TextureImporter importer)
    {
        return importer.textureType != TextureImporterType.Lightmap &&
               importer.textureType != TextureImporterType.DirectionalLightmap &&
               importer.textureType != TextureImporterType.Shadowmask;
    }

    private static bool ShouldSkip(string path)
    {
        string value = path.Replace('\\', '/');
        return value.StartsWith("Assets/SDK/Vendor/", StringComparison.OrdinalIgnoreCase) ||
               value.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("/Gizmos/", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.EndsWith(".exr", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith(".hdr", StringComparison.OrdinalIgnoreCase);
    }
}
