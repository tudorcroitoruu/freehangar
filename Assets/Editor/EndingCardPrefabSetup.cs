using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class EndingCardPrefabSetup
{
    private const string PrefabPath = "Assets/Prefabs/GameCanvas.prefab";
    [InitializeOnLoadMethod]
    private static void Queue() => EditorApplication.delayCall += Apply;

    [MenuItem("Tools/UI/Connect ending card to GameCanvas")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        if (prefab.GetComponentInChildren<EndingCardUI>(true) == null)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = new GameObject("EndingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(EndingCardUI));
                panel.transform.SetParent(root.transform, false);
                var rect = (RectTransform)panel.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                var ui = panel.GetComponent<EndingCardUI>();
                ui.canvas = panel.GetComponent<Canvas>();
                ui.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ui.canvas.overrideSorting = true; ui.canvas.sortingOrder = 32760;
                var scaler = panel.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var black = new GameObject("Black fade", typeof(RectTransform), typeof(Image));
                black.transform.SetParent(panel.transform, false);
                ui.black = black.GetComponent<Image>(); ui.black.color = Color.black; ui.black.raycastTarget = false;
                ui.black.rectTransform.anchorMin = Vector2.zero; ui.black.rectTransform.anchorMax = Vector2.one;
                ui.black.rectTransform.offsetMin = ui.black.rectTransform.offsetMax = Vector2.zero;
                ui.continuedText = Title(panel.transform, "To be continued", "TO BE CONTINUED...", 64, 44);
                ui.friendsText = Title(panel.transform, "Call your friends", "CALL YOUR FRIENDS", 36, -52);
                panel.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var template = prefab.GetComponentInChildren<EndingCardUI>(true);
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(template, out string guid, out long id)) return;
        // Bind the prefab sub-object without loading or saving over an open scene.
        const string scenePath = "Assets/Scenes/Last Scene.unity";
        string scene = File.ReadAllText(scenePath);
        string updated = Regex.Replace(scene, @"(?ms)^--- !u!114 &[^\r\n]+\r?\n(?:(?!^---).)*?guid: 0bba545858bd7e946ab89cee71b9e363(?:(?!^---).)*", match =>
        {
            string block = match.Value;
            string reference = $"  endingUiTemplate: {{fileID: {id}, guid: {guid}, type: 3}}";
            if (Regex.IsMatch(block, @"(?m)^  endingUiTemplate:"))
                return Regex.Replace(block, @"(?m)^  endingUiTemplate:[^\r\n]*", reference);
            return Regex.Replace(block, @"(?m)^(  playOnStart:[^\r\n]*)", "$1\n" + reference);
        });
        if (updated != scene) { File.WriteAllText(scenePath, updated); AssetDatabase.ImportAsset(scenePath); }
        ConfigureLaunchCard();
    }

    private static void ConfigureLaunchCard()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab.GetComponentInChildren<LaunchCardUI>(true) == null)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = new GameObject("LaunchScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(LaunchCardUI));
                panel.transform.SetParent(root.transform, false);
                var rect = (RectTransform)panel.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                var canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true; canvas.sortingOrder = 32000;
                var scaler = panel.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                var ui = panel.GetComponent<LaunchCardUI>();
                var fade = new GameObject("Black fade", typeof(RectTransform), typeof(Image));
                fade.transform.SetParent(panel.transform, false);
                ui.fade = fade.GetComponent<Image>(); ui.fade.color = Color.clear; ui.fade.raycastTarget = false;
                ui.fade.rectTransform.anchorMin = Vector2.zero; ui.fade.rectTransform.anchorMax = Vector2.one;
                ui.fade.rectTransform.offsetMin = ui.fade.rectTransform.offsetMax = Vector2.zero;
                ui.countdown = Title(panel.transform, "Get ready for launch", "Get Ready For The Launch\n3...", 36, 145);
                ui.dialogue = Title(panel.transform, "Player dialogue", "— No, no, let me out!", 25, 70);
                foreach (var text in new[] { ui.countdown, ui.dialogue })
                {
                    text.rectTransform.anchorMin = new Vector2(.1f, 0);
                    text.rectTransform.anchorMax = new Vector2(.9f, 0);
                    text.characterSpacing = 0;
                }
                panel.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponentInChildren<LaunchCardUI>(true);
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(template, out string guid, out long id)) return;
        const string scenePath = "Assets/Scenes/Gameplay.unity";
        string scene = File.ReadAllText(scenePath);
        string updated = Regex.Replace(scene, @"(?ms)^--- !u!114 &[^\r\n]+\r?\n(?:(?!^---).)*?guid: d0397dfd3909d7b42a811976621667cd(?:(?!^---).)*", match =>
        {
            string reference = $"  launchUiTemplate: {{fileID: {id}, guid: {guid}, type: 3}}";
            if (Regex.IsMatch(match.Value, @"(?m)^  launchUiTemplate:"))
                return Regex.Replace(match.Value, @"(?m)^  launchUiTemplate:[^\r\n]*", reference);
            return Regex.Replace(match.Value, @"(?m)^(  hangar:[^\r\n]*)", reference + "\n$1");
        });
        if (updated != scene) { File.WriteAllText(scenePath, updated); AssetDatabase.ImportAsset(scenePath); }
    }

    private static TextMeshProUGUI Title(Transform parent, string name, string message, float size, float y)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = message; text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.characterSpacing = 3;
        text.color = Color.white; text.raycastTarget = false;
        text.enableAutoSizing = true; text.fontSizeMin = size * .55f; text.fontSizeMax = size;
        var rect = text.rectTransform; rect.anchorMin = new Vector2(.08f, .5f); rect.anchorMax = new Vector2(.92f, .5f);
        rect.sizeDelta = new Vector2(0, 100); rect.anchoredPosition = new Vector2(0, y);
        return text;
    }
}
