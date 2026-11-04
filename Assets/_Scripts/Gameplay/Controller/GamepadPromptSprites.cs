using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
// A named TMP sprite asset lets inline interaction hints use the same PNG prompts.
public static class GamepadPromptSprites
{
    private static TMP_SpriteAsset asset;
    public static string Label(string control)
    {
        string name = control switch
        {
            "buttonNorth" => "triangle", "buttonSouth" => "cross",
            "buttonWest" => "square", "buttonEast" => "circle",
            "start" => "options", "select" => "share", _ => null
        };
        if (name == null) return null;
        if (!asset)
        {
            var texture = Resources.Load<Texture2D>("GamepadPrompts");
            if (!texture) return null;
            asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = "GamepadPrompts";
            asset.spriteSheet = texture;
            asset.material = new Material(Shader.Find("TextMeshPro/Sprite"));
            asset.material.mainTexture = texture;
            // Initialize TMP's legacy version migration before adding modern glyphs.
            asset.spriteInfoList = new System.Collections.Generic.List<TMP_Sprite>();
            asset.UpdateLookupTables();
            string[] names = { "triangle", "cross", "square", "circle", "options", "share" };
            for (int i = 0; i < names.Length; i++)
            {
                var glyph = new TMP_SpriteGlyph((uint)i, new GlyphMetrics(64, 64, 0, 52, 64),
                    new GlyphRect(i * 64, 0, 64, 64), 1, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter((uint)(0xe000 + i), glyph) { name = names[i], scale = 1 });
            }
            asset.UpdateLookupTables();
            MaterialReferenceManager.AddSpriteAsset(TMP_TextUtilities.GetHashCode(asset.name), asset);
        }
        return "<sprite=\"GamepadPrompts\" name=\"" + name + "\">";
    }
}
