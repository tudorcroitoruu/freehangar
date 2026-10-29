using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class UpgradeManager : MonoBehaviour
{
    [SerializeField] private UpgradeShovel upgradeShovelScreen;
    [SerializeField] private UpgradeStamina upgradeStaminaScreen;
    [SerializeField] private UpgradeVehicle upgradeVehicleScreen;

    [SerializeField] private Button closeButton;
    private static UpgradeManager current;
    private static int closedFrame = -1;
    private bool initialized;
    private int openedFrame;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public bool IsOpen { get; private set; }
    public static bool BlocksGameplayInput => (current != null && current.IsOpen) || closedFrame == Time.frameCount;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (upgradeShovelScreen != null) upgradeShovelScreen.gameObject.SetActive(false);
        if (upgradeStaminaScreen != null) upgradeStaminaScreen.gameObject.SetActive(false);
        if (upgradeVehicleScreen != null) upgradeVehicleScreen.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    public bool OpenShovel()
    {
        if (!enabled || upgradeShovelScreen == null || (current != null && current.IsOpen) ||
            UIPopups.BasePopupUI.HasActivePopup ||
            (transform.parent != null && !transform.parent.gameObject.activeInHierarchy)) return false;
        Initialize();
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        openedFrame = Time.frameCount;
        IsOpen = true;
        current = this;
        if (upgradeStaminaScreen != null) upgradeStaminaScreen.gameObject.SetActive(false);
        if (upgradeVehicleScreen != null) upgradeVehicleScreen.gameObject.SetActive(false);
        gameObject.SetActive(true);
        upgradeShovelScreen.gameObject.SetActive(true);
        upgradeShovelScreen.Refresh();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }

    public bool OpenStamina()
    {
        if (!enabled || upgradeStaminaScreen == null || (current != null && current.IsOpen) ||
            UIPopups.BasePopupUI.HasActivePopup ||
            (transform.parent != null && !transform.parent.gameObject.activeInHierarchy)) return false;
        Initialize();
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        openedFrame = Time.frameCount;
        IsOpen = true;
        current = this;
        if (upgradeShovelScreen != null) upgradeShovelScreen.gameObject.SetActive(false);
        if (upgradeVehicleScreen != null) upgradeVehicleScreen.gameObject.SetActive(false);
        gameObject.SetActive(true);
        upgradeStaminaScreen.gameObject.SetActive(true);
        upgradeStaminaScreen.Refresh();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }

    public bool OpenVehicle(VehicleUpgrade vehicle)
    {
        if (!enabled || upgradeVehicleScreen == null || vehicle == null || !vehicle.isActiveAndEnabled || !vehicle.IsConfigured || vehicle.HasCargo ||
            (current != null && current.IsOpen) || UIPopups.BasePopupUI.HasActivePopup ||
            (transform.parent != null && !transform.parent.gameObject.activeInHierarchy)) return false;
        Initialize();
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        openedFrame = Time.frameCount;
        IsOpen = true;
        current = this;
        if (upgradeShovelScreen != null) upgradeShovelScreen.gameObject.SetActive(false);
        if (upgradeStaminaScreen != null) upgradeStaminaScreen.gameObject.SetActive(false);
        upgradeVehicleScreen.SetTarget(vehicle);
        gameObject.SetActive(true);
        upgradeVehicleScreen.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }

    private void OnEnable()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (!Application.isFocused || UIPopups.BasePopupUI.HasActivePopup) return;
        HandleInput(Input.GetKeyDown(KeyCode.Escape), Input.GetKeyDown(KeyCode.E));
    }

    private void HandleInput(bool closePressed, bool upgradePressed)
    {
        // The E press that opens the screen must not also buy its first upgrade.
        if (!IsOpen || Time.frameCount <= openedFrame) return;
        if (closePressed) Close();
        else if (upgradePressed && upgradeShovelScreen != null && upgradeShovelScreen.isActiveAndEnabled)
            upgradeShovelScreen.TryUpgrade();
        else if (upgradePressed && upgradeStaminaScreen != null && upgradeStaminaScreen.isActiveAndEnabled)
            upgradeStaminaScreen.TryUpgrade();
        else if (upgradePressed && upgradeVehicleScreen != null && upgradeVehicleScreen.isActiveAndEnabled)
            upgradeVehicleScreen.TryUpgrade();
    }

    public void Close()
    {
        if (!IsOpen) return;
        gameObject.SetActive(false);
        ReleaseInput();
    }

    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        ReleaseInput();
    }

    private void ReleaseInput()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (current == this) current = null;
        // Also consume Escape for controllers that update later in this frame.
        closedFrame = Time.frameCount;
        bool canRestore = Application.isFocused && !UIPopups.BasePopupUI.HasActivePopup;
        Cursor.lockState = canRestore ? previousCursorLock : CursorLockMode.None;
        Cursor.visible = canRestore ? previousCursorVisible : true;
    }
}
