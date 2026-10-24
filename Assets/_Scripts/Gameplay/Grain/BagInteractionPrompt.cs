using TMPro;
using UnityEngine;

/// <summary>Screen-space presentation anchored to the bag's world position.</summary>
public sealed class BagInteractionPrompt : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private TMP_Text description;
    [SerializeField] private GameObject keycap;
    public string CurrentText { get; private set; } = "";
    public bool IsVisible => panel != null && panel.gameObject.activeSelf;

    public void Show(Camera camera, Vector3 worldPosition, string action, bool showKey = true)
    {
        if (panel == null || canvasRect == null || camera == null || string.IsNullOrEmpty(action)) { Hide(); return; }
        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) { Hide(); return; }
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        if (CurrentText != action) { CurrentText = action; description.text = action; }
        if (keycap.activeSelf != showKey) keycap.SetActive(showKey);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 position);
        Vector2 margin = panel.sizeDelta * .5f + Vector2.one * 12f;
        Rect bounds = canvasRect.rect;
        position.x = Mathf.Clamp(position.x, bounds.xMin + margin.x, bounds.xMax - margin.x);
        position.y = Mathf.Clamp(position.y, bounds.yMin + margin.y, bounds.yMax - margin.y);
        panel.anchoredPosition = position;
    }

    public void Hide()
    {
        if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
        CurrentText = "";
    }
}
