using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ValidateInteractionPrompt
{
    public static string Run()
    {
        var result = new System.Text.StringBuilder();
        var fixture = new FreeHandsWarningTests();
        fixture.SetUp();
        try { fixture.PromptShowsSeparateKeyRowsAndRemovesThemForTheNextObject(); }
        finally { fixture.TearDown(); }
        result.AppendLine("PASS: actual InteractionPrompt.Show renders X/Y, ZL/ZR and separate action images; hides unavailable images and clears extra rows.");
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/GameCanvas.prefab");
        try
        {
            var prompt = root.GetComponentInChildren<InteractionPrompt>(true);
            var so = new SerializedObject(prompt);
            var image = so.FindProperty("interactionImage").objectReferenceValue as Image;
            if (image == null || so.FindProperty("promptSettings").objectReferenceValue == null)
                throw new Exception("Prefab image/settings references are missing.");
            if (so.FindProperty("keycap") != null || so.FindProperty("mouseLeft") != null || so.FindProperty("mouseRight") != null)
                throw new Exception("Legacy prompt fields remain.");
            result.AppendLine("PASS: saved prefab uses one InteractionImage and Nintendo settings, with no legacy prompt fields.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        File.WriteAllText("output/NintendoHints/interaction-validation.txt", result.ToString());
        return result.ToString();
    }
}
