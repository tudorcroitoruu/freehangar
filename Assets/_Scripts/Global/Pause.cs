using System;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-300)]
public class Pause : MonoBehaviour
{
    [SerializeField] private GameObject pauseScreen;
    [SerializeField] private GameObject mainScreen;
    [SerializeField] private GameObject settingsScreen;
    [SerializeField] private GameObject leaveGamePopup;
    
    [SerializeField] private Button confirmLeaveGameButton;
    

    [Space]
    public bool paused;

    public static Pause Instance;
    public static Action PauseOnAction;
    public static Action PauseOffAction;

    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private void Awake()
    {
        if (!Instance) Instance = this;
        paused = false;
        SetActiveScreen(null);
        if (pauseScreen) pauseScreen.SetActive(false);
    }

    private void OnEnable()
    {
        if (confirmLeaveGameButton) confirmLeaveGameButton.onClick.AddListener(QuitGame);
    }

    private void OnDisable()
    {
        if (confirmLeaveGameButton) confirmLeaveGameButton.onClick.RemoveListener(QuitGame);
        if (paused) Unpause();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (exitToMenu || restarting || GameSettingsManager.IsRebindingInputCaptured || LoadingScreen.isLoadingOn ||
            !Input.GetKeyDown(KeyCode.Escape)) return;
        if (!paused) PauseOn();
        else if (IsSubScreenOpen()) ShowMainScreen();
        else Unpause();
    }

    private bool IsSubScreenOpen() =>
        (settingsScreen && settingsScreen.activeSelf) ||
        (leaveGamePopup && leaveGamePopup.activeSelf);

    private void LateUpdate()
    {
        if (!paused) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void SetActiveScreen(GameObject screen)
    {
        if (mainScreen) mainScreen.SetActive(mainScreen == screen);
        if (settingsScreen) settingsScreen.SetActive(settingsScreen == screen);
        if (leaveGamePopup) leaveGamePopup.SetActive(leaveGamePopup == screen);
    }

    private void OpenScreen(GameObject screen)
    {
        if (!screen || exitToMenu || restarting) return;
        if (!paused) PauseOn();
        if (paused) SetActiveScreen(screen);
    }

    public void ShowMainScreen() => OpenScreen(mainScreen);
    public void ShowSettingsScreen() => OpenScreen(settingsScreen);
    public void ShowLeaveGamePopup() => OpenScreen(leaveGamePopup);

    public void QuitGame()
    {
        Application.Quit();
    }

    public void PauseOn()
    {
        if (paused || !pauseScreen || !mainScreen || exitToMenu || restarting) return;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        paused = true;
        // Select before enabling the root so stale subpanels do not reopen.
        SetActiveScreen(mainScreen);
        pauseScreen.SetActive(true);
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
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        // A live player's ResumeInput can now restore its normal locked gameplay cursor.
        PauseOffAction?.Invoke();
    }

    private bool exitToMenu;

    private bool restarting;
}
