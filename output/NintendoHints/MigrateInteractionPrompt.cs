using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MigrateInteractionPrompt
{
    public static string Run()
    {
        const string path = "Assets/Prefabs/GameCanvas.prefab";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        string imagePath = null;
        foreach (var node in asset.GetComponentsInChildren<Transform>(true))
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(node.gameObject, out string guid, out long id);
            if (id == 1401144034268598406L) imagePath = AnimationUtility.CalculateTransformPath(node, asset.transform);
        }
        if (imagePath == null) throw new Exception("Original mouseRight object was not found.");
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var prompt = root.GetComponentInChildren<InteractionPrompt>(true);
            var so = new SerializedObject(prompt);
            var image = root.transform.Find(imagePath).GetComponent<Image>();
            if (image == null) throw new Exception("Original mouseRight has no Image.");
            image.name = "InteractionImage";
            image.sprite = AssetDatabase.LoadAssetAtPath<GamepadPromptSettings>("Assets/Resources/GamepadPromptSettings.asset")
                .prompts.First(p => p.control == "buttonNorth").sprite;
            image.material = null;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = true;
            image.rectTransform.sizeDelta = new Vector2(35, 35);
            image.gameObject.SetActive(true);
            so.FindProperty("interactionImage").objectReferenceValue = image;
            so.FindProperty("promptSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GamepadPromptSettings>("Assets/Resources/GamepadPromptSettings.asset");
            var background = so.FindProperty("buttonImageBg").objectReferenceValue as Image;
            if (background != null) background.enabled = false;
            var panel = (RectTransform)so.FindProperty("panel").objectReferenceValue;
            // Retire only the old keyboard/mouse controls; keep descriptions and progress UI.
            foreach (var text in panel.GetComponentsInChildren<TMPro.TMP_Text>(true))
                if (text.name == "Keycap" || text.name == "Key" || text.name == "Button")
                    UnityEngine.Object.DestroyImmediate(text.gameObject);
            foreach (var node in panel.GetComponentsInChildren<Transform>(true))
                if (node != null && node != image.transform && (node.name == "MouseLeft" || node.name == "Mouse Left" || node.name == "mouseLeft"))
                    UnityEngine.Object.DestroyImmediate(node.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "Reused " + imagePath + " as an Image; bound Nintendo prompt settings and saved GameCanvas.prefab.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
