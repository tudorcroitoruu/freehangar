using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen-space presentation anchored to the bag's world position.</summary>
public sealed class BagInteractionPrompt : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private TMP_Text description;
    [SerializeField] private GameObject keycap;
    [SerializeField] private GameObject mouseLeft;

    [SerializeField] private GameObject bagSliderFill;
    [SerializeField] private Image bagFill;
    [SerializeField] private Text bagFillStatus;
    public string CurrentText { get; private set; } = "";
    public bool IsVisible => panel != null && panel.gameObject.activeSelf;

    public void Show(Camera camera, Vector3 worldPosition, string action, bool showKey = true, GrainContainer bag = null)
    {
        if (bag != null && bag.IsClosing) { Hide(); return; }
        bool showFill = bag != null && !bag.IsFull;
        if (panel == null || canvasRect == null || camera == null || (string.IsNullOrEmpty(action) && !showFill)) { Hide(); return; }
        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) { Hide(); return; }
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        if (showFill) action = "Fill";
        showKey = showKey && !showFill;
        CurrentText = action;
        if (description != null && description.text != action) description.text = action;
        if (keycap != null && keycap.activeSelf != showKey) keycap.SetActive(showKey);
        if (mouseLeft != null && mouseLeft.activeSelf != showFill) mouseLeft.SetActive(showFill);
        if (bagSliderFill != null && bagSliderFill.activeSelf != showFill) bagSliderFill.SetActive(showFill);
        if (bag != null)
        {
            if (bagFill != null) bagFill.fillAmount = bag.CapacityKg > 0f ? Mathf.Clamp01(bag.Kilograms / bag.CapacityKg) : 0f;
            if (bagFillStatus != null)
            {
                string status = $"{bag.Kilograms:0.##} / {bag.CapacityKg:0.##} kg";
                if (bagFillStatus.text != status) bagFillStatus.text = status;
            }
        }
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
