using UnityEditor;
using UnityEngine;

namespace Liviu.Environment.HDRP.Editor
{
    internal static class PolyverseStyleStarCubemapGenerator
    {
        private const int CubemapSize = 512;
        private const string GeneratedFolder = "Assets/PolyverseStyleHDRPSky/Generated";
        private const string AssetPath = GeneratedFolder + "/Polyverse_DefaultStarsCubemap.asset";

        [InitializeOnLoadMethod]
        private static void GenerateDefaultAssetOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isPlaying)
                    Generate(false);
            };
        }

        [MenuItem("Tools/Polyverse Style Sky/Generate Default Stars Cubemap")]
        public static void GenerateFromMenu()
        {
            Generate(true);
            Object asset = AssetDatabase.LoadAssetAtPath<Cubemap>(AssetPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void Generate(bool overwrite)
        {
            if (!overwrite && AssetDatabase.LoadAssetAtPath<Cubemap>(AssetPath) != null)
                return;

            EnsureGeneratedFolderExists();

            if (overwrite)
                AssetDatabase.DeleteAsset(AssetPath);

            Cubemap cubemap = new(CubemapSize, TextureFormat.RGBA32, false)
            {
                name = "Polyverse_DefaultStarsCubemap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            };

            foreach (CubemapFace face in new[]
                     {
                         CubemapFace.PositiveX,
                         CubemapFace.NegativeX,
                         CubemapFace.PositiveY,
                         CubemapFace.NegativeY,
                         CubemapFace.PositiveZ,
                         CubemapFace.NegativeZ
                     })
            {
                cubemap.SetPixels(CreateFacePixels(face), face);
            }

            cubemap.Apply(false, true);
            AssetDatabase.CreateAsset(cubemap, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureGeneratedFolderExists()
        {
            if (AssetDatabase.IsValidFolder(GeneratedFolder))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/PolyverseStyleHDRPSky"))
                AssetDatabase.CreateFolder("Assets", "PolyverseStyleHDRPSky");

            AssetDatabase.CreateFolder("Assets/PolyverseStyleHDRPSky", "Generated");
        }

        private static Color[] CreateFacePixels(CubemapFace face)
        {
            Color[] pixels = new Color[CubemapSize * CubemapSize];

            for (int y = 0; y < CubemapSize; y++)
            {
                float v = 2f * (y + 0.5f) / CubemapSize - 1f;

                for (int x = 0; x < CubemapSize; x++)
                {
                    float u = 2f * (x + 0.5f) / CubemapSize - 1f;
                    Vector3 direction = DirectionFromCubemapFace(face, u, v);
                    pixels[y * CubemapSize + x] = EvaluateStars(direction);
                }
            }

            return pixels;
        }

        private static Color EvaluateStars(Vector3 direction)
        {
            float haze = GalacticHaze(direction);

            float redLayer = Mathf.Max(
                haze * 0.08f,
                StarLayer(direction, 165f, 0.9865f, 0.028f, 0.18f, 0.0f));

            float greenLayer = Mathf.Max(
                haze * 0.12f,
                StarLayer(direction, 120f, 0.9935f, 0.04f, 0.35f, 29.13f));

            float blueLayer = Mathf.Max(
                haze * 0.18f,
                StarLayer(direction, 82f, 0.9975f, 0.062f, 0.65f, 73.71f));

            float alpha = Mathf.Max(redLayer, Mathf.Max(greenLayer, blueLayer));
            return new Color(redLayer, greenLayer, blueLayer, alpha);
        }

        private static float StarLayer(
            Vector3 direction,
            float scale,
            float densityThreshold,
            float radius,
            float baseBrightness,
            float salt)
        {
            Vector3 p = direction * scale + new Vector3(salt, salt * 1.37f, salt * 2.11f);
            Vector3 cell = Floor(p);
            Vector3 local = Frac(p) - Vector3.one * 0.5f;
            float random = Hash31(cell + new Vector3(4.7f, 13.1f, 29.3f) + Vector3.one * salt);

            if (random < densityThreshold)
                return 0f;

            Vector3 starPosition = new(
                Hash31(cell + new Vector3(13.1f, 0.0f, salt)),
                Hash31(cell + new Vector3(0.0f, 27.7f, salt)),
                Hash31(cell + new Vector3(41.3f, salt, 0.0f)));

            starPosition -= Vector3.one * 0.5f;

            float distance = Vector3.Distance(local, starPosition);
            float core = SmoothStep(radius, 0f, distance);
            float glow = SmoothStep(radius * 2.6f, 0f, distance) * 0.22f;
            float brightness = Mathf.Lerp(baseBrightness, 1f, Hash31(cell + new Vector3(91.13f, salt, 7.0f)));

            return Mathf.Clamp01((core + glow) * brightness);
        }

        private static float GalacticHaze(Vector3 direction)
        {
            Vector3 bandNormal = new(0.28f, 0.84f, -0.47f);
            float distanceFromBand = Mathf.Abs(Vector3.Dot(direction.normalized, bandNormal.normalized));
            float band = Mathf.Pow(Mathf.Clamp01(1f - distanceFromBand * 5.5f), 2.2f);
            float noise = Hash31(Floor(direction * 38f) + new Vector3(19.0f, 37.0f, 53.0f));
            return band * Mathf.Lerp(0.35f, 1f, noise) * 0.45f;
        }

        private static Vector3 DirectionFromCubemapFace(CubemapFace face, float u, float v)
        {
            Vector3 direction = face switch
            {
                CubemapFace.PositiveX => new Vector3(1f, -v, -u),
                CubemapFace.NegativeX => new Vector3(-1f, -v, u),
                CubemapFace.PositiveY => new Vector3(u, 1f, v),
                CubemapFace.NegativeY => new Vector3(u, -1f, -v),
                CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
                CubemapFace.NegativeZ => new Vector3(-u, -v, -1f),
                _ => Vector3.forward
            };

            return direction.normalized;
        }

        private static Vector3 Floor(Vector3 value)
        {
            return new Vector3(
                Mathf.Floor(value.x),
                Mathf.Floor(value.y),
                Mathf.Floor(value.z));
        }

        private static Vector3 Frac(Vector3 value)
        {
            return new Vector3(
                value.x - Mathf.Floor(value.x),
                value.y - Mathf.Floor(value.y),
                value.z - Mathf.Floor(value.z));
        }

        private static float SmoothStep(float from, float to, float value)
        {
            if (Mathf.Approximately(from, to))
                return value < to ? 0f : 1f;

            float t = Mathf.Clamp01((value - from) / (to - from));
            return t * t * (3f - 2f * t);
        }

        private static float Hash31(Vector3 p)
        {
            float value = Mathf.Sin(Vector3.Dot(p, new Vector3(127.1f, 311.7f, 74.7f))) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
