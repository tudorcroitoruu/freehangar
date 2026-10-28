using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BagInteractionSetup
{
    [MenuItem("Tools/Grain/Add bag finishing and carrying")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before configuring bags.");
        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null) throw new System.InvalidOperationException("PlayerController not found.");
        var gameplayCanvas = Object.FindFirstObjectByType<GameplayCanvas>();
        if (gameplayCanvas == null) throw new System.InvalidOperationException("GameplayCanvas not found.");
        var playerData = new SerializedObject(player);
        var hands = (Transform)playerData.FindProperty("heldInstrument").objectReferenceValue;
        var shovel = (ShovelTool)playerData.FindProperty("shovelTool").objectReferenceValue;
        var interaction = player.GetComponent<BagInteraction>();
        if (interaction == null) interaction = Undo.AddComponent<BagInteraction>(player.gameObject);
        Transform anchor = player.characterCamera.transform.Find("Bag carry anchor");
        if (anchor == null)
        {
            var go = new GameObject("Bag carry anchor");
            Undo.RegisterCreatedObjectUndo(go, "Create bag carry anchor");
            anchor = go.transform;
            anchor.SetParent(player.characterCamera.transform, false);
            anchor.localPosition = new Vector3(.35f, -.35f, 1.05f);
        }
        var hud = gameplayCanvas.interactionPrompt != null ? gameplayCanvas.interactionPrompt.gameObject : GameObject.Find("Bag interaction HUD");
        if (hud == null)
        {
            hud = new GameObject("Bag interaction HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(InteractionPrompt));
            Undo.RegisterCreatedObjectUndo(hud, "Create bag interaction HUD");
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = hud.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var panel = Rect("Bag prompt", hud.transform, new Vector2(206, 54), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(.16f, .095f, .045f, .94f);
            var key = Rect("E key", panel, new Vector2(38, 38), new Vector2(-75, 0));
            key.gameObject.AddComponent<Image>().color = new Color(.97f, .9f, .75f);
            var keyLabel = Label("E", key, new Vector2(38, 38), Vector2.zero, 26, new Color(.22f, .14f, .08f));
            var label = Label("Finish", panel, new Vector2(148, 44), new Vector2(24, 0), 22, new Color(1f, .94f, .82f));
            label.enableAutoSizing = true; label.fontSizeMin = 14; label.fontSizeMax = 22;
            foreach (var graphic in hud.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
            var promptData = new SerializedObject(hud.GetComponent<InteractionPrompt>());
            promptData.FindProperty("panel").objectReferenceValue = panel;
            promptData.FindProperty("canvasRect").objectReferenceValue = hud.GetComponent<RectTransform>();
            promptData.FindProperty("description").objectReferenceValue = label;
            promptData.FindProperty("keycap").objectReferenceValue = keyLabel;
            promptData.ApplyModifiedProperties();
            panel.gameObject.SetActive(false);
        }
        var data = new SerializedObject(interaction);
        data.FindProperty("viewCamera").objectReferenceValue = player.characterCamera;
        data.FindProperty("shovel").objectReferenceValue = shovel;
        data.FindProperty("shovelPresentation").objectReferenceValue = hands;
        data.FindProperty("carryAnchor").objectReferenceValue = anchor;
        data.ApplyModifiedProperties();
        var interactor = player.GetComponent<PlayerInteractor>();
        if (interactor == null) interactor = Undo.AddComponent<PlayerInteractor>(player.gameObject);
        var interactorData = new SerializedObject(interactor);
        interactorData.FindProperty("viewCamera").objectReferenceValue = player.characterCamera;
        interactorData.ApplyModifiedProperties();
        var canvasData = new SerializedObject(gameplayCanvas);
        canvasData.FindProperty("interactionPrompt").objectReferenceValue = hud.GetComponent<InteractionPrompt>();
        canvasData.ApplyModifiedProperties();
        playerData.FindProperty("bagInteraction").objectReferenceValue = interaction;
        playerData.ApplyModifiedProperties();
        foreach (var bag in Object.FindObjectsByType<GrainContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            CornPresentationSetup.FitSackCollider(bag);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorSceneManager.MarkSceneDirty(gameplayCanvas.gameObject.scene);
        AssetDatabase.SaveAssets();
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }

    private static TextMeshProUGUI Label(string text, Transform parent, Vector2 size, Vector2 position, float fontSize, Color color)
    {
        var label = Rect(text, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text; label.fontSize = fontSize; label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center; label.color = color;
        return label;
    }
}
