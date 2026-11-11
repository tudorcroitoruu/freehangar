using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1000)]
public class MenuManager : MonoBehaviour
{
    public Transform NavigationRoot => (quitGamePopup && quitGamePopup.activeInHierarchy ? quitGamePopup : newGamePopup && newGamePopup.activeInHierarchy ? newGamePopup : settingsScreen && settingsScreen.activeInHierarchy ? settingsScreen : menuScreen)?.transform;
    public static MenuManager Instance { get; private set; }
    public static bool IsMenuOpen => Instance != null && Instance.isActiveAndEnabled;
    [Header("Persistent services")]
    [SerializeField, Tooltip("Creates the shared audio/loading services when the Menu scene is played directly.")]
    private GlobalManager servicesPrefab;
    [Header("Screens")]
    [SerializeField] private GameObject continueButton;
    [SerializeField] private GameObject menuScreen;
    [SerializeField] private GameObject settingsScreen;
    [SerializeField] private SelectedUI[] mainButtons;
    [Space, Header("New Game")]
    [SerializeField] private GameObject newGamePopup;
    [SerializeField] private SelectedUI[] newGamePopupButtons;
    [Space, Header("Quit Game")]
    [SerializeField] private GameObject quitGamePopup;
    [SerializeField] private SelectedUI[] quitGamePopupButtons;
    [Header("Transitions")]
    [SerializeField] private string gameplayScene = "Gameplay";
    [SerializeField, Min(0f)] private float minimumLoadingSeconds = 1f;
    private bool transitionRequested;
    private bool mainScreenWasVisible;
    private int newGamePopupOpenedFrame;
    private SelectedUI[] activePopupButtons;
    private int selectedPopupIndex, popupOpenedFrame, lastPopupDirection;
    private float nextPopupMoveAt;

    [Header("Background cameras (in playback order)")]
    [SerializeField] private Camera[] menuCameras = new Camera[3];
    [SerializeField, Min(.1f)] private float cameraHoldSeconds = 6f;
    [SerializeField, Min(0f)] private float fadeSeconds = 1f;

