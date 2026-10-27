using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tracks stamina spent while working and recovered while resting.</summary>
[DisallowMultipleComponent]
public sealed class StaminaManager : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float shovelActionCost = 10f;
    [Tooltip("Stamina consumed per second while carrying a bag.")]
    [SerializeField, Min(0f)] private float workConsumptionPerSecond = 10f;
    [SerializeField, Min(0f)] private float recoveryPerSecond = 15f;
    [SerializeField, Min(0f)] private float recoveryDelay = 2f;
    [SerializeField] private Image staminaBarFill;

    private float recoveryDelayRemaining;
    public float CurrentStamina { get; private set; }
    public float MaxStamina => maxStamina;
    public bool HasInfiniteStamina => SaveSystem.Data != null &&
        SaveSystem.Data.infiniteStaminaUntilUtc > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public long InfiniteStaminaSecondsRemaining => SaveSystem.Data == null ? 0L :
        Math.Max(0L, SaveSystem.Data.infiniteStaminaUntilUtc - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    public float NormalizedStamina => HasInfiniteStamina ? 1f : maxStamina > 0f ? Mathf.Clamp01(CurrentStamina / maxStamina) : 0f;
    public bool IsExhausted => !HasInfiniteStamina && CurrentStamina <= 0f;
    public bool IsWorking { get; private set; }

    private void Awake()
    {
        CurrentStamina = maxStamina;
        UpdateStaminaBar();
    }

    public void ConsumeShovelStamina()
    {
        if (HasInfiniteStamina) { CurrentStamina = maxStamina; UpdateStaminaBar(); return; }
        if (!isActiveAndEnabled || Time.timeScale <= 0f || shovelActionCost <= 0f) return;
        CurrentStamina = Mathf.Clamp(CurrentStamina - shovelActionCost, 0f, maxStamina);
        recoveryDelayRemaining = recoveryDelay;
        UpdateStaminaBar();
    }

    public void Tick(bool isWorking, float deltaTime, bool recoveryBlocked = false, float consumptionPerSecond = -1f)
    {
        if (!isActiveAndEnabled || !(deltaTime > 0f) || float.IsInfinity(deltaTime)) return;
        IsWorking = isWorking;
        if (HasInfiniteStamina)
        {
            CurrentStamina = maxStamina;
            recoveryDelayRemaining = 0f;
            UpdateStaminaBar();
            return;
        }
        if (isWorking)
        {
            CurrentStamina = Mathf.Clamp(CurrentStamina - (consumptionPerSecond >= 0f && float.IsFinite(consumptionPerSecond) ? consumptionPerSecond : workConsumptionPerSecond) * deltaTime, 0f, maxStamina);
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
        CurrentStamina = Mathf.Clamp(CurrentStamina + recoveryPerSecond * restingTime, 0f, maxStamina);
        UpdateStaminaBar();
    }

    private void UpdateStaminaBar()
    {
        if (staminaBarFill != null) staminaBarFill.fillAmount = NormalizedStamina;
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
        recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
        recoveryDelay = Mathf.Max(0f, recoveryDelay);
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0f, maxStamina);
    }
}
