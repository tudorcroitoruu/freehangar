using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tracks work, recovery and the stamina capacity lost to hunger.</summary>
[DisallowMultipleComponent]
public sealed class StaminaManager : MonoBehaviour
{
    public float SavedHunger => hungryPercent;
    public void RestoreSave(float stamina, float hunger)
    {
        pendingHungerReduction = 0f;
        SetHungerPercent(hunger);
        if (float.IsFinite(stamina) && stamina >= 0) CurrentStamina = Mathf.Clamp(stamina, 0, CurrentMaxStamina);
        UpdateStaminaBar();
        UpdateHungryIcon();
    }
    [SerializeField, Min(.01f)] private float maxStamina = 100f;
    [SerializeField] private StaminaUpgradeSettings upgradeSettings;
    [SerializeField, Min(0f), Tooltip("Stamina per scoop for a 1 kg shovel; multiplied by the equipped shovel's weight.")]
    private float shovelActionCost = 10f;
    [SerializeField, Min(0f), Tooltip("Stamina required and consumed by one successful jump.")]
    private float jumpActionCost = 10f;
    [Tooltip("Stamina consumed per second while carrying a bag at the reference grain weight.")]
    [SerializeField, Min(0f)] private float workConsumptionPerSecond = 10f;
    [SerializeField, Min(.01f), Tooltip("Grain kg at which bag carrying consumes the full work rate. Lighter/heavier loads scale proportionally.")]
    private float bagReferenceWeightKg = 50f;
    [SerializeField, Min(0f), Tooltip("Additional stamina consumed per second while sprinting.")]
    private float sprintConsumptionPerSecond = 10f;
    [SerializeField, Min(0f)] private float recoveryPerSecond = 15f;
    [SerializeField, Min(0f)] private float recoveryDelay = 2f;

    [Header("Hunger")]
    [SerializeField, Range(0f, 100f), Tooltip("Maximum hunger percentage. At 70%, the player retains 30% of their maximum stamina capacity.")]
    private float maxHungryPercent = 70f;
    [SerializeField, Range(0f, 100f)] private float hungryPercent;
    [SerializeField, Min(0f), Tooltip("Percentage points of hunger gained per minute of game time. Zero pauses hunger growth.")]
    private float hungerIncreasePerMinute = 1f;

    [SerializeField] private GameObject hungryIcon;
    [SerializeField] private float hungryPercentToShowIcon = 15f;

    [Header("UI")]
    [SerializeField] private Image staminaBarFill;
    [SerializeField] private Image staminaBarBackground;

    private float recoveryDelayRemaining;
    private float pendingHungerReduction, hungerReductionPerSecond;
    private RectTransform backgroundRect;
    private float initialBackgroundSizeDeltaX;
    private PlayerData staminaProfile;
    private Material originalFillMaterial, fillMaterial;
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int UseFillId = Shader.PropertyToID("_UseFill");
    public float CurrentStamina { get; private set; }
    private StaminaUpgradeSettings UpgradeSettings
    {
        get
        {
            upgradeSettings = GrainEconomySettings.ResolveStamina(upgradeSettings);
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
        SetHungerPercent(hungryPercent);
        CurrentStamina = CurrentMaxStamina;
        UpdateStaminaBar();
    }

    private void OnEnable()
    {
        SaveSystem.MoneyChanged += RefreshUpgrade;
        RefreshUpgrade(SaveSystem.Money);
        InvokeRepeating(nameof(UpdateHungryIcon), 0f, 5f);
    }

    private void OnDisable()
    {
        SaveSystem.MoneyChanged -= RefreshUpgrade;
        CancelInvoke(nameof(UpdateHungryIcon));
    }

    private void UpdateHungryIcon()
    {
        if (hungryIcon == null) return;
        bool showIcon = hungryPercent >= hungryPercentToShowIcon;
        if (hungryIcon.activeSelf != showIcon) hungryIcon.SetActive(showIcon);
    }

    private void RefreshUpgrade(int _)
    {
        bool profileChanged = staminaProfile != SaveSystem.Data;
        if (profileChanged) pendingHungerReduction = 0f;
        staminaProfile = SaveSystem.Data;
        CurrentStamina = profileChanged || HasInfiniteStamina ? CurrentMaxStamina : Mathf.Clamp(CurrentStamina, 0f, CurrentMaxStamina);
        UpdateStaminaBar();
    }

    private void Update()
    {
        TickHunger(Time.deltaTime);
        if (pendingHungerReduction <= 0f || Time.deltaTime <= 0f) return;
        float reduction = Mathf.Min(pendingHungerReduction, hungerReductionPerSecond * Time.deltaTime);
        pendingHungerReduction = Mathf.Max(0f, pendingHungerReduction - reduction);
        ApplyFoodHungerReduction(reduction);
    }

    public void RestoreStaminaAndReduceHunger(float reductionPercent, float duration)
    {
        CurrentStamina = CurrentMaxStamina;
        if (float.IsFinite(reductionPercent) && reductionPercent > 0f)
        {
            if (!float.IsFinite(duration) || duration <= 0f)
                ApplyFoodHungerReduction(reductionPercent);
            else
            {
                pendingHungerReduction = Mathf.Min(hungryPercent, pendingHungerReduction + reductionPercent);
                hungerReductionPerSecond = pendingHungerReduction / duration;
            }
        }
        UpdateStaminaBar();
    }

    private void ApplyFoodHungerReduction(float reduction)
    {
        float previousMax = CurrentMaxStamina;
        SetHungerPercent(hungryPercent - reduction);
        CurrentStamina = Mathf.Clamp(CurrentStamina + CurrentMaxStamina - previousMax, 0f, CurrentMaxStamina);
        UpdateStaminaBar();
        UpdateHungryIcon();
    }

    public void TickHunger(float deltaTime)
    {
        if (!isActiveAndEnabled || HasInfiniteStamina || !(deltaTime > 0f) || !float.IsFinite(deltaTime)) return;
        SetHungerPercent(hungryPercent + hungerIncreasePerMinute * (deltaTime / 60f));
    }

    public void SetHungerPercent(float percent)
    {
        if (!float.IsFinite(percent)) return;
        hungryPercent = Mathf.Clamp(percent, 0f, Mathf.Clamp(maxHungryPercent, 0f, 100f));
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

    public bool TryConsumeJumpStamina()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || CurrentMaxStamina <= 0f) return false;
        if (HasInfiniteStamina) return true;
        if (CurrentStamina < jumpActionCost) return false;
        CurrentStamina = Mathf.Max(0f, CurrentStamina - jumpActionCost);
        recoveryDelayRemaining = recoveryDelay;
        UpdateStaminaBar();
        return true;
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
        CurrentStamina = Mathf.Clamp(CurrentStamina + recoveryPerSecond * 1.5f * restingTime, 0f, CurrentMaxStamina);
        UpdateStaminaBar();
    }

