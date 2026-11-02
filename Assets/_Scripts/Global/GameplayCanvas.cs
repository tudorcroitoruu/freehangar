using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameplayCanvas : MonoBehaviour
{
    public static GameplayCanvas Instance;

    [SerializeField] private GameObject hudObject;
    private bool hudHiddenByPause;
    private bool hudActiveBeforePause;
    [Header("Money")]
    [SerializeField] private Text moneyText;
    [SerializeField, Min(0f)] private float moneyAnimationSeconds = .4f;
    private bool moneyInitialized;
    private double displayedMoney, moneyAnimationStart;
    private int targetMoney;
    private float moneyAnimationElapsed;
    
    [Space, Header("Interactor")]
    public InteractionPrompt interactionPrompt;
    [SerializeField] private CanvasGroup freeHandsWarning;
    [SerializeField, Min(0f)] private float freeHandsFadeDuration = .2f;
    private bool showFreeHandsWarning;
    
    [Space, Header("Hints")]
    [SerializeField] private GameObject[] walkingHints;
    [SerializeField] private GameObject[] drivingHints;

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
        RefreshMoney(SaveSystem.Money);
        var rider = FindFirstObjectByType<PlayerBicycleInteraction>();
        SetDrivingHints(rider != null && rider.IsRiding);
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
        ResetFreeHandsWarning();
    }

    private void HideHudForPause()
    {
        if (hudObject == null || hudHiddenByPause) return;
        hudActiveBeforePause = hudObject.activeSelf;
        hudHiddenByPause = true;
        hudObject.SetActive(false);
    }

    private void RestoreHudAfterPause()
    {
        if (!hudHiddenByPause) return;
        hudHiddenByPause = false;
        if (hudObject != null) hudObject.SetActive(hudActiveBeforePause);
    }

    public void SetFreeHandsWarning(bool visible)
    {
        showFreeHandsWarning = visible && isActiveAndEnabled;
        if (freeHandsWarning == null) return;
        freeHandsWarning.interactable = false;
        freeHandsWarning.blocksRaycasts = false;
        if (showFreeHandsWarning && !freeHandsWarning.gameObject.activeSelf)
            freeHandsWarning.gameObject.SetActive(true);
        if (freeHandsFadeDuration <= 0f) freeHandsWarning.alpha = showFreeHandsWarning ? 1f : 0f;
    }

    public void SetDrivingHints(bool driving)
    {
        if (walkingHints != null)
            foreach (var hint in walkingHints)
                if (hint != null) hint.SetActive(!driving);
        if (drivingHints != null)
            foreach (var hint in drivingHints)
                if (hint != null) hint.SetActive(driving);
    }

    private void Update()
    {
        if (warningPopup != null) warningPopup.Tick(Time.deltaTime);
        AnimateMoney(Time.unscaledDeltaTime);
        FadeFreeHandsWarning(Time.unscaledDeltaTime);
        RefreshCollectibleUI();
        if (fadingToolHints && hintsParent != null)
        {
            hintsParent.alpha = toolHintsFadeSeconds > 0f
                ? Mathf.MoveTowards(hintsParent.alpha, 1f, Time.unscaledDeltaTime / toolHintsFadeSeconds) : 1f;
            fadingToolHints = hintsParent.alpha < 1f;
        }
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

    private void FadeFreeHandsWarning(float deltaTime)
    {
        if (freeHandsWarning == null) return;
        float step = freeHandsFadeDuration > 0f ? Mathf.Max(0f, deltaTime) / freeHandsFadeDuration : 1f;
        freeHandsWarning.alpha = Mathf.MoveTowards(freeHandsWarning.alpha, showFreeHandsWarning ? 1f : 0f, step);
    }

    private void ResetFreeHandsWarning()
    {
        showFreeHandsWarning = false;
        if (freeHandsWarning == null) return;
        freeHandsWarning.alpha = 0f;
        freeHandsWarning.interactable = false;
        freeHandsWarning.blocksRaycasts = false;
    }

    public void RefreshMoneyFromSave() => RefreshMoney(SaveSystem.Money);

    private void RefreshMoney(int amount)
    {
        if (moneyInitialized && amount == targetMoney) return;
        targetMoney = amount;
        moneyAnimationStart = displayedMoney;
        moneyAnimationElapsed = 0f;
        if (!moneyInitialized || moneyAnimationSeconds <= 0f)
        {
            displayedMoney = amount;
            moneyInitialized = true;
            RenderMoney();
        }
    }

    private void AnimateMoney(float deltaTime)
    {
        if (!moneyInitialized || displayedMoney == targetMoney) return;
        moneyAnimationElapsed += Mathf.Max(0f, deltaTime);
        float progress = moneyAnimationSeconds > 0f ? Mathf.Clamp01(moneyAnimationElapsed / moneyAnimationSeconds) : 1f;
        // Keep the interpolated value in double precision, including large balances.
        // New transactions restart from the current display rather than the old target.
        displayedMoney = progress >= 1f ? targetMoney
            : moneyAnimationStart + (targetMoney - moneyAnimationStart) * Mathf.SmoothStep(0f, 1f, progress);
        RenderMoney();
    }

    private void RenderMoney()
    {
        if (moneyText != null)
            moneyText.text = "$ " + EconomyNumberFormat.Accounting((int)System.Math.Round(displayedMoney));
    }

    public bool AddMoney(int amount) => SaveSystem.AddMoney(amount);

    public bool SpendMoney(int amount) => SaveSystem.SpendMoney(amount);
}