    private readonly List<Camera> cameras = new List<Camera>();
    private readonly List<RenderTexture> cameraTargets = new List<RenderTexture>();
    private readonly List<Camera> suspendedCameras = new List<Camera>();
    private readonly List<AudioListener> suspendedListeners = new List<AudioListener>();
    private readonly List<AudioListener> listeners = new List<AudioListener>();
    private Canvas cameraCanvas;
    private CanvasGroup fade;
    private RawImage blendImage;
    private RenderTexture blendTexture;
    private Coroutine playback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionState()
    {
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
        if (GlobalManager.Instance == null && FindFirstObjectByType<GlobalManager>() == null && servicesPrefab != null)
            Instantiate(servicesPrefab);
        var settingsManager = GameSettingsManager.Instance;
        if (settingsManager == null)
            settingsManager = FindFirstObjectByType<GameSettingsManager>(FindObjectsInactive.Include);
        if (settingsManager != null)
        {
            settingsManager.Init();
            if (SaveSystem.savesLoaded) ResolveLoadingScreen()?.LoadingOff(.35f);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ContinueGame()
    {
        if (LoadingScreen.isLoadingOn || !SaveSystem.HasSavedGame ||
            (newGamePopup != null && newGamePopup.activeSelf)) return;
        BeginGame(false);
    }

    public void NewGame()
    {
        if (transitionRequested || LoadingScreen.isLoadingOn) return;
        if (!SaveSystem.HasSavedGame) { BeginGame(true); return; }
        if (newGamePopup == null)
        {
            Debug.LogError("Assign the New Game confirmation popup before replacing a save.", this);
            return;
        }
        if (newGamePopup.activeSelf) return;
        AudioManager.Instance?.PlayConfirm();
        SetScreenActive(settingsScreen, false);
        SetScreenActive(quitGamePopup, false);
        SetScreenActive(menuScreen, false);
        SetScreenActive(newGamePopup, true);
        newGamePopupOpenedFrame = Time.frameCount;
        InitializePopupSelection(newGamePopup, ref newGamePopupButtons);
    }

    public void ConfirmNewGame()
    {
        if (LoadingScreen.isLoadingOn || newGamePopup == null || !newGamePopup.activeSelf) return;
        BeginGame(true);
    }

    public void ShowMenuScreen()
    {
        if (transitionRequested || LoadingScreen.isLoadingOn) return;
        if ((settingsScreen != null && settingsScreen.activeSelf) || (quitGamePopup != null && quitGamePopup.activeSelf) ||
            (newGamePopup != null && newGamePopup.activeSelf))
            AudioManager.Instance?.PlayCancel();
        ClearPopupSelection();
        SetScreenActive(menuScreen, true);
        SetScreenActive(settingsScreen, false);
        SetScreenActive(quitGamePopup, false);
        SetScreenActive(newGamePopup, false);
        RefreshContinueButtonState();
        if (menuScreen != null) InitializePopupSelection(menuScreen, ref mainButtons);
    }

    public void ShowSettingsScreen()
    {
        if (transitionRequested || LoadingScreen.isLoadingOn) return;
        if (newGamePopup != null && newGamePopup.activeSelf) return;
        if (settingsScreen == null || settingsScreen.activeSelf) return;
        AudioManager.Instance?.PlayConfirm();
        SetScreenActive(menuScreen, false);
        ClearPopupSelection();
        SetScreenActive(quitGamePopup, false);
        SetScreenActive(settingsScreen, true);
    }

    public void ShowQuitGamePopup()
    {
        if (transitionRequested || LoadingScreen.isLoadingOn) return;
        if (newGamePopup != null && newGamePopup.activeSelf) return;
        if (quitGamePopup == null || quitGamePopup.activeSelf) return;
        AudioManager.Instance?.PlayConfirm();
        SetScreenActive(settingsScreen, false);
        SetScreenActive(menuScreen, true);
        SetScreenActive(quitGamePopup, true);
        InitializePopupSelection(quitGamePopup, ref quitGamePopupButtons);
    }

    private void InitializePopupSelection(GameObject popup, ref SelectedUI[] buttons)
    {
        ClearPopupSelection();
        if (buttons == null || buttons.Length == 0) buttons = popup.GetComponentsInChildren<SelectedUI>(true);
        activePopupButtons = buttons;
        foreach (var item in buttons)
            if (item != null) item.PointerEntered += OnPopupPointerEntered;
        popupOpenedFrame = Time.frameCount;
        lastPopupDirection = 0;
        selectedPopupIndex = -1;
        for (int i = 0; i < buttons.Length; i++)
            if (SelectPopupButton(i)) break;
    }

    private void RefreshContinueButtonState()
    {
        if ((mainButtons == null || mainButtons.Length == 0) && menuScreen != null)
            mainButtons = menuScreen.GetComponentsInChildren<SelectedUI>(true);
        var selected = mainButtons != null && mainButtons.Length > 0 ? mainButtons[0] :
            continueButton != null ? continueButton.GetComponent<SelectedUI>() : null;
        var button = selected != null ? selected.button != null ? selected.button : selected.GetComponent<Button>() :
            continueButton != null ? continueButton.GetComponent<Button>() : null;
        bool hasSave = SaveSystem.HasSavedGame;
        SetScreenActive(selected != null ? selected.gameObject : continueButton, true);
        if (button != null) button.interactable = hasSave;
        if (selected != null)
        {
            if (!hasSave)
            {
                selected.OnPointerExit(null);
                selected.OnDeselect(null);
                selected.Deselect();
            }
            selected.enabled = hasSave;
        }
    }

    private bool SelectPopupButton(int index)
    {
        var selected = activePopupButtons[index];
        var button = selected != null ? selected.button != null ? selected.button : selected.GetComponent<Button>() : null;
        if (selected == null || !selected.isActiveAndEnabled || button == null || !button.IsActive() || !button.IsInteractable()) return false;
        foreach (var item in activePopupButtons)
            if (item != null)
            {
                if (item != selected)
                {
                    item.OnPointerExit(null);
                    item.OnDeselect(null);
                }
                item.Deselect();
            }
        selectedPopupIndex = index;
        selected.Select();
        EventSystem.current?.SetSelectedGameObject(button.gameObject);
        return true;
    }

    private void OnPopupPointerEntered(SelectedUI item)
    {
        if (activePopupButtons == null || transitionRequested || LoadingScreen.IsLoading) return;
        int index = System.Array.IndexOf(activePopupButtons, item);
        if (index >= 0) SelectPopupButton(index);
    }

    // PadMenuCursor delegates the authored menu button groups here to share mouse/pad selection.
    public bool HandleConfirmationInput(Gamepad pad)
    {
        bool popupOpen = (newGamePopup != null && newGamePopup.activeInHierarchy) ||
            (quitGamePopup != null && quitGamePopup.activeInHierarchy);
        bool mainMenuOpen = !popupOpen && menuScreen != null && menuScreen.activeInHierarchy &&
            (settingsScreen == null || !settingsScreen.activeInHierarchy);
        if ((!popupOpen && !mainMenuOpen) || activePopupButtons == null || activePopupButtons.Length == 0) return false;
        if (pad == null || Time.frameCount <= popupOpenedFrame || transitionRequested ||
            LoadingScreen.IsLoading || GameSettingsManager.IsRebindingInputCaptured) return true;
        float axis = mainMenuOpen ? -pad.dpad.ReadValue().y : pad.dpad.ReadValue().x;
        if (Mathf.Abs(axis) < .5f) axis = mainMenuOpen ? -pad.leftStick.ReadValue().y : pad.leftStick.ReadValue().x;
        int direction = Mathf.Abs(axis) >= .5f ? axis > 0 ? 1 : -1 : 0;
        if (direction != 0 && (direction != lastPopupDirection || Time.unscaledTime >= nextPopupMoveAt))
        {
            nextPopupMoveAt = Time.unscaledTime + (direction != lastPopupDirection ? .35f : .12f);
            for (int step = 1; step <= activePopupButtons.Length; step++)
            {
                int index = (selectedPopupIndex + direction * step + activePopupButtons.Length * 2) % activePopupButtons.Length;
                if (SelectPopupButton(index)) break;
            }
        }
        lastPopupDirection = direction;
        if (MenuPadInput.ConfirmPressed(pad) && selectedPopupIndex >= 0)
        {
            var selected = activePopupButtons[selectedPopupIndex];
            var button = selected != null ? selected.button != null ? selected.button : selected.GetComponent<Button>() : null;
            if (button != null && button.IsActive() && button.IsInteractable()) button.onClick.Invoke();
        }
        return true;
    }

    private void ClearPopupSelection()
    {
        if (activePopupButtons != null)
            foreach (var item in activePopupButtons)
                if (item != null)
                {
                    item.PointerEntered -= OnPopupPointerEntered;
                    item.OnPointerExit(null);
                    item.OnDeselect(null);
                    item.Deselect();
                }
        activePopupButtons = null;
        selectedPopupIndex = -1;
    }

    public void QuitGame()
    {
        if (transitionRequested || LoadingScreen.isLoadingOn) return;
        if (SaveSystem.savesLoaded) SaveSystem.SaveData();
        Application.Quit();
    }

    private void SetScreenActive(GameObject screen, bool active)
    {
        if (screen != null && !transform.IsChildOf(screen.transform)) screen.SetActive(active);
    }

    private void LateUpdate()
    {
        bool visible = menuScreen != null && menuScreen.activeInHierarchy &&
            (settingsScreen == null || !settingsScreen.activeInHierarchy) &&
            (quitGamePopup == null || !quitGamePopup.activeInHierarchy) &&
            (newGamePopup == null || !newGamePopup.activeInHierarchy);
        if (visible && !mainScreenWasVisible && !transitionRequested)
            InitializePopupSelection(menuScreen, ref mainButtons);
        mainScreenWasVisible = visible;
    }

    private void Update()
    {
        if (transitionRequested || LoadingScreen.isLoadingOn || GameSettingsManager.IsRebindingInputCaptured) return;
        if (newGamePopup != null && newGamePopup.activeSelf)
        {
            if (FarmInput.Back) ShowMenuScreen();
            else if (Time.frameCount > newGamePopupOpenedFrame &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) ConfirmNewGame();
            return;
        }
        if (!FarmInput.Back) return;
        if ((settingsScreen != null && settingsScreen.activeSelf) || (quitGamePopup != null && quitGamePopup.activeSelf))
            ShowMenuScreen();
        else ShowQuitGamePopup();
    }

    private LoadingScreen ResolveLoadingScreen()
    {
        if (LoadingScreen.Instance != null) return LoadingScreen.Instance;
        return FindFirstObjectByType<LoadingScreen>(FindObjectsInactive.Include);
    }

    private void BeginGame(bool newGame)
    {
        if (transitionRequested || !isActiveAndEnabled) return;
        if (!LoadingScreen.CanLoadScene(gameplayScene)) return;
        var loading = ResolveLoadingScreen();
        if (loading == null)
        {
            Debug.LogError("Assign a persistent LoadingScreen, for example under GlobalManager.", this);
            return;
        }
        transitionRequested = true;
        AudioManager.Instance?.PlayConfirm();
        if (!loading.RunTransition(StartGame(newGame), minimumLoadingSeconds)) transitionRequested = false;
    }

    private IEnumerator StartGame(bool newGame)
    {
        try
        {
            if (!SaveSystem.savesLoaded)
            {
                var load = SaveSystem.LoadData();
                while (!load.IsCompleted) yield return null;
                if (load.IsFaulted) throw load.Exception;
            }
            if (newGame)
            {
                var reset = SaveSystem.MakeNewSave();
                while (!reset.IsCompleted) yield return null;
                if (reset.IsFaulted) throw reset.Exception;
            }
            string destination = !newGame && SaveSystem.Data?.world?.scene == "Last Scene" ? "Last Scene" : gameplayScene;
            yield return LoadingScreen.LoadSceneRoutine(destination);
        }
        finally { transitionRequested = false; }
    }

    private void OnEnable()
    {
        Instance = this;
        mainScreenWasVisible = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // Reset the page selection when this menu scene opens again.
        SetScreenActive(newGamePopup, false);
        RefreshContinueButtonState();
        SetScreenActive(menuScreen, true);
        SetScreenActive(settingsScreen, false);
        SetScreenActive(quitGamePopup, false);
        if (menuScreen != null) InitializePopupSelection(menuScreen, ref mainButtons);
        cameras.Clear();
        cameraTargets.Clear();
        suspendedCameras.Clear();
        suspendedListeners.Clear();
        listeners.Clear();
        if (menuCameras != null)
            foreach (var camera in menuCameras)
            {
                if (camera == null || cameras.Contains(camera)) continue;
                cameras.Add(camera);
                cameraTargets.Add(camera.targetTexture);
                var listener = camera.GetComponent<AudioListener>();
                listeners.Add(listener);
            }
        if (cameras.Count == 0) return;
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            if (camera.enabled && !cameras.Contains(camera))
            {
                suspendedCameras.Add(camera);
                camera.enabled = false;
            }
        if (listeners.Exists(listener => listener != null))
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.enabled && !listeners.Contains(listener))
                {
                    suspendedListeners.Add(listener);
                    listener.enabled = false;
                }
        // Authored menu cameras may start inactive outside the menu.
        foreach (var camera in cameras)
        {
            camera.enabled = false;
            camera.targetTexture = null;
        }
        foreach (var camera in cameras) camera.gameObject.SetActive(true);
        ShowCamera(0);
        if (cameras.Count < 2) return;
        CreateFade();
        playback = StartCoroutine(CycleCameras());
    }

    private IEnumerator CycleCameras()
    {
        int current = 0;
        while (true)
        {
            // Menu transitions keep running even when gameplay time is paused.
            yield return new WaitForSecondsRealtime(Mathf.Max(.1f, cameraHoldSeconds));
            int next = (current + 1) % cameras.Count;
            if (cameras[next] == null) continue;
            PrepareBlendTexture();
            cameras[next].targetTexture = blendTexture;
            cameras[next].enabled = true;
            // The current camera keeps rendering to the screen under the overlay.
            // Let the incoming camera render before revealing its image.
            yield return null;
            yield return FadeTo(1f);
            cameras[next].targetTexture = null;
            ShowCamera(next);
            // Keep the fully opaque image until the new camera has rendered
            // directly to the screen, then remove the overlay without a gap.
            yield return null;
            fade.alpha = 0f;
            current = next;
        }
    }

    private IEnumerator FadeTo(float target)
    {
        float start = fade.alpha;
        float duration = Mathf.Max(0f, fadeSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        fade.alpha = target;
    }

    private void ShowCamera(int index)
    {
        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i] != null) cameras[i].enabled = i == index;
            if (listeners[i] != null) listeners[i].enabled = i == index;
        }
    }

    private void CreateFade()
    {
        var layer = new GameObject("Menu camera crossfade", typeof(RectTransform), typeof(Canvas));
        // Keep this a root canvas: a canvas nested under the menu retains its
        // default RectTransform size instead of automatically filling the screen.
        SceneManager.MoveGameObjectToScene(layer, gameObject.scene);
        var canvas = layer.GetComponent<Canvas>();
        cameraCanvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = cameras[0].targetDisplay;
        canvas.overrideSorting = true;
        // Keep menu buttons and text above the background transition.
        canvas.sortingOrder = -1000;
        blendImage = CreateCameraImage("Incoming camera", layer.transform);
        fade = blendImage.gameObject.AddComponent<CanvasGroup>();
        fade.alpha = 0f;
        fade.interactable = false;
        fade.blocksRaycasts = false;
    }

    private RawImage CreateCameraImage(string imageName, Transform parent)
    {
        var overlay = new GameObject(imageName, typeof(RectTransform), typeof(RawImage));
        overlay.transform.SetParent(parent, false);
        var rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = overlay.GetComponent<RawImage>();
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private void PrepareBlendTexture()
    {
        int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        if (blendTexture != null && blendTexture.width == width && blendTexture.height == height) return;
        ReleaseBlendTexture();
        blendTexture = CreateCameraTexture("Menu camera crossfade", width, height);
        blendImage.texture = blendTexture;
    }

    private RenderTexture CreateCameraTexture(string textureName, int width, int height)
    {
        var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = textureName,
            antiAliasing = 1
        };
        texture.Create();
        return texture;
    }

    private void ReleaseBlendTexture()
    {
        // Detach cameras before releasing textures, including on resolution changes.
        foreach (var camera in cameras)
            if (camera != null && blendTexture != null && camera.targetTexture == blendTexture)
                camera.targetTexture = null;
        if (blendImage != null) blendImage.texture = null;
        if (blendTexture == null) return;
        blendTexture.Release();
        Destroy(blendTexture);
        blendTexture = null;
    }

    private void OnDisable()
    {
        ClearPopupSelection();
        if (playback != null) StopCoroutine(playback);
        playback = null;
        if (cameraCanvas != null)
        {
            cameraCanvas.gameObject.SetActive(false);
            Destroy(cameraCanvas.gameObject);
            cameraCanvas = null;
        }
        fade = null;
        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i] != null) cameras[i].targetTexture = cameraTargets[i];
        }
        // Stop every assigned camera even if setup was interrupted.
        if (menuCameras != null)
            foreach (var camera in menuCameras)
            {
                if (camera == null) continue;
                camera.enabled = false;
                var listener = camera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
                camera.gameObject.SetActive(false);
            }
        foreach (var camera in suspendedCameras)
            if (camera != null) camera.enabled = true;
        foreach (var listener in suspendedListeners)
            if (listener != null) listener.enabled = true;
        ReleaseBlendTexture();
        blendImage = null;
    }
}
