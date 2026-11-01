using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

public class UpgradeStamina : MonoBehaviour
{
    [SerializeField] private StaminaUpgradeSettings settings;
    [SerializeField] private Text currentLevelText;
    [SerializeField] private Text staminaModification;
    [SerializeField] private Color nextLevelColor = Color.forestGreen;

    [Space, Header("Price")]
    [SerializeField] private Text price;
    [SerializeField] private Text notEnoughMoney;

    [SerializeField] private GameObject notEnoughMoneyPopup;
    
    [Space, Header("Upgrade Button")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private CanvasGroup upgradeButtonCanvas;
    [SerializeField] private Image upgradeButtonImage;
    private Material originalButtonMaterial, buttonMaterial;
    private bool waitingForUpgrade;
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");

    private void OnEnable()
    {
        settings = GrainEconomySettings.ResolveStamina(settings);
        if (upgradeButton != null) upgradeButton.onClick.AddListener(Upgrade);
        SaveSystem.MoneyChanged += OnMoneyChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(Upgrade);
        SaveSystem.MoneyChanged -= OnMoneyChanged;
    }

    private void OnMoneyChanged(int _) => Refresh();
    private void Update()
    {
        if (waitingForUpgrade && !SaveSystem.UpgradePurchaseLocked) Refresh();
    }
    public void Upgrade() => TryUpgrade();

    public bool TryUpgrade()
    {
        if (Application.isPlaying) settings = GrainEconomySettings.ResolveStamina(settings);
        if (!isActiveAndEnabled || SaveSystem.UpgradePurchaseLocked || SaveSystem.Data == null || settings == null || !settings.IsConfigured) return false;
        int level = SaveSystem.Data.staminaUpgradeLevel;
        if (level < 0 || level >= settings.MaxUpgrades) return false;
        bool purchased = SaveSystem.TryPurchaseStaminaUpgrade(settings.Price(level), level, settings.MaxUpgrades);
        Refresh();
        return purchased;
    }

    public void Refresh()
    {
        if (Application.isPlaying) settings = GrainEconomySettings.ResolveStamina(settings);
        bool configured = settings != null && settings.IsConfigured;
        int level = configured ? settings.ClampLevel(SaveSystem.Data?.staminaUpgradeLevel ?? 0) : 0;
        bool maxed = configured && level >= settings.MaxUpgrades;
        int cost = configured ? settings.Price(level) : 0;
        bool usesVoucher = cost > 0 && (SaveSystem.Data?.freeUpgradeVouchers ?? 0) > 0;
        bool available = configured && SaveSystem.Data != null && !maxed;
        int missing = available && !usesVoucher ? Mathf.Max(0, cost - SaveSystem.Money) : 0;
        bool affordable = available && missing == 0;
        waitingForUpgrade = SaveSystem.UpgradePurchaseLocked;
        bool canUpgrade = affordable && !waitingForUpgrade;
        if (configured)
        {
            if (currentLevelText != null) currentLevelText.text = $"Level {level + 1}";
            if (staminaModification != null)
            {
                staminaModification.supportRichText = true;
                string current = settings.Capacity(level).ToString("0.##", CultureInfo.InvariantCulture);
                string next = settings.Capacity(level + 1).ToString("0.##", CultureInfo.InvariantCulture);
                staminaModification.text = maxed ? current + " (MAX)" :
                    $"{current}  \u279c  <color=#{ColorUtility.ToHtmlStringRGBA(nextLevelColor)}>{next}</color>";
            }
        }
        if (price != null) price.text = maxed ? "MAX" : configured ? cost == 0 || usesVoucher ? "Free" : $"$ {EconomyNumberFormat.Accounting(cost)}" : "Unavailable";
        if (notEnoughMoney != null) notEnoughMoney.text = missing > 0 ? $"You need ${EconomyNumberFormat.Accounting(missing)} more." : "";
        if (notEnoughMoneyPopup != null) notEnoughMoneyPopup.SetActive(missing > 0);
        if (upgradeButton != null) upgradeButton.interactable = canUpgrade;
        if (upgradeButtonCanvas != null) upgradeButtonCanvas.alpha = canUpgrade ? 1f : .5f;
        UpdateButtonMaterial(canUpgrade ? 1f : .5f);
    }

    private void UpdateButtonMaterial(float saturation)
    {
        if (upgradeButtonImage == null) return;
        if (buttonMaterial == null)
        {
            originalButtonMaterial = upgradeButtonImage.material;
            if (originalButtonMaterial == null || !originalButtonMaterial.HasProperty(SaturationId)) return;
            buttonMaterial = new Material(originalButtonMaterial) { hideFlags = HideFlags.DontSave };
            upgradeButtonImage.material = buttonMaterial;
        }
        buttonMaterial.SetFloat(SaturationId, saturation);
        Material rendered = upgradeButtonImage.materialForRendering;
        if (rendered != null && rendered.HasProperty(SaturationId)) rendered.SetFloat(SaturationId, saturation);
        upgradeButtonImage.SetMaterialDirty();
    }

    private void OnDestroy()
    {
        if (buttonMaterial == null) return;
        if (upgradeButtonImage != null && upgradeButtonImage.material == buttonMaterial)
            upgradeButtonImage.material = originalButtonMaterial;
        if (Application.isPlaying) Destroy(buttonMaterial);
        else DestroyImmediate(buttonMaterial);
    }
}
