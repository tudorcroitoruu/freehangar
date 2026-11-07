using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
// A named TMP sprite asset lets inline interaction hints use the same PNG prompts.
public static class GamepadPromptSprites
{
    private static TMP_SpriteAsset asset;
    private static readonly string[] Names = {
        "Y", "A", "X", "B", "menu", "view", "LB", "RB",
        "LT", "RT", "L3", "R3", "LeftStick", "RightStick", "dPad_All", "dPad_Up",
        "dPad_Down", "dPad_Left", "dPad_Right", "Home"
    };
    public static void SetKeycap(TMP_Text label, string value)
    {
        if (!label) return;
        const string prefix = "<sprite=\"GamepadPrompts\" name=\"";
        if (value != null && value.StartsWith(prefix, System.StringComparison.Ordinal) && asset)
        {
            string name = value.Substring(prefix.Length).TrimEnd('>', '"');
            int index = asset.GetSpriteIndexFromName(name);
            if (index >= 0)
            {
                label.richText = true;
                label.spriteAsset = asset;
                label.text = "<sprite=" + index + ">";
                return;
            }
        }
        label.text = value ?? "";
    }

    public static string Label(string control)
    {
        string name = control switch
        {
            "buttonNorth" => "Y", "buttonSouth" => "A",
            "buttonWest" => "X", "buttonEast" => "B",
            "start" => "menu", "select" => "view",
            "leftShoulder" => "LB", "rightShoulder" => "RB",
            "leftTrigger" => "LT", "rightTrigger" => "RT",
            "leftStickPress" => "L3", "rightStickPress" => "R3",
            "leftStick" => "LeftStick", "rightStick" => "RightStick",
            "dpad" => "dPad_All", "dpad/up" => "dPad_Up", "dpad/down" => "dPad_Down",
            "dpad/left" => "dPad_Left", "dpad/right" => "dPad_Right", _ => null
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
            const int tile = 128, columns = 8, rows = 3;
            for (int i = 0; i < Names.Length; i++)
            {
                var glyph = new TMP_SpriteGlyph((uint)i, new GlyphMetrics(64, 64, 0, 52, 64),
                    new GlyphRect(i % columns * tile, (rows - 1 - i / columns) * tile, tile, tile), 1, 0);
                asset.spriteGlyphTable.Add(glyph);
                asset.spriteCharacterTable.Add(new TMP_SpriteCharacter((uint)(0xe000 + i), glyph) { name = Names[i], scale = 1 });
            }
            asset.UpdateLookupTables();
            MaterialReferenceManager.AddSpriteAsset(TMP_TextUtilities.GetHashCode(asset.name), asset);
        }
        return "<sprite=\"GamepadPrompts\" name=\"" + name + "\">";
    }
}
