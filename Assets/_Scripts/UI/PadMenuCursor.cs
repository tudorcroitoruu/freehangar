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
    private EventSystem owner;
    private bool previousNavigation;
    private bool pointerOwnsFocus;
    private Transform scope;
    private Vector2 lastDirection;
    private float repeatAt;
    private readonly List<Selectable> controls = new List<Selectable>();
    private Selectable[] buffer = new Selectable[128];
    private GameObject lastSoundSelection;

    private void LateUpdate()
    {
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == lastSoundSelection) return;
        lastSoundSelection = selected;
        if (selected && selected.activeInHierarchy && !LoadingScreen.IsLoading &&
            selected.TryGetComponent<Selectable>(out var control) && control.IsInteractable())
            AudioManager.Instance?.PlaySelectStandard(0);
    }

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
        var root = UIPopups.BasePopupUI.CurrentPopup ? UIPopups.BasePopupUI.CurrentPopup.transform :
            Pause.Instance && Pause.Instance.paused ? Pause.Instance.NavigationRoot :
            MenuManager.IsMenuOpen ? MenuManager.Instance.NavigationRoot :
            null;
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
        // A newly opened page needs focus even while the connected pad is idle.
        // A real mouse action in this frame still owns pointer navigation.
        if (scope != navigationRoot && pad != null && !pointerInput) pointerOwnsFocus = false;
        if (padInput && !pointerInput) pointerOwnsFocus = false;
        owner.sendNavigationEvents = !padInput && !FarmInput.UsingPad && previousNavigation;
        bool handledSelection = !UIPopups.BasePopupUI.CurrentPopup && (Pause.Instance && Pause.Instance.paused
            ? Pause.Instance.HandleMainScreenInput(pad)
            : MenuManager.IsMenuOpen && MenuManager.Instance.HandleConfirmationInput(pad));
        var settingsNavigation = root.GetComponentInChildren<GameSettingsManager>();
        if (!handledSelection && settingsNavigation != null)
            handledSelection = settingsNavigation.HandleSettingsInput(pad);
        if (handledSelection)
        {
            owner.sendNavigationEvents = false;
            scope = navigationRoot;
            lastDirection = Vector2.zero;
            return;
        }
        var selected = owner.currentSelectedGameObject ? owner.currentSelectedGameObject.GetComponent<Selectable>() : null;
        if (scope != navigationRoot || !selected || !controls.Contains(selected))
        {
            scope = navigationRoot;
            // Mouse hover owns its own visuals. Only controller navigation needs
            // an automatically selected first control.
            if (pointerOwnsFocus || pad == null)
            {
                lastDirection = Vector2.zero;
                return;
            }
            // Hierarchy order is stable; Unity's global selectable array is not.
            SelectFirstControl(owner, navigationRoot);
            lastDirection = Vector2.zero;
            return; // Do not submit the press which opened this screen.
        }
        if (pad == null || (!padInput && !FarmInput.UsingPad)) return;
        var dropdown = root.GetComponentInChildren<TMPro.TMP_Dropdown>();
        var legacy = root.GetComponentInChildren<Dropdown>();
        if (MenuPadInput.CancelPressed(pad) && ((dropdown && dropdown.IsExpanded) || (legacy && legacy.transform.Find("Dropdown List"))))
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
        if (MenuPadInput.ConfirmPressed(pad))
            ExecuteEvents.Execute(owner.currentSelectedGameObject, new BaseEventData(owner), ExecuteEvents.submitHandler);
    }

    private static bool HasNavigationInput(Gamepad pad) =>
        pad.dpad.ReadValue().sqrMagnitude >= .25f || pad.leftStick.ReadValue().sqrMagnitude >= .25f ||
        MenuPadInput.ConfirmPressed(pad) || MenuPadInput.CancelPressed(pad) ||
        pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;

    public static bool SelectFirstControl(EventSystem events, Transform root)
    {
        if (!events || !root) return false;
        foreach (var control in root.GetComponentsInChildren<Selectable>())
        {
            if (!control.IsActive() || !control.IsInteractable()) continue;
            events.SetSelectedGameObject(control.gameObject);
            return true;
        }
        events.SetSelectedGameObject(null);
        return false;
    }
    private void Restore() { if (owner) owner.sendNavigationEvents = previousNavigation; owner = null; }
    private void OnDisable() => Restore();
}
