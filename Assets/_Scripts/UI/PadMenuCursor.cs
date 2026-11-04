using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Sends real pointer events through the existing UI module, including drag,
// scroll and pointer-only buttons. The physical mouse can take over at any time.
[DefaultExecutionOrder(-1200)]
public sealed class PadMenuCursor : MonoBehaviour
{
    private VirtualMouseInput pointer;
    private Canvas canvas;
    private RectTransform tip;
    private Text legend;
    private InputAction move, click, scroll;
    private EventSystem navigationOwner;
    private bool previousNavigation;
    private bool previousCursorVisibility;
    private bool releasedCursorLock;
    internal static int DropdownClosedFrame { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        DropdownClosedFrame = -1;
        var root = new GameObject("Gamepad menu controls");
        DontDestroyOnLoad(root);
        root.AddComponent<PadMenuCursor>();
    }

    private void Awake()
    {
        var surface = new GameObject("Controller pointer", typeof(RectTransform));
        surface.SetActive(false);
        surface.transform.SetParent(transform, false);
        canvas = surface.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var cursor = new GameObject("Cursor", typeof(RectTransform), typeof(Text));
        cursor.transform.SetParent(surface.transform, false);
        var text = cursor.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "+";
        text.fontSize = 30;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        var edge = cursor.AddComponent<UnityEngine.UI.Outline>();
        edge.effectColor = Color.black;
        tip = text.rectTransform;
        tip.anchorMin = tip.anchorMax = Vector2.zero;
        tip.sizeDelta = new Vector2(42f, 42f);

        var help = new GameObject("Controller menu hints", typeof(RectTransform), typeof(Text));
        help.transform.SetParent(surface.transform, false);
        legend = help.GetComponent<Text>();
        legend.font = text.font;
        legend.fontSize = 17;
        legend.alignment = TextAnchor.LowerCenter;
        legend.raycastTarget = false;
        legend.rectTransform.anchorMin = Vector2.zero;
        legend.rectTransform.anchorMax = Vector2.right;
        legend.rectTransform.pivot = new Vector2(.5f, 0);
        legend.rectTransform.sizeDelta = new Vector2(0, 54);
        legend.rectTransform.anchoredPosition = new Vector2(0, 8);
        help.AddComponent<UnityEngine.UI.Outline>().effectColor = Color.black;

        move = new InputAction("Menu pointer", InputActionType.Value, "<Gamepad>/leftStick");
        move.AddBinding("<Gamepad>/dpad");
        click = new InputAction("Menu click", InputActionType.Button, "<Gamepad>/buttonSouth");
        scroll = new InputAction("Menu scroll", InputActionType.Value, "<Gamepad>/rightStick");
        pointer = surface.AddComponent<VirtualMouseInput>();
        pointer.cursorGraphic = text;
        pointer.cursorTransform = tip;
        pointer.stickAction = new InputActionProperty(move);
        pointer.leftButtonAction = new InputActionProperty(click);
        pointer.scrollWheelAction = new InputActionProperty(scroll);
        pointer.scrollSpeed = 12f;
    }

    private void Update()
    {
        bool menu = MenuManager.IsMenuOpen || (Pause.Instance && Pause.Instance.paused) ||
            UpgradeManager.BlocksGameplayInput || UIPopups.BasePopupUI.HasActivePopup;
        bool show = Application.isFocused && FarmInput.UsingPad && menu && !LoadingScreen.IsLoading;
        if (!show)
        {
            ReleasePointer();
            return;
        }
        if (navigationOwner != EventSystem.current)
        {
            RestoreNavigation();
            navigationOwner = EventSystem.current;
            if (navigationOwner)
            {
                previousNavigation = navigationOwner.sendNavigationEvents;
                navigationOwner.sendNavigationEvents = false;
            }
        }
        if (!pointer.gameObject.activeSelf)
        {
            previousCursorVisibility = Cursor.visible;
            releasedCursorLock = Cursor.lockState == CursorLockMode.Locked;
            if (releasedCursorLock) Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            var pad = Gamepad.current;
            legend.text = "Left stick / D-pad: point     " + pad.buttonSouth.shortDisplayName + ": select     Right stick: scroll     " +
                pad.buttonEast.shortDisplayName + ": back";
            tip.anchoredPosition = new Vector2(Screen.width, Screen.height) * .5f;
            pointer.cursorSpeed = Mathf.Max(640f, Screen.height * 1.15f);
            pointer.gameObject.SetActive(true);
        }
        if (Gamepad.current.buttonEast.wasPressedThisFrame && navigationOwner &&
            navigationOwner.currentSelectedGameObject)
        {
            var dropdown = navigationOwner.currentSelectedGameObject.GetComponentInParent<TMPro.TMP_Dropdown>();
            if (dropdown && dropdown.IsExpanded)
            {
                dropdown.Hide();
                DropdownClosedFrame = Time.frameCount;
            }
            var legacy = navigationOwner.currentSelectedGameObject?.GetComponentInParent<Dropdown>();
            if (legacy && legacy.transform.Find("Dropdown List"))
            {
                legacy.Hide();
                DropdownClosedFrame = Time.frameCount;
            }
        }
    }

    private void RestoreNavigation()
    {
        if (navigationOwner) navigationOwner.sendNavigationEvents = previousNavigation;
        navigationOwner = null;
    }

    private void ReleasePointer()
    {
        if (pointer && pointer.gameObject.activeSelf)
        {
            pointer.gameObject.SetActive(false);
            if (releasedCursorLock && !UIPopups.BasePopupUI.HasActivePopup)
                Cursor.lockState = CursorLockMode.Locked;
            if (Cursor.lockState == CursorLockMode.None)
                Cursor.visible = previousCursorVisibility || UIPopups.BasePopupUI.HasActivePopup;
            releasedCursorLock = false;
        }
        RestoreNavigation();
    }

    private void OnDisable() => ReleasePointer();
    private void OnDestroy()
    {
        move?.Dispose();
        click?.Dispose();
        scroll?.Dispose();
    }
}
