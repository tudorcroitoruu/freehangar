using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-300)]
public class Pause : MonoBehaviour
{
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private GameObject mainScreen;
    [SerializeField] private SelectedUI[] mainScreenButtons;
    [SerializeField] private GameObject settingsScreen;
    [SerializeField] private GameObject leaveGamePopup;
    
    [SerializeField] private Button confirmLeaveGameButton;
    

    [Space]
    public bool paused;

    public Transform NavigationRoot => (leaveGamePopup && leaveGamePopup.activeInHierarchy ? leaveGamePopup : settingsScreen && settingsScreen.activeInHierarchy ? settingsScreen : mainScreen)?.transform;
    public static Pause Instance;
    public static Action PauseOnAction;
    public static Action PauseOffAction;

    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private float previousTimeScale = 1f;
    private bool previousAudioPaused;
    private int selectedMainIndex = -1, mainOpenedFrame, lastMainDirection;
    private float nextMainMoveAt;

    private void Awake()
    {
        if (!Instance) Instance = this;
        paused = false;
        SetActiveScreen(null);
        if (pauseScreen) pauseScreen.SetActive(false);
    }

    private void OnEnable()
    {
        if (confirmLeaveGameButton) confirmLeaveGameButton.onClick.AddListener(GoToMenu);
    }

    private void OnDisable()
    {
        ClearMainSelection();
        if (confirmLeaveGameButton) confirmLeaveGameButton.onClick.RemoveListener(GoToMenu);
        if (paused) Unpause();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (MenuManager.IsMenuOpen || exitToMenu || restarting || GameSettingsManager.IsRebindingInputCaptured || LoadingScreen.IsLoading ||
            !(FarmInput.Down("Pause") || (paused && FarmInput.Back))) return;
        if (!paused) PauseOn();
        else if (IsSubScreenOpen()) ShowMainScreen();
        else Unpause();
    }

    private bool IsSubScreenOpen() =>
        (settingsScreen && settingsScreen.activeSelf) ||
        (leaveGamePopup && leaveGamePopup.activeSelf);

    private void SetActiveScreen(GameObject screen)
    {
        ClearMainSelection();
        if (mainScreen) mainScreen.SetActive(mainScreen == screen);
        if (settingsScreen) settingsScreen.SetActive(settingsScreen == screen);
        if (leaveGamePopup) leaveGamePopup.SetActive(leaveGamePopup == screen);
        if (screen == mainScreen && mainScreen && mainScreen.activeInHierarchy) InitializeMainSelection();
    }

    private void InitializeMainSelection()
    {
        ClearMainSelection();
        if (mainScreenButtons == null || mainScreenButtons.Length == 0)
            mainScreenButtons = mainScreen.GetComponentsInChildren<SelectedUI>(true);
        foreach (var item in mainScreenButtons)
            if (item != null)
            {
                item.PointerEntered += OnMainPointerEntered;
                var button = item.button != null ? item.button : item.GetComponent<Button>();
                if (button != null && button.transition == Selectable.Transition.ColorTint)
                {
                    if (button.targetGraphic != null)
                        button.targetGraphic.CrossFadeColor(button.colors.normalColor * button.colors.colorMultiplier, 0f, true, true);
                    button.transition = Selectable.Transition.None;
                }
            }
        mainOpenedFrame = Time.frameCount;
        lastMainDirection = 0;
        for (int i = 0; i < mainScreenButtons.Length; i++)
            if (SelectMainButton(i)) break;
    }

    private bool SelectMainButton(int index)
    {
        var selected = mainScreenButtons[index];
        var button = selected != null ? selected.button != null ? selected.button : selected.GetComponent<Button>() : null;
        if (selected == null || !selected.isActiveAndEnabled || button == null || !button.IsActive() || !button.IsInteractable()) return false;
        foreach (var item in mainScreenButtons)
            if (item != null)
            {
                if (item != selected)
                {
                    item.OnPointerExit(null);
                    item.OnDeselect(null);
                }
                item.Deselect();
            }
        selectedMainIndex = index;
        selected.Select();
        EventSystem.current?.SetSelectedGameObject(button.gameObject);
        return true;
    }

    private void OnMainPointerEntered(SelectedUI item)
    {
        if (!paused || IsSubScreenOpen() || mainScreenButtons == null || exitToMenu || restarting || LoadingScreen.IsLoading) return;
        int index = Array.IndexOf(mainScreenButtons, item);
        if (index >= 0) SelectMainButton(index);
    }

