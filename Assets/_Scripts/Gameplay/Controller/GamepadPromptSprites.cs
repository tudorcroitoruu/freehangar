using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

// Register each assigned sprite with TMP so hints also work inside longer text.
public static class GamepadPromptSprites
{
    private static GamepadPromptSettings settings;
    private static readonly Dictionary<string, string> labels = new Dictionary<string, string>();
    private static readonly Dictionary<string, TMP_SpriteAsset> assets = new Dictionary<string, TMP_SpriteAsset>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (var asset in assets.Values)
        {
            if (!asset) continue;
            Object.Destroy(asset.material);
            Object.Destroy(asset);
        }
        assets.Clear();
        labels.Clear();
        settings = null;
    }

    public static void SetKeycap(TMP_Text label, string value)
    {
        if (!label) return;
        if (value != null && assets.TryGetValue(value, out var asset) && asset)
        {
            label.richText = true;
            label.spriteAsset = asset;
            label.text = "<sprite=0>";
            return;
        }
        label.text = value ?? "";
    }

    public static string Label(string control)
    {
        if (string.IsNullOrEmpty(control)) return null;
        if (labels.TryGetValue(control, out var value)) return value;
        if (!settings) settings = Resources.Load<GamepadPromptSettings>("GamepadPromptSettings");
        if (!settings || settings.prompts == null) return null;
        for (int i = 0; i < settings.prompts.Length; i++)
        {
            var prompt = settings.prompts[i];
            if (prompt == null || prompt.control != control || !prompt.sprite) continue;
            var sprite = prompt.sprite;
            var rect = sprite.textureRect;
            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "GamepadPrompts_" + i;
            asset.spriteSheet = sprite.texture;
            asset.material = new Material(Shader.Find("TextMeshPro/Sprite"));
            asset.material.mainTexture = sprite.texture;
            asset.spriteInfoList = new List<TMP_Sprite>();
            asset.UpdateLookupTables();
            float width = 64f * rect.width / rect.height;
            var glyph = new TMP_SpriteGlyph(0, new GlyphMetrics(width, 64, 0, 52, width),
                new GlyphRect(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y),
                    Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height)), 1, 0);
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter(0xe000, glyph) { name = "icon", scale = 1 });
            asset.UpdateLookupTables();
            MaterialReferenceManager.AddSpriteAsset(TMP_TextUtilities.GetHashCode(asset.name), asset);
            value = "<sprite=\"" + asset.name + "\" name=\"icon\">";
            labels.Add(control, value);
            assets.Add(value, asset);
            return value;
        }
        return null;
    }
}
