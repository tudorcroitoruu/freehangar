using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(-1500)]
public sealed class DemoSession : MonoBehaviour
{
    public const uint DemoAppId = 5602740, FullAppId = 5602730;
    public const float Allowance = 3600f;
    private const string TimeKey = "Demo.PlayedSeconds.5602740";
#if UNITY_EDITOR
    private static SteamAchievementSettings editorSettings;
#endif
    public static bool IsDemo {
        get {
#if DEMO_BUILD
            return true;
#elif UNITY_EDITOR
            if (!editorSettings) editorSettings = Resources.Load<SteamAchievementSettings>("SteamAchievementSettings");
            return editorSettings && editorSettings.previewDemoInEditor;
#else
            return false;
#endif
        }
    }
    public static DemoSession Instance { get; private set; }
    private static int closedFrame = -1;
    public static bool BlocksInput => ModalOpen || closedFrame == Time.frameCount;
    public static bool ModalOpen => Instance != null && Instance.modal;
    public static bool Expired => IsDemo && (Instance != null ? Instance.elapsed : PlayerPrefs.GetFloat(TimeKey)) >= Allowance;
    private float elapsed, saveAt, previousTimeScale;
    private GameObject panel;
    private Button yes, no;
    private TMP_Text timer;
    private RectTransform perks;
    private bool modal, menuPending, returning, previousNavigation;
    private int openedFrame, selected;
    private Vector2 lastDirection;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() {
        Instance = null; closedFrame = -1;
#if UNITY_EDITOR
        editorSettings = null;
#endif
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install() { if (Instance == null) new GameObject("Demo session").AddComponent<DemoSession>(); }
    private void Awake() {
        Instance = this; DontDestroyOnLoad(gameObject);
        elapsed = Mathf.Clamp(PlayerPrefs.GetFloat(TimeKey), 0, Allowance);
        SceneManager.sceneLoaded += SceneLoaded;
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode) {
        if (modal) Time.timeScale = previousTimeScale;
        modal = false; returning = false; panel = null; timer = null; perks = null; yes = no = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (node.name == "Demo" && node.parent != null &&
                    (node.parent.name == "MenuCanvas" || node.parent.name == "GameCanvas"))
                { panel = node.gameObject; panel.SetActive(false); }
        if (!IsDemo) return;
        if (panel != null) {
            foreach (var button in panel.GetComponentsInChildren<Button>(true)) {
                if (button.name == "ConfirmNewGame") yes = button;
                if (button.name == "CancelNewGame") no = button;
            }
            var canvas = panel.GetComponent<Canvas>();
            if (!canvas) canvas = panel.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 20000;
            if (!panel.GetComponent<GraphicRaycaster>()) panel.AddComponent<GraphicRaycaster>();
            var blocker = panel.GetComponent<Image>(); if (blocker) blocker.raycastTarget = true;
        }
        menuPending = scene.name == "Menu";
        if (scene.name == "Gameplay") CreateTimer();
    }
    private void CreateTimer() {
        var hud = GameplayCanvas.Instance;
        if (!hud) hud = FindFirstObjectByType<GameplayCanvas>();
        if (!hud) return;
        var obj = new GameObject("Demo time remaining", typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(hud.transform, false);
        timer = obj.GetComponent<TextMeshProUGUI>(); var rect = timer.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(28, -230); rect.sizeDelta = new Vector2(400, 90);
        var presentation = panel ? panel.GetComponent<DemoPopupButtons>() : null;
        timer.font = presentation && presentation.timerFont ? presentation.timerFont : TMP_Settings.defaultFontAsset;
        timer.fontSize = 28; timer.fontStyle = FontStyles.Bold;
        timer.color = new Color(.8f, .08f, .03f); timer.raycastTarget = false;
        foreach (var node in hud.GetComponentsInChildren<RectTransform>(true))
            if (node.name == "Boosters") { perks = node; break; }
    }
    private void Update() {
        if (!IsDemo) return;
        if (menuPending && !LoadingScreen.IsLoading) { menuPending = false; ShowOffer(); }
        bool gameplay = SceneManager.GetActiveScene().name == "Gameplay";
        if (gameplay && !Expired && !modal && !LoadingScreen.IsLoading && SaveSystem.savesLoaded &&
            Application.isFocused && Time.timeScale > 0 && !UIPopups.BasePopupUI.HasActivePopup &&
            !(Pause.Instance && Pause.Instance.paused)) {
            elapsed = Mathf.Min(Allowance, elapsed + Time.unscaledDeltaTime);
            if (Time.unscaledTime >= saveAt) { Persist(); saveAt = Time.unscaledTime + 5; }
        }
        if (timer) {
            if (perks) {
                var parent = (RectTransform)timer.transform.parent;
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, perks);
                timer.rectTransform.anchoredPosition = new Vector2(28, Mathf.Min(-180, bounds.min.y - parent.rect.yMax - 12));
            }
            int seconds = Mathf.CeilToInt(Mathf.Max(0, Allowance - elapsed));
            timer.text = $"DEMO VERSION\nTime left: {seconds / 60:00}:{seconds % 60:00}";
        }
        if (gameplay && Expired && !modal && !returning && !LoadingScreen.IsLoading) ShowOffer();
        if (!modal) return;
        if (gameplay) Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        var events = EventSystem.current; if (events) events.sendNavigationEvents = false;
        if (Time.frameCount <= openedFrame || !Application.isFocused) return;
        var pad = FarmInput.ActivePad ?? Gamepad.current;
        Vector2 direction = pad != null ? pad.dpad.ReadValue() : Vector2.zero;
        if (pad != null && direction.sqrMagnitude < .25f) direction = pad.leftStick.ReadValue();
        if ((direction.sqrMagnitude >= .25f && lastDirection.sqrMagnitude < .25f) ||
            Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Tab)) Select(1 - selected);
        lastDirection = direction;
        if (pad?.buttonSouth.wasPressedThisFrame == true || Input.GetKeyDown(KeyCode.Return)) {
            if (events && events.currentSelectedGameObject == no.gameObject) Decline(); else Buy();
        } else if (pad?.buttonEast.wasPressedThisFrame == true || Input.GetKeyDown(KeyCode.Escape)) Decline();
    }
    private void Select(int index) {
        selected = index; EventSystem.current?.SetSelectedGameObject((index == 0 ? yes : no).gameObject);
    }
    public void ShowOffer() {
        if (!IsDemo || modal) return;
        if (!panel || !yes || !no) { Debug.LogError("Demo panel or Yes/No buttons missing."); return; }
        previousTimeScale = Time.timeScale;
        previousNavigation = EventSystem.current && EventSystem.current.sendNavigationEvents;
        modal = true; openedFrame = Time.frameCount; lastDirection = Vector2.zero;
        panel.SetActive(true); panel.transform.SetAsLastSibling();
        if (SceneManager.GetActiveScene().name == "Gameplay") {
            Persist(); if (SaveSystem.savesLoaded) SaveSystem.SaveData(); Time.timeScale = 0;
        }
        Select(0);
    }
    public void Buy() {
        if (!IsDemo || !modal) return;
        Application.OpenURL("https://store.steampowered.com/app/5602730/Grain_Cleanup_Co/");
        // Expiry stays modal after visiting the store: only leaving for the menu is allowed.
        if (SceneManager.GetActiveScene().name == "Menu") CloseOffer();
    }
    public void Decline() {
        if (!IsDemo || !modal || returning) return;
        if (SceneManager.GetActiveScene().name != "Menu" && Expired) {
            if (LoadingScreen.Instance == null) { Debug.LogError("LoadingScreen unavailable."); return; }
            returning = true; Persist(); if (SaveSystem.savesLoaded) SaveSystem.SaveData();
            LoadingScreen.Instance.LoadScene("Menu");
        } else CloseOffer();
    }
    private void CloseOffer() {
        modal = false; closedFrame = Time.frameCount; if (panel) panel.SetActive(false);
        if (EventSystem.current) EventSystem.current.sendNavigationEvents = previousNavigation;
        Time.timeScale = previousTimeScale;
        if (MenuManager.IsMenuOpen) MenuManager.Instance.ShowMenuScreen();
    }
    private void Persist() {
        if (!IsDemo) return;
        PlayerPrefs.SetFloat(TimeKey, elapsed); PlayerPrefs.Save();
    }
    private void OnApplicationFocus(bool focused) { if (!focused) Persist(); }
    private void OnApplicationQuit() => Persist();
    private void OnDestroy() {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (Instance == this) { Persist(); Instance = null; }
    }
}
