using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared screen-space interaction presentation. Keeps existing scene references.</summary>
public sealed class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text keycap;
    [SerializeField, Tooltip("Right mouse button indicator for pouring (legacy serialized field name).")]
    private GameObject mouseLeft;

    [SerializeField] private GameObject bagSliderFill;
    [SerializeField] private Image bagFill;
    [SerializeField] private Text bagFillStatus;
    public string CurrentText { get; private set; } = "";
    public bool IsVisible => panel != null && panel.gameObject.activeSelf;

    public void Show(Camera camera, Vector3 worldPosition, string action, bool showKey = true, GrainContainer bag = null)
    {
        if (bag != null && (bag.IsClosing || bag.IsCarried)) { Hide(); return; }
        bool showFill = bag != null && !bag.IsFull;
        var player = camera != null ? camera.GetComponentInParent<PlayerController>() : null;
        bool canPour = bag != null && player != null && player.IsShovelEquipped && player.Shovel.CanPourInto(bag);
        var data = new InteractionPromptData(worldPosition, canPour ? "Fill" : action, showKey && !canPour);
        data.ShowPrimaryButton = canPour;
        data.ShowProgress = showFill;
        if (bag != null)
        {
            data.Progress = bag.CapacityKg > 0f ? Mathf.Clamp01(bag.Kilograms / bag.CapacityKg) : 0f;
            data.ProgressText = $"{bag.Kilograms:0.##} / {bag.CapacityKg:0.##} kg";
        }
        Show(camera, data);
    }

    public void Show(Camera camera, InteractionPromptData data)
    {
        if (data.RequiresFreeHands || Time.timeScale <= 0f || UIPopups.BasePopupUI.HasActivePopup) { Hide(); return; }
        if (panel == null || canvasRect == null || camera == null || (string.IsNullOrEmpty(data.Action) && !data.ShowProgress)) { Hide(); return; }
        Vector3 screen = camera.WorldToScreenPoint(data.Position);
        if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height) { Hide(); return; }
        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        CurrentText = data.Action;
        if (description != null && description.text != data.Action) description.text = data.Action;
        bool showKey = data.ShowKey && !data.ShowPrimaryButton;
        if (keycap != null && showKey && keycap.text != "E") keycap.text = "E";
        if (keycap != null && keycap.gameObject.activeSelf != showKey) keycap.gameObject.SetActive(showKey);
        if (mouseLeft != null && mouseLeft.activeSelf != data.ShowPrimaryButton) mouseLeft.SetActive(data.ShowPrimaryButton);
        if (bagSliderFill != null && bagSliderFill.activeSelf != data.ShowProgress) bagSliderFill.SetActive(data.ShowProgress);
        if (bagFill != null) bagFill.fillAmount = Mathf.Clamp01(data.Progress);
        if (bagFillStatus != null && bagFillStatus.text != data.ProgressText) bagFillStatus.text = data.ProgressText;
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
        if (description != null && description.text.Length > 0) description.text = "";
    }
}
