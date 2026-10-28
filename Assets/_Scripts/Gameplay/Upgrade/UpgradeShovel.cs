using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

public class UpgradeShovel : MonoBehaviour
{
    [SerializeField] private ShovelUpgradeSettings settings;
    [SerializeField] private Image shovelImage;
    [SerializeField] private Text currentLevelText;
    [SerializeField] private GameObject[] upgradeDots;
    [SerializeField] private Text scopeModification;
    [SerializeField] private Text weightModification;
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
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");

    private void OnEnable()
    {
        if (settings == null) settings = Resources.Load<ShovelUpgradeSettings>(ShovelUpgradeSettings.ResourcePath);
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

    public void Upgrade() => TryUpgrade();

    public bool TryUpgrade()
    {
        if (!isActiveAndEnabled || SaveSystem.Data == null || settings == null || !settings.IsConfigured) return false;
        int level = SaveSystem.Data.shovelUpgradeLevel;
        if (level < 0 || level >= settings.MaxUpgrades) return false;
        int cost = settings.Price(level);
        bool purchased = cost >= 0 && SaveSystem.TryPurchaseShovelUpgrade(cost, level, settings.MaxUpgrades);
        Refresh();
        return purchased;
    }

    public void Refresh()
    {
        bool configured = settings != null && settings.IsConfigured;
        int level = configured ? settings.ClampLevel(SaveSystem.Data?.shovelUpgradeLevel ?? 0) : 0;
        bool maxed = configured && level >= settings.MaxUpgrades;
        int cost = configured ? settings.Price(level) : 0;
        bool available = configured && SaveSystem.Data != null && !maxed && cost >= 0;
        int missing = available ? Mathf.Max(0, cost - SaveSystem.Money) : 0;
        bool affordable = available && missing == 0;

        if (configured)
        {
            var shovel = settings.shovels[settings.TypeIndex(level)];
            if (shovelImage != null) shovelImage.sprite = shovel.image;
            if (currentLevelText != null) currentLevelText.text = $"Level {settings.TypeIndex(level) + 1}";
            if (upgradeDots != null)
                for (int i = 0; i < upgradeDots.Length; i++)
                    if (upgradeDots[i] != null) upgradeDots[i].SetActive(i < settings.FilledDots(level));
            int next = Mathf.Min(level + 1, settings.MaxUpgrades);
            SetModification(scopeModification, settings.Capacity(level), settings.Capacity(next), maxed);
            SetModification(weightModification, settings.Weight(level), settings.Weight(next), maxed);
        }
        if (price != null) price.text = maxed ? "MAX" : configured ? cost == 0 ? "Free" : $"$ {cost:N0}" : "Unavailable";
        if (notEnoughMoney != null) notEnoughMoney.text = missing > 0 ? $"You need ${missing:N0} more." : "";
        if (notEnoughMoneyPopup != null) notEnoughMoneyPopup.SetActive(missing > 0);
        if (upgradeButton != null) upgradeButton.interactable = affordable;
        if (upgradeButtonCanvas != null) upgradeButtonCanvas.alpha = affordable ? 1f : .5f;
        UpdateButtonMaterial(affordable ? 1f : .5f);
    }

    private void SetModification(Text label, float current, float next, bool maxed)
    {
        if (label == null) return;
        label.supportRichText = true;
        string value = current.ToString("0.##", CultureInfo.InvariantCulture);
        label.text = maxed ? value + " kg (MAX)" :
            $"{value}  ➜  <color=#{ColorUtility.ToHtmlStringRGBA(nextLevelColor)}>{next.ToString("0.##", CultureInfo.InvariantCulture)}</color> kg";
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
        // A Mask can supply a cached stencil material; keep that copy in sync too.
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
