using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class UpgradeManager : MonoBehaviour
{
    [SerializeField] private UpgradeShovel upgradeShovelScreen;
    [SerializeField] private UpgradeShovel axeScreen, hammerScreen;
    [SerializeField] private UpgradeStamina upgradeStaminaScreen;
    [SerializeField] private UpgradeVehicle upgradeVehicleScreen;
    
    [SerializeField] private ParticleSystem upgradeShovelParticles;

    [SerializeField] private Button closeButton;
    private static UpgradeManager current;
    private static int closedFrame = -1;
    private bool initialized;
    private int openedFrame;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private bool cursorCaptured;

    public bool IsOpen { get; private set; }
    public static bool IsAnyOpen => current != null && current.IsOpen;
    public static bool BlocksGameplayInput => (current != null && current.IsOpen) || closedFrame == Time.frameCount || SellingScreen.BlocksGameplayInput;

    private void Awake() => Initialize();

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (axeScreen) axeScreen.gameObject.SetActive(false);
        if (hammerScreen) hammerScreen.gameObject.SetActive(false);
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
        openedFrame = Time.frameCount;
        IsOpen = true;
        current = this;
        if (upgradeStaminaScreen != null) upgradeStaminaScreen.gameObject.SetActive(false);
        if (upgradeVehicleScreen != null) upgradeVehicleScreen.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        gameObject.SetActive(true);
        upgradeShovelScreen.gameObject.SetActive(true);
        upgradeShovelScreen.Refresh();
        AudioManager.Instance?.PlayConfirm();
        return true;
    }

    public bool OpenCombatTool(PlayerCombatTools.Tool tool)
    {
        var screen = tool == PlayerCombatTools.Tool.Axe ? axeScreen : hammerScreen;
        if (!screen || !CombatToolUpgrades.Found(SaveSystem.Data, tool) || !enabled ||
            (current != null && current.IsOpen) || UIPopups.BasePopupUI.HasActivePopup ||
            (transform.parent && !transform.parent.gameObject.activeInHierarchy)) return false;
        Initialize();
        openedFrame = Time.frameCount;
        IsOpen = true;
        current = this;
        if (closeButton) closeButton.gameObject.SetActive(true);
        gameObject.SetActive(true);
        screen.gameObject.SetActive(true);
        AudioManager.Instance?.PlayConfirm();
        return true;
    }

    public bool OpenStamina()
    {
        if (!enabled || upgradeStaminaScreen == null || (current != null && current.IsOpen) ||
            UIPopups.BasePopupUI.HasActivePopup ||
            (transform.parent != null && !transform.parent.gameObject.activeInHierarchy)) return false;
        Initialize();
        openedFrame = Time.frameCount;
        IsOpen = true;
        current = this;
        if (upgradeShovelScreen != null) upgradeShovelScreen.gameObject.SetActive(false);
        if (upgradeVehicleScreen != null) upgradeVehicleScreen.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        gameObject.SetActive(true);
        upgradeStaminaScreen.gameObject.SetActive(true);
        upgradeStaminaScreen.Refresh();
        AudioManager.Instance?.PlayConfirm();
        return true;
    }

    public bool OpenVehicle(VehicleUpgrade vehicle)
    {
        if (!enabled || upgradeVehicleScreen == null || vehicle == null || !vehicle.isActiveAndEnabled || !vehicle.IsConfigured || !vehicle.IsUnlocked || vehicle.HasCargo ||
            (current != null && current.IsOpen) || UIPopups.BasePopupUI.HasActivePopup ||
            (transform.parent != null && !transform.parent.gameObject.activeInHierarchy)) return false;
        Initialize();
        openedFrame = Time.frameCount;
        IsOpen = true;
        current = this;
        if (upgradeShovelScreen != null) upgradeShovelScreen.gameObject.SetActive(false);
        if (upgradeStaminaScreen != null) upgradeStaminaScreen.gameObject.SetActive(false);
        upgradeVehicleScreen.SetTarget(vehicle);
        if (closeButton != null) closeButton.gameObject.SetActive(true);
        gameObject.SetActive(true);
        upgradeVehicleScreen.gameObject.SetActive(true);
        AudioManager.Instance?.PlayConfirm();
        return true;
    }

    private void OnEnable()
    {
        if (upgradeShovelScreen != null) upgradeShovelScreen.UpgradeSucceeded += PlayShovelUpgradeParticles;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorCaptured = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (!Application.isFocused || UIPopups.BasePopupUI.HasActivePopup) return;
        HandleInput(FarmInput.Back, FarmInput.Down("Interact") || Input.GetKeyDown(KeyCode.F));
    }

    private void HandleInput(bool closePressed, bool upgradePressed)
    {
        // The E press that opens the screen must not also buy its first upgrade.
        if (!IsOpen || Time.frameCount <= openedFrame) return;
        if (closePressed) Close();
        else if (upgradePressed && upgradeShovelScreen != null && upgradeShovelScreen.isActiveAndEnabled)
            upgradeShovelScreen.TryUpgrade();
        else if (upgradePressed && axeScreen && axeScreen.isActiveAndEnabled) axeScreen.Upgrade();
        else if (upgradePressed && hammerScreen && hammerScreen.isActiveAndEnabled) hammerScreen.Upgrade();
        else if (upgradePressed && upgradeStaminaScreen != null && upgradeStaminaScreen.isActiveAndEnabled)
            upgradeStaminaScreen.TryUpgrade();
        else if (upgradePressed && upgradeVehicleScreen != null && upgradeVehicleScreen.isActiveAndEnabled)
            upgradeVehicleScreen.TryUpgrade();
    }

    public void Close()
    {
        if (IsOpen) AudioManager.Instance?.PlayCancel();
        foreach (Transform child in transform)
            if (child.gameObject.activeSelf) child.gameObject.SetActive(false);
        gameObject.SetActive(false);
        ReleaseInput();
    }

    private void OnDisable()
    {
        if (upgradeShovelScreen != null) upgradeShovelScreen.UpgradeSucceeded -= PlayShovelUpgradeParticles;
        if (cursorCaptured)
        {
            cursorCaptured = false;
            bool restore = Application.isFocused && !UIPopups.BasePopupUI.HasActivePopup;
            Cursor.lockState = restore ? previousCursorLock : CursorLockMode.None;
            Cursor.visible = restore ? previousCursorVisible : true;
        }
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        ReleaseInput();
    }

    private void PlayShovelUpgradeParticles()
    {
        if (upgradeShovelParticles == null) return;
        upgradeShovelParticles.gameObject.SetActive(true);
        upgradeShovelParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        upgradeShovelParticles.Play(true);
    }

    private void ReleaseInput()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (current == this) current = null;
        // Also consume Escape for controllers that update later in this frame.
        closedFrame = Time.frameCount;
    }
}
