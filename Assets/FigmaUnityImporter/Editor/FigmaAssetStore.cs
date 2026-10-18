using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Stanislaw.FigmaUnityImporter.Editor
{
    internal static class FigmaAssetStore
    {
        private const int ImportedTextureMaxSize = 8192;

        private static readonly Dictionary<string, string> SemanticAssetPaths =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public static Sprite GetOrCreateSprite(string base64, string mime, string suggestedName,
            bool allowNearDuplicate = false, string assetReuseKey = null)
        {
            if (string.IsNullOrEmpty(base64))
                return null;

            var bytes = Convert.FromBase64String(base64);
            var hash = ComputeHash(bytes);
            var folder = NormalizeFolder(FigmaBridgeSettings.AssetFolder);
            EnsureFolders(folder);

            var extension = mime == "image/jpeg" ? ".jpg" : ".png";
            // Content-addressed names guarantee that the same pixels always reuse one Unity asset,
            // even when identical Figma instances have different layer names.
            var path = $"{folder}/figma_{hash.Substring(0, 20)}{extension}";
            var replaceEquivalentWithIncoming = false;
            if (!string.IsNullOrEmpty(assetReuseKey) &&
                SemanticAssetPaths.TryGetValue(assetReuseKey, out var semanticPath) &&
                File.Exists(semanticPath))
            {
                path = semanticPath;
                replaceEquivalentWithIncoming = IncomingHasLargerArea(path, bytes);
            }
            else if (TryFindEquivalentImage(folder, extension, bytes, allowNearDuplicate,
                    out var equivalentPath, out replaceEquivalentWithIncoming))
            {
                path = equivalentPath;
            }
            if (!string.IsNullOrEmpty(assetReuseKey)) SemanticAssetPaths[assetReuseKey] = path;
            if (!File.Exists(path) || replaceEquivalentWithIncoming)
                File.WriteAllBytes(path, bytes);

            AssetDatabase.ImportAsset(path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled ||
                                    importer.maxTextureSize < ImportedTextureMaxSize))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = ImportedTextureMaxSize;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static bool IncomingHasLargerArea(string existingPath, byte[] incomingBytes)
        {
            byte[] existingBytes;
            try { existingBytes = File.ReadAllBytes(existingPath); }
            catch (IOException) { return true; }
            if (!TryDecodeImage(existingBytes, out var existingSize, out var existingPixels)) return true;
            if (!TryDecodeImage(incomingBytes, out var incomingSize, out var incomingPixels))
            {
                existingPixels.Clear();
                return false;
            }
            existingPixels.Clear();
            incomingPixels.Clear();
            return (long)incomingSize.x * incomingSize.y > (long)existingSize.x * existingSize.y;
        }

        private static bool TryFindEquivalentImage(string folder, string extension, byte[] incomingBytes,
            bool allowNearDuplicate, out string path, out bool replaceWithIncoming)
        {
            path = null;
            replaceWithIncoming = false;
            if (!TryDecodeImage(incomingBytes, out var incomingSize, out var incomingPixels)) return false;
            try
            {
                var candidates = Directory.GetFiles(folder, $"figma_*{extension}", SearchOption.TopDirectoryOnly)
                    .Select(candidate => candidate.Replace('\\', '/'))
                    .OrderBy(candidate => candidate, StringComparer.Ordinal);
                string bestPath = null;
                long bestArea = -1;
                foreach (var candidate in candidates)
                {
                    byte[] candidateBytes;
                    try { candidateBytes = File.ReadAllBytes(candidate); }
                    catch (IOException) { continue; }
                    if (!TryDecodeImage(candidateBytes, out var candidateSize, out var candidatePixels)) continue;
                    try
                    {
                        var sameSize = candidateSize == incomingSize;
                        var equivalent = sameSize && PixelsAreIdentical(incomingPixels, candidatePixels);
                        if (!equivalent && sameSize && allowNearDuplicate)
                            equivalent = AreEquivalentTintMasks(incomingPixels, candidatePixels);
                        if (!equivalent && !sameSize)
                            equivalent = AreScaleEquivalent(incomingSize, incomingPixels, candidateSize, candidatePixels);
                        if (!equivalent) continue;

                        var area = (long)candidateSize.x * candidateSize.y;
                        if (area > bestArea || area == bestArea &&
                            string.CompareOrdinal(candidate, bestPath) < 0)
                        {
                            bestPath = candidate;
                            bestArea = area;
                        }
                    }
                    finally
                    {
                        candidatePixels.Clear();
                    }
                }
                if (bestPath == null) return false;
                path = bestPath;
                replaceWithIncoming = (long)incomingSize.x * incomingSize.y > bestArea;
                return true;
            }
            finally
            {
                incomingPixels.Clear();
            }
        }

        private static bool TryDecodeImage(byte[] bytes, out Vector2Int size, out List<Color32> pixels)
        {
            size = default;
            pixels = null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false)) return false;
                size = new Vector2Int(texture.width, texture.height);
                pixels = texture.GetPixels32().ToList();
                return true;
            }
            catch (UnityException)
            {
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool PixelsAreIdentical(IReadOnlyList<Color32> left, IReadOnlyList<Color32> right)
        {
            if (left.Count != right.Count) return false;
            for (var i = 0; i < left.Count; i++)
            {
                if (!left[i].Equals(right[i])) return false;
            }
            return true;
        }

        private static bool AreScaleEquivalent(Vector2Int leftSize, IReadOnlyList<Color32> left,
            Vector2Int rightSize, IReadOnlyList<Color32> right)
        {
            var leftAspect = (float)leftSize.x / leftSize.y;
            var rightAspect = (float)rightSize.x / rightSize.y;
            if (Mathf.Abs(leftAspect - rightAspect) > 0.015f) return false;

            const int sampleSize = 48;
            double difference = 0;
            for (var y = 0; y < sampleSize; y++)
            {
                var v = (y + 0.5f) / sampleSize;
                for (var x = 0; x < sampleSize; x++)
                {
                    var u = (x + 0.5f) / sampleSize;
                    var a = SamplePremultiplied(leftSize, left, u, v);
                    var b = SamplePremultiplied(rightSize, right, u, v);
                    difference += Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) +
                                  Mathf.Abs(a.z - b.z) + Mathf.Abs(a.w - b.w);
                }
            }
            return difference / (sampleSize * sampleSize * 4d) <= 4d;
        }

        private static Vector4 SamplePremultiplied(Vector2Int size, IReadOnlyList<Color32> pixels,
            float u, float v)
        {
            var px = Mathf.Clamp01(u) * (size.x - 1);
            var py = Mathf.Clamp01(v) * (size.y - 1);
            var x0 = Mathf.FloorToInt(px);
            var y0 = Mathf.FloorToInt(py);
            var x1 = Mathf.Min(x0 + 1, size.x - 1);
            var y1 = Mathf.Min(y0 + 1, size.y - 1);
            var tx = px - x0;
            var ty = py - y0;
            var bottom = Vector4.Lerp(ToPremultiplied(pixels[y0 * size.x + x0]),
                ToPremultiplied(pixels[y0 * size.x + x1]), tx);
            var top = Vector4.Lerp(ToPremultiplied(pixels[y1 * size.x + x0]),
                ToPremultiplied(pixels[y1 * size.x + x1]), tx);
            return Vector4.Lerp(bottom, top, ty);
        }

        private static Vector4 ToPremultiplied(Color32 color)
        {
            var alpha = color.a / 255f;
            return new Vector4(color.r * alpha, color.g * alpha, color.b * alpha, color.a);
        }

        private static bool AreEquivalentTintMasks(IReadOnlyList<Color32> left, IReadOnlyList<Color32> right)
        {
            if (left.Count != right.Count || left.Count == 0) return false;
            long alphaDifference = 0;
            for (var i = 0; i < left.Count; i++)
            {
                var a = left[i];
                var b = right[i];
                var alphaDelta = Math.Abs(a.a - b.a);
                if (alphaDelta > 16) return false;
                alphaDifference += alphaDelta;
                if (Math.Max(a.a, b.a) <= 8) continue;
                if (Math.Abs(a.r - b.r) > 4 || Math.Abs(a.g - b.g) > 4 || Math.Abs(a.b - b.b) > 4)
                    return false;
            }
            return alphaDifference <= left.Count * 8L;
        }

        private static string ComputeHash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string NormalizeFolder(string folder)
        {
            folder = (folder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            return folder.StartsWith("Assets/", StringComparison.Ordinal) ? folder : FigmaBridgeSettings.DefaultAssetFolder;
        }

        private static void EnsureFolders(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "layer";
            foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            value = value.Replace('/', '_').Replace(' ', '_');
            return value.Length > 48 ? value.Substring(0, 48) : value;
        }
    }
}
