using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Blends the authored stamina look over normal rendering for the saved perk's lifetime.</summary>
public sealed class InfiniteStaminaVolume : MonoBehaviour
{
    [SerializeField] private Volume staminaVolume;
    [SerializeField, Min(.05f)] private float transitionSeconds = 1.2f;
    public float Blend => staminaVolume != null ? staminaVolume.weight : 0;
    private void OnEnable()
    {
        if (staminaVolume == null) return;
        staminaVolume.gameObject.SetActive(true);
        staminaVolume.weight = 0;
    }
    private void Update()
    {
        if (staminaVolume == null) return;
        bool active = SaveSystem.Data != null && SaveSystem.Data.infiniteStaminaUntilUtc > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        staminaVolume.weight = Mathf.MoveTowards(staminaVolume.weight, active ? 1 : 0, Time.unscaledDeltaTime / transitionSeconds);
    }
    private void OnDisable() { if (staminaVolume != null) staminaVolume.weight = 0; }
}
