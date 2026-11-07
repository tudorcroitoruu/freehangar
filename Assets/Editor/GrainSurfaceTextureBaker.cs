using System.IO;
using UnityEditor;
using UnityEngine;

// Offline bake: no procedural texture generation or per-kernel objects in a build.
public static class GrainSurfaceTextureBaker
{
    private const string Path = "Assets/Resources/GrainSurfaceDetail.asset";
    private const int Size = 256, Cells = 8;

    [InitializeOnLoadMethod]
    private static void EnsureTextures()
    {
        EditorApplication.delayCall += () => { if (!File.Exists(Path)) Bake(); };
    }

    [MenuItem("Tools/Grain/Bake Surface Detail Textures")]
    public static void Bake()
    {
        var texture = new Texture2DArray(Size, Size, 10, TextureFormat.RGBA32, true, true)
        { name = "Grain surface normals and height", wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        for (int type = 0; type < 10; type++)
        {
            var heights = new float[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++) heights[y * Size + x] = Height(x * (float)Cells / Size, y * (float)Cells / Size, type);
            var pixels = new Color[heights.Length];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = heights[y * Size + (x + 1) % Size] - heights[y * Size + (x + Size - 1) % Size];
                float dy = heights[((y + 1) % Size) * Size + x] - heights[((y + Size - 1) % Size) * Size + x];
                Vector3 normal = new Vector3(-dx * 5f, -dy * 5f, 1f).normalized;
                pixels[y * Size + x] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, heights[y * Size + x], 1f);
            }
            texture.SetPixels(pixels, type);
        }
        texture.Apply(true, true);
        var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(Path);
        if (existing != null) { EditorUtility.CopySerialized(texture, existing); Object.DestroyImmediate(texture); EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(texture, Path);
        AssetDatabase.SaveAssets();
        Debug.Log("Baked ten seamless grain normal/height maps at " + Path);
    }

    private static float Noise(int x, int y, int type)
    {
        uint value = (uint)(((x % Cells + Cells) % Cells) + ((y % Cells + Cells) % Cells) * 137 + type * 7919 + 31);
        value ^= value >> 16; value *= 0x7feb352d; value ^= value >> 15; value *= 0x846ca68b; value ^= value >> 16;
        return (value & 65535) / 65535f;
    }

    private static float Height(float x, float y, int type)
    {
        int cellX = Mathf.FloorToInt(x), cellY = Mathf.FloorToInt(y);
        float height = .08f;
        for (int j = -1; j <= 1; j++)
        for (int i = -1; i <= 1; i++)
        {
            int cx = cellX + i, cy = cellY + j;
            float seed = Noise(cx, cy, type);
            float angle = seed * Mathf.PI * 2f;
            float px = x - (cx + .5f + (Noise(cx, cy, type + 11) - .5f) * .32f);
            float py = y - (cy + .5f + (Noise(cx, cy, type + 23) - .5f) * .32f);
            float u = px * Mathf.Cos(angle) - py * Mathf.Sin(angle);
            float v = px * Mathf.Sin(angle) + py * Mathf.Cos(angle);
            float width = type >= 1 && type <= 5 ? .34f : type == 8 ? .37f : .58f;
            float length = type >= 1 && type <= 5 || type == 8 ? .72f : .58f;
            float shape = type == 9 ? Mathf.Abs(u) / width + Mathf.Abs(v) / length :
                u * u / (width * width) + v * v / (length * length);
            float dome = Mathf.Sqrt(Mathf.Clamp01(1f - shape));
            if (type >= 1 && type <= 4) dome *= 1f - .28f * Mathf.Exp(-u * u * 650f);
            if (type == 8) dome *= .85f + .15f * Mathf.Cos(u * 65f);
            height = Mathf.Max(height, dome * (.8f + .2f * seed));
        }
        return height;
    }
}
