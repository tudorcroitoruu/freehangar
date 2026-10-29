using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tracks work, recovery and the stamina capacity lost to hunger.</summary>
[DisallowMultipleComponent]
public sealed class StaminaManager : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float maxStamina = 100f;
    [SerializeField] private StaminaUpgradeSettings upgradeSettings;
    [SerializeField, Min(0f), Tooltip("Stamina per scoop for a 1 kg shovel; multiplied by the equipped shovel's weight.")]
    private float shovelActionCost = 10f;
    [Tooltip("Stamina consumed per second while carrying a bag.")]
    [SerializeField, Min(0f)] private float workConsumptionPerSecond = 10f;
    [SerializeField, Min(0f), Tooltip("Additional stamina consumed per second while sprinting.")]
    private float sprintConsumptionPerSecond = 10f;
    [SerializeField, Min(0f)] private float recoveryPerSecond = 15f;
    [SerializeField, Min(0f)] private float recoveryDelay = 2f;

    [Header("Hunger")]
    [SerializeField, Range(0f, 100f)] private float hungryPercent;
    [SerializeField, Min(0f), Tooltip("Percentage points of hunger gained per minute of game time. Zero pauses hunger growth.")]
    private float hungerIncreasePerMinute = 1f;

    [Header("UI")]
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private Image staminaBarBackground;

    private float recoveryDelayRemaining;
    private RectTransform backgroundRect;
    private float initialBackgroundSizeDeltaX;
    private PlayerData staminaProfile;
    public float CurrentStamina { get; private set; }
    private StaminaUpgradeSettings UpgradeSettings
    {
        get
        {
            if (upgradeSettings == null) upgradeSettings = Resources.Load<StaminaUpgradeSettings>(StaminaUpgradeSettings.ResourcePath);
            return upgradeSettings;
        }
    }
    public float MaxStamina => UpgradeSettings != null && UpgradeSettings.IsConfigured
        ? UpgradeSettings.Capacity(SaveSystem.Data?.staminaUpgradeLevel ?? 0) : maxStamina;
    public float HungryPercent => hungryPercent;
    public float CurrentMaxStamina => MaxStamina * (1f - Mathf.Clamp01(hungryPercent / 100f));
    public bool HasInfiniteStamina => SaveSystem.Data != null &&
        SaveSystem.Data.infiniteStaminaUntilUtc > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long InfiniteStaminaSecondsRemaining => SaveSystem.Data == null ? 0L :
        Math.Max(0L, SaveSystem.Data.infiniteStaminaUntilUtc - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    public float NormalizedStamina => CurrentMaxStamina > 0f ? HasInfiniteStamina ? 1f : Mathf.Clamp01(CurrentStamina / CurrentMaxStamina) : 0f;
    public bool IsExhausted => CurrentMaxStamina <= 0f || (!HasInfiniteStamina && CurrentStamina <= 0f);
    public bool IsWorking { get; private set; }

    private void Awake()
    {
        staminaProfile = SaveSystem.Data;
        CurrentStamina = CurrentMaxStamina;
        UpdateStaminaBar();
    }

    private void OnEnable()
    {
        SaveSystem.MoneyChanged += RefreshUpgrade;
        RefreshUpgrade(SaveSystem.Money);
    }

    private void OnDisable() => SaveSystem.MoneyChanged -= RefreshUpgrade;

    private void RefreshUpgrade(int _)
    {
        bool profileChanged = staminaProfile != SaveSystem.Data;
        staminaProfile = SaveSystem.Data;
        CurrentStamina = profileChanged || HasInfiniteStamina ? CurrentMaxStamina : Mathf.Clamp(CurrentStamina, 0f, CurrentMaxStamina);
        UpdateStaminaBar();
    }

    private void Update() => TickHunger(Time.deltaTime);

    public void TickHunger(float deltaTime)
    {
        if (!isActiveAndEnabled || !(deltaTime > 0f) || !float.IsFinite(deltaTime)) return;
        SetHungerPercent(hungryPercent + hungerIncreasePerMinute * (deltaTime / 60f));
    }

    public void SetHungerPercent(float percent)
    {
        if (!float.IsFinite(percent)) return;
        hungryPercent = Mathf.Clamp(percent, 0f, 100f);
        CurrentStamina = HasInfiniteStamina ? CurrentMaxStamina : Mathf.Clamp(CurrentStamina, 0f, CurrentMaxStamina);
        UpdateStaminaBar();
    }

    public bool ConsumeFood(float hungerReductionPercent)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || !float.IsFinite(hungerReductionPercent) ||
            hungerReductionPercent <= 0f || hungryPercent <= 0f) return false;
        SetHungerPercent(hungryPercent - hungerReductionPercent);
        return true;
    }

    public void ConsumeShovelStamina()
    {
        ConsumeShovelStamina(1f);
    }

    public void ConsumeShovelStamina(float shovelWeightKg)
    {
        if (HasInfiniteStamina) { CurrentStamina = CurrentMaxStamina; UpdateStaminaBar(); return; }
        if (!isActiveAndEnabled || Time.timeScale <= 0f || shovelActionCost <= 0f ||
            !float.IsFinite(shovelWeightKg) || shovelWeightKg <= 0f) return;
        CurrentStamina = Mathf.Clamp(CurrentStamina - shovelActionCost * shovelWeightKg, 0f, CurrentMaxStamina);
        recoveryDelayRemaining = recoveryDelay;
        UpdateStaminaBar();
    }

    public void Tick(bool isWorking, float deltaTime, bool recoveryBlocked = false, float consumptionPerSecond = -1f, bool sprinting = false)
    {
        if (!isActiveAndEnabled || !(deltaTime > 0f) || float.IsInfinity(deltaTime)) return;
        IsWorking = isWorking || sprinting;
        if (HasInfiniteStamina)
        {
            CurrentStamina = CurrentMaxStamina;
            recoveryDelayRemaining = 0f;
            UpdateStaminaBar();
            return;
        }
        if (isWorking || sprinting)
        {
            float workRate = isWorking ? (consumptionPerSecond >= 0f && float.IsFinite(consumptionPerSecond) ? consumptionPerSecond : workConsumptionPerSecond) : 0f;
            CurrentStamina = Mathf.Clamp(CurrentStamina - (workRate + (sprinting ? sprintConsumptionPerSecond : 0f)) * deltaTime, 0f, CurrentMaxStamina);
            recoveryDelayRemaining = recoveryDelay;
            UpdateStaminaBar();
            return;
        }

        if (recoveryBlocked)
        {
            recoveryDelayRemaining = recoveryDelay;
            return;
        }

        float restingTime = Mathf.Max(0f, deltaTime - recoveryDelayRemaining);
        recoveryDelayRemaining = Mathf.Max(0f, recoveryDelayRemaining - deltaTime);
        CurrentStamina = Mathf.Clamp(CurrentStamina + recoveryPerSecond * restingTime, 0f, CurrentMaxStamina);
        UpdateStaminaBar();
    }

    private void UpdateStaminaBar()
    {
        if (staminaBarFill != null) staminaBarFill.fillAmount = NormalizedStamina;
        if (staminaBarBackground == null) return;
        if (backgroundRect != staminaBarBackground.rectTransform)
        {
            backgroundRect = staminaBarBackground.rectTransform;
            initialBackgroundSizeDeltaX = backgroundRect.sizeDelta.x;
        }

        // Rebuild the full width from the original layout, including stretched anchors.
        float parentWidth = backgroundRect.parent is RectTransform parent ? parent.rect.width : 0f;
        float fullWidth = Mathf.Max(0f, parentWidth * (backgroundRect.anchorMax.x - backgroundRect.anchorMin.x) + initialBackgroundSizeDeltaX);
        float width = fullWidth * (1f - Mathf.Clamp01(hungryPercent / 100f));
        if (!Mathf.Approximately(backgroundRect.rect.width, width))
            backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private void OnGUI()
    {
        if (!HasInfiniteStamina) return;
        long remaining = InfiniteStaminaSecondsRemaining;
        GUI.Box(new Rect(Mathf.Max(8f, Screen.width - 226f), 12f, 218f, 30f),
            $"Infinite stamina  {remaining / 60:00}:{remaining % 60:00}");
    }

    private void OnValidate()
    {
        maxStamina = Mathf.Max(.01f, maxStamina);
        shovelActionCost = Mathf.Max(0f, shovelActionCost);
        workConsumptionPerSecond = Mathf.Max(0f, workConsumptionPerSecond);
        sprintConsumptionPerSecond = Mathf.Max(0f, sprintConsumptionPerSecond);
        recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
        recoveryDelay = Mathf.Max(0f, recoveryDelay);
        hungryPercent = Mathf.Clamp(hungryPercent, 0f, 100f);
        hungerIncreasePerMinute = Mathf.Max(0f, hungerIncreasePerMinute);
        // OnValidate may run during asset import: do not load Resources here.
        float validatedMax = upgradeSettings != null && upgradeSettings.IsConfigured
            ? upgradeSettings.Capacity(SaveSystem.Data?.staminaUpgradeLevel ?? 0) : maxStamina;
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, validatedMax * (1f - hungryPercent / 100f));
    }
}
