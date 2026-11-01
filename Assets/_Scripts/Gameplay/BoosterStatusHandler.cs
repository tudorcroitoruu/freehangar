using System;
using UnityEngine;
using UnityEngine.UI;

public class BoosterStatusHandler : MonoBehaviour
{
    [SerializeField] private BoosterStatusSettings[] settings;

    [SerializeField] private Image boosterBg;
    [SerializeField] private Image boosterIcon;
    [SerializeField] private BuriedFind.Kind kind;
    private ChestReward reward;
    private long expiresAt;
    private double countdownSeconds;
    private bool initialized;
    private Color outlineColor;
    private Material originalMaterial, runtimeMaterial;
    private static readonly int OutlineId = Shader.PropertyToID("_Outline");
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int UseOutlineFillId = Shader.PropertyToID("_UseOutlineFill");
    private static readonly int OutlineFillAmountId = Shader.PropertyToID("_OutlineFillAmount");
    private static readonly int OutlineFillDirectionId = Shader.PropertyToID("_OutlineFillDirection");
    private static double Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;

    public void Initialize(ChestReward booster, long until)
    {
        reward = booster;
        BoosterStatusSettings appearance = null;
        if (settings != null)
            foreach (var entry in settings)
                if (entry != null && (entry.kind == BuriedFind.Kind.InfiniteStamina
                        ? ChestReward.InfiniteStamina : entry.boosterReward) == reward)
                {
                    appearance = entry;
                    // Prefer the generic PerkBottle configuration over legacy entries.
                    if (entry.kind == BuriedFind.Kind.PerkBottle) break;
                }
        if (appearance != null)
        {
            kind = appearance.kind;
            outlineColor = appearance.outlineTimerColor;
            if (boosterIcon != null) boosterIcon.sprite = appearance.icon;
        }
        else
        {
            outlineColor = Color.white;
            Debug.LogWarning("Missing booster UI settings for " + reward, this);
        }
        if (boosterBg != null && runtimeMaterial == null)
        {
            originalMaterial = boosterBg.material;
            if (originalMaterial != null && originalMaterial.HasProperty(OutlineFillAmountId))
            {
                runtimeMaterial = new Material(originalMaterial) { hideFlags = HideFlags.DontSave };
                boosterBg.material = runtimeMaterial;
            }
        }
        initialized = true;
        expiresAt = 0;
        SetExpiry(until);
        UpdateTimer();
    }

    public void SetExpiry(long until)
    {
        if (expiresAt == until) return;
        expiresAt = until;
        // Repeated rewards extend the existing indicator and refill its countdown.
        countdownSeconds = Math.Max(.001d, until - Now);
    }

    private void Update() => UpdateTimer();

    private void UpdateTimer()
    {
        if (!initialized || boosterBg == null || runtimeMaterial == null) return;
        float fill = Mathf.Clamp01((float)((expiresAt - Now) / countdownSeconds));
        ApplyMaterial(runtimeMaterial, fill);
        var renderedMaterial = boosterBg.materialForRendering;
        if (renderedMaterial != null && renderedMaterial != runtimeMaterial)
            ApplyMaterial(renderedMaterial, fill);
        boosterBg.SetMaterialDirty();
    }

    private void ApplyMaterial(Material material, float fill)
    {
        if (material.HasProperty(OutlineId)) material.SetFloat(OutlineId, 1f);
        if (material.HasProperty(OutlineColorId)) material.SetColor(OutlineColorId, outlineColor);
        if (material.HasProperty(UseOutlineFillId)) material.SetFloat(UseOutlineFillId, 1f);
        if (material.HasProperty(OutlineFillDirectionId)) material.SetFloat(OutlineFillDirectionId, 4f);
        if (material.HasProperty(OutlineFillAmountId)) material.SetFloat(OutlineFillAmountId, fill);
    }

    public static long Expiry(PlayerData data, ChestReward booster)
    {
        if (data == null) return 0;
        switch (booster)
        {
            case ChestReward.InfiniteStamina: return data.infiniteStaminaUntilUtc;
            case ChestReward.NoAttacks: return data.noAttacksUntilUtc;
            case ChestReward.Mobility: return data.mobilityUntilUtc;
            case ChestReward.DoubleSellCash: return data.doubleSellCashUntilUtc;
            default: return 0;
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null) return;
        if (boosterBg != null && boosterBg.material == runtimeMaterial) boosterBg.material = originalMaterial;
        if (Application.isPlaying) Destroy(runtimeMaterial);
        else DestroyImmediate(runtimeMaterial);
    }
}

[Serializable]
public class BoosterStatusSettings
{
    public BuriedFind.Kind kind;
    public ChestReward boosterReward;
    public Sprite icon;
    public Color outlineTimerColor;
}
