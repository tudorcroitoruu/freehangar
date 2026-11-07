using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shared focus navigation for menu, pause and popup UI. Back remains owned by each screen.
[DefaultExecutionOrder(-1200)]
public sealed class PadMenuCursor : MonoBehaviour
{
    internal static int DropdownClosedFrame { get; private set; } = -1;
    private Image focusBackground;
    private EventSystem owner;
    private bool previousNavigation;
    private bool pointerOwnsFocus;
    private Transform scope;
    private Vector2 lastDirection;
    private float repeatAt;
    private readonly List<Selectable> controls = new List<Selectable>();
    private Selectable[] buffer = new Selectable[128];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        DropdownClosedFrame = -1;
        var root = new GameObject("Gamepad UI navigation manager");
        DontDestroyOnLoad(root);
        root.AddComponent<PadMenuCursor>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        bool pointerInput = mouse != null && mouse.layout != "VirtualMouse" && mouse.name != "VirtualMouse" &&
            (mouse.delta.ReadValue().sqrMagnitude > .01f ||
            mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
            mouse.scroll.ReadValue().sqrMagnitude > .01f);
        var events = EventSystem.current;
        if (pointerInput) pointerOwnsFocus = true;
        if (pointerInput && events && events.currentSelectedGameObject)
        {
            var current = events.currentSelectedGameObject;
            // Moving the mouse must not leave a second navigation highlight behind.
            // Text fields retain focus so moving the pointer does not interrupt typing.
            if (!current.GetComponent<InputField>() && !current.GetComponent<TMPro.TMP_InputField>())
                events.SetSelectedGameObject(null);
        }
        var root = Pause.Instance && Pause.Instance.paused ? Pause.Instance.NavigationRoot :
            MenuManager.IsMenuOpen ? MenuManager.Instance.NavigationRoot :
            UIPopups.BasePopupUI.CurrentPopup ? UIPopups.BasePopupUI.CurrentPopup.transform : null;
        UpdateFocusBackground(root);
        Transform navigationRoot = root;
        if (root)
            foreach (var toggle in root.GetComponentsInChildren<Toggle>())
                if (toggle.transform.parent && toggle.transform.parent.parent && toggle.transform.parent.parent.name == "Dropdown List")
                    navigationRoot = toggle.transform.parent.parent;
        // SelectedUI owns its authored selection visuals; other buttons use material focus.
        if (buffer.Length < Selectable.allSelectableCount) buffer = new Selectable[Selectable.allSelectableCount + 32];
        int count = Selectable.AllSelectablesNoAlloc(buffer);
        controls.Clear();
        for (int i = 0; i < count; i++)
        {
            var item = buffer[i];
            if (item is Button && item.GetComponentInParent<SelectedUI>() == null &&
                !item.TryGetComponent<ButtonMaterialFocus>(out _))
                item.gameObject.AddComponent<ButtonMaterialFocus>();
            if (root && item.transform.IsChildOf(navigationRoot) && item.IsActive() && item.IsInteractable()) controls.Add(item);
        }
        if (!root || !Application.isFocused || LoadingScreen.IsLoading || GameSettingsManager.IsRebindingInputCaptured)
        { Restore(); scope = null; return; }
        if (!events) return;
        if (owner != events)
        {
            Restore(); owner = events; previousNavigation = owner.sendNavigationEvents;
        }
        // UI controls must not depend on the gameplay action map's last-device state.
        var pad = FarmInput.ActivePad ?? Gamepad.current;
        foreach (var connected in Gamepad.all)
            if (HasNavigationInput(connected)) { pad = connected; break; }
        bool padInput = pad != null && HasNavigationInput(pad);
        if (padInput && !pointerInput) pointerOwnsFocus = false;
        owner.sendNavigationEvents = !padInput && !FarmInput.UsingPad && previousNavigation;
        bool handledSelection = Pause.Instance && Pause.Instance.paused
            ? Pause.Instance.HandleMainScreenInput(pad)
            : MenuManager.IsMenuOpen && MenuManager.Instance.HandleConfirmationInput(pad);
        var settingsNavigation = root.GetComponentInChildren<GameSettingsManager>();
        if (!handledSelection && settingsNavigation != null)
            handledSelection = settingsNavigation.HandleSettingsInput(pad);
        if (handledSelection)
        {
            owner.sendNavigationEvents = false;
            scope = root;
            lastDirection = Vector2.zero;
            return;
        }
        var selected = owner.currentSelectedGameObject ? owner.currentSelectedGameObject.GetComponent<Selectable>() : null;
        if (scope != root || !selected || !controls.Contains(selected))
        {
            scope = root;
            // Mouse hover owns its own visuals. Only controller navigation needs
            // an automatically selected first control.
            if (pointerOwnsFocus || (!padInput && !FarmInput.UsingPad))
            {
                lastDirection = Vector2.zero;
                return;
            }
            // Hierarchy order is stable; Unity's global selectable array is not.
            foreach (var first in navigationRoot.GetComponentsInChildren<Selectable>())
                if (controls.Contains(first))
                {
                    owner.SetSelectedGameObject(first.gameObject);
                    break;
                }
            lastDirection = Vector2.zero;
            return; // Do not submit the press which opened this screen.
        }
        if (pad == null || (!padInput && !FarmInput.UsingPad)) return;
        var dropdown = root.GetComponentInChildren<TMPro.TMP_Dropdown>();
        var legacy = root.GetComponentInChildren<Dropdown>();
        if (pad.buttonEast.wasPressedThisFrame && ((dropdown && dropdown.IsExpanded) || (legacy && legacy.transform.Find("Dropdown List"))))
        {
            if (dropdown) dropdown.Hide();
            if (legacy) legacy.Hide();
            DropdownClosedFrame = Time.frameCount;
            return;
        }
        var settings = root.GetComponentInChildren<GameSettingsManager>();
        bool expanded = (dropdown && dropdown.IsExpanded) || (legacy && legacy.transform.Find("Dropdown List"));
        if (settings && !expanded && (pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame))
        {
            settings.CycleSection(pad.rightShoulder.wasPressedThisFrame ? 1 : -1);
            return;
        }
        Vector2 input = pad.dpad.ReadValue();
        if (input.sqrMagnitude < .25f) input = pad.leftStick.ReadValue();
        Vector2 direction = input.sqrMagnitude < .25f ? Vector2.zero :
            Mathf.Abs(input.x) > Mathf.Abs(input.y) ? new Vector2(Mathf.Sign(input.x), 0) : new Vector2(0, Mathf.Sign(input.y));
        if (direction != Vector2.zero && (direction != lastDirection || Time.unscaledTime >= repeatAt))
        {
            repeatAt = Time.unscaledTime + (direction != lastDirection ? .35f : .12f);
            var data = new AxisEventData(owner) { moveVector = direction, moveDir = direction.x > 0 ? MoveDirection.Right : direction.x < 0 ? MoveDirection.Left : direction.y > 0 ? MoveDirection.Up : MoveDirection.Down };
            if (selected is Slider || selected is Scrollbar) ExecuteEvents.Execute(selected.gameObject, data, ExecuteEvents.moveHandler);
            else
            {
                Selectable best = null; float score = float.PositiveInfinity;
                foreach (var candidate in controls)
                {
                    Vector2 delta = candidate.transform.position - selected.transform.position;
                    float forward = Vector2.Dot(delta, direction);
                    if (forward <= .01f) continue;
                    float value = delta.sqrMagnitude / forward;
                    if (value < score) { score = value; best = candidate; }
                }
                if (best) owner.SetSelectedGameObject(best.gameObject);
            }
        }
        lastDirection = direction;
        if (pad.buttonSouth.wasPressedThisFrame)
            ExecuteEvents.Execute(owner.currentSelectedGameObject, new BaseEventData(owner), ExecuteEvents.submitHandler);
    }

    private static bool HasNavigationInput(Gamepad pad) =>
        pad.dpad.ReadValue().sqrMagnitude >= .25f || pad.leftStick.ReadValue().sqrMagnitude >= .25f ||
        pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
        pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;
    private void UpdateFocusBackground(Transform root)
    {
        var settings = root ? root.GetComponentInChildren<GameSettingsManager>() : null;
        var current = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        bool show = settings && current && current.transform.IsChildOf(root) && current.activeInHierarchy;
        // Tabs already own their textured selected state.
        if (show && current.GetComponent<SelectedUI>()) show = false;
        if (!show) { if (focusBackground) focusBackground.gameObject.SetActive(false); return; }
        var tab = settings.FirstSectionTab ? settings.FirstSectionTab.GetComponent<SelectedUI>() : null;
        var source = tab ? tab.SelectionBackground : null;
        if (!source) return;
        if (!focusBackground)
        {
            var go = new GameObject("Controller selected background", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            focusBackground = go.GetComponent<Image>();
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            focusBackground.raycastTarget = false;
        }
        // Use the tab's authored texture/material, without copying tab scripts or events.
        focusBackground.sprite = source.sprite;
        focusBackground.material = source.material;
        focusBackground.color = source.color;
        focusBackground.type = source.type;
        // Each settings row is a direct child of its page's vertical layout.
        // Walk past sliders, arrow buttons and value containers to highlight the whole row.
        RectTransform target = null;
        for (Transform ancestor = current.transform; ancestor && ancestor != root; ancestor = ancestor.parent)
        {
            if (ancestor.parent && ancestor.parent.GetComponent<VerticalLayoutGroup>())
            {
                target = ancestor as RectTransform;
                break;
            }
        }
        if (!target) { focusBackground.gameObject.SetActive(false); return; }
        var rect = focusBackground.rectTransform;
        if (rect.parent != target) rect.SetParent(target, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        focusBackground.gameObject.SetActive(true);
    }
    private void Restore() { if (owner) owner.sendNavigationEvents = previousNavigation; owner = null; }
    private void OnDisable() => Restore();
}