    public void TickBagCarry(float grainKg, float deltaTime, bool sprinting = false)
    {
        bool loaded = float.IsFinite(grainKg) && grainKg > 0f;
        float rate = loaded ? workConsumptionPerSecond * grainKg / Mathf.Max(.01f, bagReferenceWeightKg) : 0f;
        Tick(loaded, deltaTime, consumptionPerSecond: rate, sprinting: sprinting);
    }

    private void UpdateStaminaBar()
    {
        UpdateFillMaterial();
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

    private void UpdateFillMaterial()
    {
        if (staminaBarFill == null) return;
        if (fillMaterial == null)
        {
            originalFillMaterial = staminaBarFill.material;
            if (originalFillMaterial == null || !originalFillMaterial.HasProperty(FillAmountId)) return;
            // Stamina must not change other UI elements sharing the material asset.
            fillMaterial = new Material(originalFillMaterial) { hideFlags = HideFlags.DontSave };
            staminaBarFill.material = fillMaterial;
        }
        fillMaterial.SetFloat(FillAmountId, NormalizedStamina);
        if (fillMaterial.HasProperty(UseFillId)) fillMaterial.SetFloat(UseFillId, 1f);
        // UI Mask can render a separate, cached stencil material.
        var renderedMaterial = staminaBarFill.materialForRendering;
        if (renderedMaterial != null && renderedMaterial != fillMaterial && renderedMaterial.HasProperty(FillAmountId))
        {
            renderedMaterial.SetFloat(FillAmountId, NormalizedStamina);
            if (renderedMaterial.HasProperty(UseFillId)) renderedMaterial.SetFloat(UseFillId, 1f);
        }
        staminaBarFill.SetMaterialDirty();
    }

    private void OnDestroy()
    {
        if (fillMaterial == null) return;
        if (staminaBarFill != null && staminaBarFill.material == fillMaterial)
            staminaBarFill.material = originalFillMaterial;
        if (Application.isPlaying) Destroy(fillMaterial);
        else DestroyImmediate(fillMaterial);
    }

    private void OnValidate()
    {
        maxStamina = Mathf.Max(.01f, maxStamina);
        shovelActionCost = Mathf.Max(0f, shovelActionCost);
        workConsumptionPerSecond = Mathf.Max(0f, workConsumptionPerSecond);
        sprintConsumptionPerSecond = Mathf.Max(0f, sprintConsumptionPerSecond);
        recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
        recoveryDelay = Mathf.Max(0f, recoveryDelay);
        maxHungryPercent = Mathf.Clamp(maxHungryPercent, 0f, 100f);
        hungryPercent = Mathf.Clamp(hungryPercent, 0f, maxHungryPercent);
        hungerIncreasePerMinute = Mathf.Max(0f, hungerIncreasePerMinute);
        // OnValidate may run during asset import: do not load Resources here.
        float validatedMax = upgradeSettings != null && upgradeSettings.IsConfigured
            ? upgradeSettings.Capacity(SaveSystem.Data?.staminaUpgradeLevel ?? 0) : maxStamina;
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, validatedMax * (1f - hungryPercent / 100f));
    }
}
