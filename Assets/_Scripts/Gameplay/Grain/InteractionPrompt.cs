using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>Shared screen-space interaction presentation. Keeps existing scene references.</summary>
public sealed class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private TMP_Text description;
    [SerializeField] private TMP_Text descriptionTwo;
    [SerializeField] private TMP_Text keycap;
    [SerializeField, Tooltip("Right mouse button indicator for pouring (legacy serialized field name).")]
    private GameObject mouseLeft;

    [SerializeField] private GameObject bagSliderFill;
    [SerializeField] private Image bagFill;
    [SerializeField] private Text bagFillStatus;
    [SerializeField, Min(0f)] private float actionRowSpacing = 8f;
    private sealed class ActionRow
    {
        public RectTransform panel;
        public TMP_Text description, key;
        public GameObject mouse, progress;
        public Image progressFill;
        public Text progressText;
    }
    private readonly List<ActionRow> actionRows = new List<ActionRow>();
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
            data.ProgressText = $"{EconomyNumberFormat.Accounting(bag.Kilograms)} / {EconomyNumberFormat.Accounting(bag.CapacityKg)} kg";
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
        if (descriptionTwo != null)
        {
            descriptionTwo.text = data.SecondaryDescription ?? "";
            descriptionTwo.gameObject.SetActive(!string.IsNullOrEmpty(data.SecondaryDescription));
        }
        bool padPrimary = FarmInput.UsingPad && data.ShowPrimaryButton;
        bool showKey = padPrimary || (data.ShowKey && !data.ShowPrimaryButton);
        string key = padPrimary ? FarmInput.Label(data.PrimaryIsAttack ? "Attack" : "Unload", "") : KeyLabel(data.InteractionKey);
        if (keycap != null && showKey && keycap.text != key) keycap.text = key;
        if (keycap != null && keycap.gameObject.activeSelf != showKey) keycap.gameObject.SetActive(showKey);
        if (mouseLeft != null && mouseLeft.activeSelf != (data.ShowPrimaryButton && !padPrimary)) mouseLeft.SetActive(data.ShowPrimaryButton && !padPrimary);
        if (bagSliderFill != null && bagSliderFill.activeSelf != data.ShowProgress) bagSliderFill.SetActive(data.ShowProgress);
        if (bagFill != null) bagFill.fillAmount = Mathf.Clamp01(data.Progress);
        if (bagFillStatus != null && bagFillStatus.text != data.ProgressText) bagFillStatus.text = data.ProgressText;
        int extraCount = data.AdditionalActions?.Length ?? 0;
        EnsureActionRows(extraCount);
        for (int i = 0; i < actionRows.Count; i++)
        {
            var row = actionRows[i];
            bool active = i < extraCount;
            row.panel.gameObject.SetActive(active);
            if (!active)
            {
                if (row.progress != null) row.progress.SetActive(false);
                continue;
            }
            var action = data.AdditionalActions[i];
            if (row.description != null) row.description.text = action.Action;
            if (row.key != null) { row.key.text = KeyLabel(action.Key); row.key.gameObject.SetActive(true); }
            if (row.mouse != null) row.mouse.SetActive(false);
            if (row.progress != null) row.progress.SetActive(action.ShowProgress);
            if (row.progressFill != null) row.progressFill.fillAmount = Mathf.Clamp01(action.Progress);
            if (row.progressText != null && row.progressText.text != action.ProgressText)
                row.progressText.text = action.ProgressText ?? "";
            CurrentText += "\n[" + KeyLabel(action.Key) + "] " + action.Action;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 position);
        float rowStep = panel.rect.height + actionRowSpacing;
        Vector2 margin = new Vector2(panel.rect.width, panel.rect.height + extraCount * rowStep) * .5f + Vector2.one * 12f;
        Rect bounds = canvasRect.rect;
        position.x = Mathf.Clamp(position.x, bounds.xMin + margin.x, bounds.xMax - margin.x);
        position.y = Mathf.Clamp(position.y, bounds.yMin + margin.y, bounds.yMax - margin.y);
        panel.anchoredPosition = position + Vector2.up * (extraCount * rowStep * .5f);
        for (int i = 0; i < extraCount; i++)
            actionRows[i].panel.anchoredPosition = panel.anchoredPosition - Vector2.up * ((i + 1) * rowStep);
    }

    private static string KeyLabel(KeyCode key) => FarmInput.KeyLabel(key);

    private void EnsureActionRows(int count)
    {
        while (actionRows.Count < count)
        {
            // Clone the authored row to preserve its key background, font, and materials.
            var clone = Instantiate(panel, panel.parent);
            clone.name = "Interaction Action " + (actionRows.Count + 2);
            var secondary = CopyOf(descriptionTwo != null ? descriptionTwo.transform : null, panel, clone);
            if (secondary != null) secondary.gameObject.SetActive(false);
            foreach (var controller in clone.GetComponentsInChildren<InteractionPrompt>(true)) controller.enabled = false;
            var row = new ActionRow { panel = clone };
            var text = CopyOf(description != null ? description.transform : null, panel, clone);
            var key = CopyOf(keycap != null ? keycap.transform : null, panel, clone);
            var mouse = CopyOf(mouseLeft != null ? mouseLeft.transform : null, panel, clone);
            var progress = CopyOf(bagSliderFill != null ? bagSliderFill.transform : null, panel, clone);
            row.description = text != null ? text.GetComponent<TMP_Text>() : null;
            row.key = key != null ? key.GetComponent<TMP_Text>() : null;
            row.mouse = mouse != null ? mouse.gameObject : null;
            row.progress = progress != null ? progress.gameObject : null;
            var fill = CopyOf(bagFill != null ? bagFill.transform : null, panel, clone);
            var status = CopyOf(bagFillStatus != null ? bagFillStatus.transform : null, panel, clone);
            row.progressFill = fill != null ? fill.GetComponent<Image>() : null;
            row.progressText = status != null ? status.GetComponent<Text>() : null;
            actionRows.Add(row);
        }
    }

    private static Transform CopyOf(Transform original, Transform source, Transform clone)
    {
        if (original == null || !original.IsChildOf(source)) return null;
        var indices = new Stack<int>();
        for (var node = original; node != source; node = node.parent) indices.Push(node.GetSiblingIndex());
        var result = clone;
        while (indices.Count > 0) result = result.GetChild(indices.Pop());
        return result;
    }

    public void Hide()
    {
        if (bagSliderFill != null) bagSliderFill.SetActive(false);
        if (bagFill != null) bagFill.fillAmount = 0;
        if (bagFillStatus != null) bagFillStatus.text = "";
        if (panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
        foreach (var row in actionRows)
        {
            if (row.progress != null) row.progress.SetActive(false);
            if (row.panel != null) row.panel.gameObject.SetActive(false);
        }
        CurrentText = "";
        if (descriptionTwo != null)
        {
            descriptionTwo.text = "";
            descriptionTwo.gameObject.SetActive(false);
        }
        if (description != null && description.text.Length > 0) description.text = "";
    }

    private void OnDisable() => Hide();
    private void OnDestroy()
    {
        foreach (var row in actionRows)
            if (row.panel != null)
            {
                if (Application.isPlaying) Destroy(row.panel.gameObject);
                else DestroyImmediate(row.panel.gameObject);
            }
        actionRows.Clear();
    }
}
