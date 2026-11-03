using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayCanvas : MonoBehaviour
{
    public static GameplayCanvas Instance;

    [SerializeField] private GameObject hudObject;
    [SerializeField] private CanvasGroup hudNavigationObject;
    private bool hudHiddenByPause;
    private bool hudActiveBeforePause;
    private readonly HashSet<Object> cutsceneHudOwners = new HashSet<Object>();

    public void SetCutsceneHudHidden(Object owner, bool hidden)
    {
        if (hidden) cutsceneHudOwners.Add(owner);
        else cutsceneHudOwners.Remove(owner);
        if (cutsceneHudOwners.Count > 0 || (Pause.Instance != null && Pause.Instance.paused)) HideHudForPause();
        else RestoreHudAfterPause();
    }
    [Header("Money")]
    [SerializeField] private Text moneyText;
    [SerializeField, Min(0f)] private float moneyAnimationSeconds = .4f;
    private bool moneyInitialized;
    private double displayedMoney, moneyAnimationStart;
    private int targetMoney;
    private float moneyAnimationElapsed;
    
    [Header("Money Change")]
    [SerializeField] private CanvasGroup moneyChangeCanvas;
    [SerializeField] private Text moneyChangeValue;
    [SerializeField] private RectTransform arrow;
    [SerializeField] private Image arrowImage;
    [SerializeField] private Color addMoneyColor;
    [SerializeField] private Color removeMoneyColor;
    [SerializeField, Min(0f)] private float moneyChangeFadeSeconds = .2f;
    [SerializeField, Min(0f)] private float moneyChangeTransferDelay = .3f;
    [SerializeField, Min(0f)] private float moneyChangeHoldSeconds = .35f;
    private bool moneyChangeFadingIn, moneyChangeAdding;
    private float moneyChangeHoldRemaining;
    private float moneyChangeDelayRemaining;

    [Space, Header("Interactor")]
    public InteractionPrompt interactionPrompt;

    [Space, Header("Hints")]
    [SerializeField] private GameObject[] walkingHints;
    [SerializeField] private GameObject[] drivingHints;
    [SerializeField] private GameObject[] bicycleHints;
    [SerializeField] private GameObject[] forkliftHints;
    [SerializeField] private TMP_Text walkingInteraction;
    private BagInteraction playerBags;

    [Space, Header("Tools Equip Hints")] 
    [SerializeField] private CanvasGroup hintsParent;
    [SerializeField] private SelectedUI shovelEquipped;
    [SerializeField] private SelectedUI broomEquipped;
    [SerializeField] private SelectedUI axeEquipped;
    [SerializeField] private SelectedUI hammerEquipped;
    [SerializeField, Min(0f)] private float toolHintsFadeSeconds = .25f;
    private PlayerCombatTools playerTools;
    private int displayedTools = -1;
    private PlayerCombatTools.Tool displayedEquipped;
    private bool fadingToolHints;

    [Space, Header("Boosters")]
    [SerializeField] private Transform boostersParent;
    [SerializeField] private BoosterStatusHandler boosterHandlerPrefab;
    private readonly Dictionary<ChestReward, BoosterStatusHandler> boosterHandlers = new Dictionary<ChestReward, BoosterStatusHandler>();
    private PlayerData boosterProfile;
    private static readonly ChestReward[] TimedBoosters =
    {
        ChestReward.InfiniteStamina, ChestReward.NoAttacks, ChestReward.Mobility, ChestReward.DoubleSellCash
    };

    [Space, Header("Warning")] 
    [SerializeField] private WarningPopup warningPopup;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        ResetFreeHandsWarning();
        if (warningPopup != null) warningPopup.Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        ClearBoosters();
    }

    private void OnEnable()
    {
        Pause.PauseOnAction += HideHudForPause;
        Pause.PauseOffAction += RestoreHudAfterPause;
        if (Pause.Instance != null && Pause.Instance.paused) HideHudForPause();
        ResetFreeHandsWarning();
        SaveSystem.MoneyChanged += RefreshMoney;
        // Opening the HUD shows the saved balance immediately; only later changes animate.
        moneyInitialized = false;
        ResetMoneyChange();
        RefreshMoney(SaveSystem.Money);
        var rider = FindFirstObjectByType<PlayerBicycleInteraction>();
        playerBags = FindFirstObjectByType<BagInteraction>(FindObjectsInactive.Include);
        RefreshWalkingInteraction();
        SetDrivingHints(rider != null && rider.IsRiding, rider != null && rider.IsRiding && rider.bicycle != null && rider.bicycle.GetComponent<ForkliftController>() != null);
        playerTools = FindFirstObjectByType<PlayerCombatTools>(FindObjectsInactive.Include);
        // Rebuild hint visibility from saved discoveries whenever the HUD opens.
        displayedTools = -1;
        RefreshCollectibleUI();
    }

    private void OnDisable()
    {
        Pause.PauseOnAction -= HideHudForPause;
        Pause.PauseOffAction -= RestoreHudAfterPause;
        RestoreHudAfterPause();
        SaveSystem.MoneyChanged -= RefreshMoney;
        ResetMoneyChange();
        ResetFreeHandsWarning();
    }

    private void HideHudForPause()
    {
        if (hudObject == null || hudHiddenByPause) return;
        hudActiveBeforePause = hudObject.activeSelf;
        hudHiddenByPause = true;
        hudObject.SetActive(false);
        if (hudNavigationObject != null) hudNavigationObject.alpha = 0f;
    }

    private void RestoreHudAfterPause()
    {
        if (cutsceneHudOwners.Count > 0) return;
        if (!hudHiddenByPause) return;
        hudHiddenByPause = false;
        if (hudObject != null) hudObject.SetActive(hudActiveBeforePause);
        if (hudNavigationObject != null) hudNavigationObject.alpha = 1f;
    }

    public void SetFreeHandsWarning(bool visible)
    {
        SetWarning(WarningType.OccupiedHands, this, visible && isActiveAndEnabled);
    }

    public void SetDrivingHints(bool driving, bool forklift = false)
    {
        var rider = driving ? FindFirstObjectByType<PlayerBicycleInteraction>() : null;
        bool ridingBicycle = driving && !forklift && rider != null && rider.IsRiding &&
            rider.bicycle != null && !rider.bicycle.motorDriven;
        if (walkingHints != null)
            foreach (var hint in walkingHints)
                if (hint != null) hint.SetActive(!driving);
        if (drivingHints != null)
            foreach (var hint in drivingHints)
                if (hint != null) hint.SetActive(driving);
        if (bicycleHints != null)
            foreach (var hint in bicycleHints)
                if (hint != null) hint.SetActive(ridingBicycle);
        if (forkliftHints != null)
            foreach (var hint in forkliftHints)
                if (hint != null) hint.SetActive(driving && forklift);
    }

    private void Update()
    {
        if (warningPopup != null) warningPopup.Tick(Time.deltaTime);
        if (!hudHiddenByPause) AnimateMoney(Time.unscaledDeltaTime);
        RefreshCollectibleUI();
        if (fadingToolHints && hintsParent != null)
        {
            hintsParent.alpha = toolHintsFadeSeconds > 0f
                ? Mathf.MoveTowards(hintsParent.alpha, 1f, Time.unscaledDeltaTime / toolHintsFadeSeconds) : 1f;
            fadingToolHints = hintsParent.alpha < 1f;
        }
    }

    private void LateUpdate() => RefreshWalkingInteraction();

    private void RefreshWalkingInteraction()
    {
        if (walkingInteraction == null) return;
        string action = playerBags != null && playerBags.IsCarrying ? "Drop" : "Interact";
        if (walkingInteraction.text != action) walkingInteraction.text = action;
    }

    public void ShowWarning(WarningType type, float seconds = 2f)
    {
        if (warningPopup != null) warningPopup.ShowWarning(type, seconds);
    }

    public void SetWarning(WarningType type, Object source, bool visible)
    {
        if (warningPopup != null) warningPopup.SetWarning(type, source, visible);
    }

    private void RefreshCollectibleUI()
    {
        int tools = 1;
        if (IsToolUnlocked(PlayerCombatTools.Tool.Broom)) tools |= 2;
        if (IsToolUnlocked(PlayerCombatTools.Tool.Axe)) tools |= 4;
        if (IsToolUnlocked(PlayerCombatTools.Tool.Hammer)) tools |= 8;
        var equipped = playerTools != null ? playerTools.Equipped : PlayerCombatTools.Tool.Shovel;
        if (tools != displayedTools || equipped != displayedEquipped)
        {
            bool showNewTool = displayedTools < 0 || (tools & ~displayedTools) != 0;
            SetToolHint(shovelEquipped, true, equipped == PlayerCombatTools.Tool.Shovel);
            SetToolHint(broomEquipped, (tools & 2) != 0, equipped == PlayerCombatTools.Tool.Broom);
            SetToolHint(axeEquipped, (tools & 4) != 0, equipped == PlayerCombatTools.Tool.Axe);
            SetToolHint(hammerEquipped, (tools & 8) != 0, equipped == PlayerCombatTools.Tool.Hammer);
            displayedTools = tools;
            displayedEquipped = equipped;
            if (hintsParent != null && tools == 1)
            {
                // The shovel is available from the start; show equipment hints
                // only after another tool has been discovered.
                hintsParent.gameObject.SetActive(false);
                hintsParent.alpha = 0f;
                fadingToolHints = false;
            }
            else if (showNewTool && hintsParent != null)
            {
                hintsParent.gameObject.SetActive(true);
                hintsParent.alpha = toolHintsFadeSeconds > 0f ? 0f : 1f;
                fadingToolHints = toolHintsFadeSeconds > 0f;
            }
        }

        var data = SaveSystem.Data;
        if (boosterProfile != data)
        {
            ClearBoosters();
            boosterProfile = data;
        }
        foreach (var reward in TimedBoosters)
        {
            long until = BoosterStatusHandler.Expiry(data, reward);
            boosterHandlers.TryGetValue(reward, out var handler);
            if (until <= PlayerPerks.Now)
            {
                if (handler != null)
                {
                    if (Application.isPlaying) Destroy(handler.gameObject);
                    else DestroyImmediate(handler.gameObject);
                }
                boosterHandlers.Remove(reward);
                continue;
            }
            if (handler == null)
            {
                if (boosterHandlerPrefab == null || boostersParent == null) continue;
                handler = Instantiate(boosterHandlerPrefab, boostersParent, false);
                handler.Initialize(reward, until);
                handler.gameObject.SetActive(true);
                boosterHandlers[reward] = handler;
            }
            else handler.SetExpiry(until);
        }
    }

    private bool IsToolUnlocked(PlayerCombatTools.Tool tool)
    {
        if (playerTools != null) return playerTools.IsUnlocked(tool);
        var data = SaveSystem.Data;
        return tool == PlayerCombatTools.Tool.Broom ? BuriedFind.WasFound(data, PlayerCombatTools.BroomFindId)
            : tool == PlayerCombatTools.Tool.Axe ? BuriedFind.WasFound(data, PlayerCombatTools.AxeFindId) || BuriedFind.WasFound(data, PlayerCombatTools.LegacyAxeFindId)
            : BuriedFind.WasFound(data, PlayerCombatTools.HammerFindId);
    }

    private static void SetToolHint(SelectedUI hint, bool available, bool selected)
    {
        if (hint == null) return;
        hint.gameObject.SetActive(available);
        if (available && selected) hint.Select();
        else hint.Deselect();
    }

    private void ClearBoosters()
    {
        foreach (var handler in boosterHandlers.Values)
            if (handler != null)
            {
                if (Application.isPlaying) Destroy(handler.gameObject);
                else DestroyImmediate(handler.gameObject);
            }
        boosterHandlers.Clear();
    }

    private void ResetFreeHandsWarning()
    {
        SetFreeHandsWarning(false);
    }

    public void RefreshMoneyFromSave() => RefreshMoney(SaveSystem.Money);

    private void RefreshMoney(int amount)
    {
        if (moneyInitialized && amount == targetMoney) return;
        bool transferPending = moneyInitialized && displayedMoney != targetMoney;
        targetMoney = amount;
        moneyAnimationStart = displayedMoney;
        moneyAnimationElapsed = 0f;
        if (!moneyInitialized)
        {
            displayedMoney = amount;
            moneyInitialized = true;
            RenderMoney();
            return;
        }
        double change = targetMoney - displayedMoney;
        if (change == 0d)
        {
            moneyChangeFadingIn = false;
            moneyChangeHoldRemaining = 0f;
            moneyChangeDelayRemaining = 0f;
            RenderMoneyChange();
            return;
        }
        moneyChangeAdding = change > 0d;
        Color color = moneyChangeAdding ? addMoneyColor : removeMoneyColor;
        if (moneyChangeValue != null) moneyChangeValue.color = color;
        if (arrowImage != null) arrowImage.color = color;
        if (arrow != null) arrow.localRotation = Quaternion.Euler(0f, 0f, moneyChangeAdding ? 0f : 180f);
        moneyChangeHoldRemaining = moneyChangeHoldSeconds;
        moneyChangeFadingIn = moneyChangeCanvas != null;
        if (!transferPending)
            moneyChangeDelayRemaining = moneyChangeCanvas != null ? moneyChangeTransferDelay : 0f;
        if (moneyChangeCanvas != null)
        {
            moneyChangeCanvas.interactable = false;
            moneyChangeCanvas.blocksRaycasts = false;
            moneyChangeCanvas.gameObject.SetActive(true);
        }
        RenderMoneyChange();
    }

    private void AnimateMoney(float deltaTime)
    {
        if (!moneyInitialized) return;
        deltaTime = Mathf.Max(0f, deltaTime);
        if (moneyChangeFadingIn)
        {
            float fadeTime = moneyChangeFadeSeconds > 0f ? (1f - moneyChangeCanvas.alpha) * moneyChangeFadeSeconds : 0f;
            moneyChangeCanvas.alpha = Mathf.MoveTowards(moneyChangeCanvas.alpha, 1f,
                moneyChangeFadeSeconds > 0f ? deltaTime / moneyChangeFadeSeconds : 1f);
            if (moneyChangeCanvas.alpha < 1f) return;
            moneyChangeFadingIn = false;
            deltaTime = Mathf.Max(0f, deltaTime - fadeTime);
        }
        if (displayedMoney == targetMoney)
        {
            float holdTime = Mathf.Min(deltaTime, moneyChangeHoldRemaining);
            moneyChangeHoldRemaining -= holdTime;
            deltaTime -= holdTime;
            if (moneyChangeCanvas != null && moneyChangeHoldRemaining <= 0f)
                moneyChangeCanvas.alpha = Mathf.MoveTowards(moneyChangeCanvas.alpha, 0f,
                    moneyChangeFadeSeconds > 0f ? deltaTime / moneyChangeFadeSeconds : 1f);
            return;
        }
        float delayTime = Mathf.Min(deltaTime, moneyChangeDelayRemaining);
        moneyChangeDelayRemaining -= delayTime;
        deltaTime -= delayTime;
        if (moneyChangeDelayRemaining > 0f || deltaTime <= 0f) return;
        moneyAnimationElapsed += deltaTime;
        float progress = moneyAnimationSeconds > 0f ? Mathf.Clamp01(moneyAnimationElapsed / moneyAnimationSeconds) : 1f;
        // Keep the interpolated value in double precision, including large balances.
        // New transactions restart from the current display rather than the old target.
        displayedMoney = progress >= 1f ? targetMoney
            : moneyAnimationStart + (targetMoney - moneyAnimationStart) * Mathf.SmoothStep(0f, 1f, progress);
        RenderMoney();
        RenderMoneyChange();
    }

    private void RenderMoneyChange()
    {
        if (moneyChangeValue == null) return;
        // Match the rounded balance so the visible remaining amount and balance add up exactly.
        double remaining = System.Math.Abs(targetMoney - System.Math.Round(displayedMoney));
        moneyChangeValue.text = (moneyChangeAdding ? "+$" : "-$") + EconomyNumberFormat.Accounting(remaining);
    }

    private void ResetMoneyChange()
    {
        moneyChangeDelayRemaining = 0f;
        moneyChangeFadingIn = false;
        moneyChangeHoldRemaining = 0f;
        if (moneyChangeCanvas != null)
        {
            moneyChangeCanvas.alpha = 0f;
            moneyChangeCanvas.interactable = false;
            moneyChangeCanvas.blocksRaycasts = false;
        }
        if (moneyChangeValue != null) moneyChangeValue.text = "";
    }

    private void RenderMoney()
    {
        if (moneyText != null)
            moneyText.text = "$" + EconomyNumberFormat.Accounting((int)System.Math.Round(displayedMoney));
    }

    public bool AddMoney(int amount) => SaveSystem.AddMoney(amount);

    public bool SpendMoney(int amount) => SaveSystem.SpendMoney(amount);
}
