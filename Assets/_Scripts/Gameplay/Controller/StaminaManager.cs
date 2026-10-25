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
    public float NormalizedStamina => maxStamina > 0f ? Mathf.Clamp01(CurrentStamina / maxStamina) : 0f;
    public bool IsExhausted => CurrentStamina <= 0f;
    public bool IsWorking { get; private set; }

    private void Awake()
    {
        CurrentStamina = maxStamina;
        UpdateStaminaBar();
    }

    public void ConsumeShovelStamina()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || shovelActionCost <= 0f) return;
        CurrentStamina = Mathf.Clamp(CurrentStamina - shovelActionCost, 0f, maxStamina);
        recoveryDelayRemaining = recoveryDelay;
        UpdateStaminaBar();
    }

    public void Tick(bool isWorking, float deltaTime, bool recoveryBlocked = false)
    {
        if (!isActiveAndEnabled || !(deltaTime > 0f) || float.IsInfinity(deltaTime)) return;
        IsWorking = isWorking;
        if (isWorking)
        {
            CurrentStamina = Mathf.Clamp(CurrentStamina - workConsumptionPerSecond * deltaTime, 0f, maxStamina);
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