    public bool HandleMainScreenInput(Gamepad pad)
    {
        if (!paused || IsSubScreenOpen() || !mainScreen || !mainScreen.activeInHierarchy ||
            mainScreenButtons == null || mainScreenButtons.Length == 0) return false;
        if (pad == null || Time.frameCount <= mainOpenedFrame || exitToMenu || restarting ||
            LoadingScreen.IsLoading || GameSettingsManager.IsRebindingInputCaptured) return true;
        float vertical = pad.dpad.ReadValue().y;
        if (Mathf.Abs(vertical) < .5f) vertical = pad.leftStick.ReadValue().y;
        int direction = Mathf.Abs(vertical) >= .5f ? vertical > 0f ? -1 : 1 : 0;
        if (direction != 0 && (direction != lastMainDirection || Time.unscaledTime >= nextMainMoveAt))
        {
            nextMainMoveAt = Time.unscaledTime + (direction != lastMainDirection ? .35f : .12f);
            for (int step = 1; step <= mainScreenButtons.Length; step++)
            {
                int index = (selectedMainIndex + direction * step + mainScreenButtons.Length * 2) % mainScreenButtons.Length;
                if (SelectMainButton(index)) break;
            }
        }
        lastMainDirection = direction;
        if (pad.buttonSouth.wasPressedThisFrame && selectedMainIndex >= 0)
        {
            var selected = mainScreenButtons[selectedMainIndex];
            var button = selected != null ? selected.button != null ? selected.button : selected.GetComponent<Button>() : null;
            if (selected != null && selected.isActiveAndEnabled && button != null && button.IsActive() && button.IsInteractable())
                button.onClick.Invoke();
        }
        return true;
    }

    private void ClearMainSelection()
    {
        if (mainScreenButtons != null)
            foreach (var item in mainScreenButtons)
                if (item != null)
                {
                    item.PointerEntered -= OnMainPointerEntered;
                    item.OnPointerExit(null);
                    item.OnDeselect(null);
                    item.Deselect();
                }
        selectedMainIndex = -1;
    }

    private void OpenScreen(GameObject screen)
    {
        if (!screen || exitToMenu || restarting) return;
        if (!paused) PauseOn();
        if (paused && !screen.activeSelf)
        {
            if (screen == mainScreen) AudioManager.Instance?.PlayCancel();
            else AudioManager.Instance?.PlayConfirm();
            SetActiveScreen(screen);
        }
    }

    public void ShowMainScreen() => OpenScreen(mainScreen);
    public void ShowSettingsScreen() => OpenScreen(settingsScreen);
    public void ShowLeaveGamePopup() => OpenScreen(leaveGamePopup);

    public void QuitGame()
    {
        Application.Quit();
    }

    public void GoToMenu()
    {
        if (LoadingScreen.isLoadingOn || !LoadingScreen.CanLoadScene(SceneName.Menu.ToString())) return;
        var loading = LoadingScreen.Instance != null ? LoadingScreen.Instance :
            FindFirstObjectByType<LoadingScreen>(FindObjectsInactive.Include);
        if (loading == null) { Debug.LogError("A persistent LoadingScreen is required to return to the menu.", this); return; }
        if (SaveSystem.savesLoaded) SaveSystem.SaveData();
        loading.LoadScene(SceneName.Menu.ToString());
    }

    public void PauseOn()
    {
        // Sale/upgrade screens own Escape, including the frame in which they close.
        if (paused || UpgradeManager.BlocksGameplayInput || !pauseScreen || !mainScreen || exitToMenu || restarting) return;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        previousTimeScale = Time.timeScale;
        previousAudioPaused = AudioListener.pause;
        AudioListener.pause = true;
        Time.timeScale = 0f;
        paused = true;
        AudioManager.Instance?.PlayConfirm();
        // Select before enabling the root so stale subpanels do not reopen.
        SetActiveScreen(mainScreen);
        pauseScreen.SetActive(true);
        InitializeMainSelection();
        PauseOnAction?.Invoke();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Unpause()
    {
        if (exitToMenu) return;
        bool wasPaused = paused;
        paused = false;
        SetActiveScreen(null);
        if (pauseScreen) pauseScreen.SetActive(false);
        if (!wasPaused) return;
        AudioListener.pause = previousAudioPaused;
        AudioManager.Instance?.PlayCancel();
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        // A live player's ResumeInput can now restore its normal locked gameplay cursor.
        PauseOffAction?.Invoke();
    }

    private bool exitToMenu;

    private bool restarting;
}
