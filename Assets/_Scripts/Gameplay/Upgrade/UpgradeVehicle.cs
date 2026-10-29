using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

public class UpgradeVehicle : MonoBehaviour
{
    [SerializeField] private GameObject currentLevelHolder;
    [SerializeField] private GameObject nextLevelHolder;
    [SerializeField] private GameObject maxLevelHolder;
    
    [Space, Header("Upgrade Data")]
    [SerializeField] private Image[] vehicleImages;
    [SerializeField] private Text currentLevelCapacity;
    [SerializeField] private Text nextLevelCapacity;
    [SerializeField] private Text maxLevelCapacity;
    [SerializeField] private Text currentLevelSlots;
    [SerializeField] private Text nextLevelSlots;
    [SerializeField] private Text maxLevelSlots;
    [SerializeField] private Color nextLevelColor = Color.forestGreen;
    
    [Space, Header("Price")]
    [SerializeField] private Text price;
    [SerializeField] private Text notEnoughMoney;
    [SerializeField] private GameObject notEnoughMoneyPopup;
    
    [Space, Header("Upgrade Button")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private CanvasGroup upgradeButtonCanvas;
    [SerializeField] private Image upgradeButtonImage;
    private VehicleUpgrade target;
    private Material originalButtonMaterial, buttonMaterial;
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    public void SetTarget(VehicleUpgrade vehicle) { target = vehicle; Refresh(); }

    private void OnEnable()
    {
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
        if (!isActiveAndEnabled || target == null) return false;
        bool purchased = target.TryUpgrade();
        Refresh();
        return purchased;
    }
    public void Refresh()
    {
        bool configured = target != null && target.IsConfigured;
        bool maxed = configured && target.IsMaxed;
        bool available = configured && target.isActiveAndEnabled && SaveSystem.Data != null && !maxed;
        int cost = configured && !target.UsesVoucher ? target.Price : 0;
        int missing = available ? Mathf.Max(0, cost - SaveSystem.Money) : 0;
        bool affordable = available && missing == 0;
        if (currentLevelHolder != null) currentLevelHolder.SetActive(configured && !maxed);
        if (nextLevelHolder != null) nextLevelHolder.SetActive(configured && !maxed);
        if (maxLevelHolder != null) maxLevelHolder.SetActive(maxed);
        if (configured)
        {
            var current = target.Profile.At(target.Level);
            var next = target.Profile.At(target.Level + 1);
            var maximum = target.Profile.At(target.Profile.MaxLevel);
            SetText(currentLevelCapacity, Volume(current.capacityCubicMeters));
            SetText(nextLevelCapacity, Colored(Volume(next.capacityCubicMeters)));
            SetText(maxLevelCapacity, Volume(maximum.capacityCubicMeters));
            SetText(currentLevelSlots, current.slots.ToString());
            SetText(nextLevelSlots, Colored(next.slots.ToString()));
            SetText(maxLevelSlots, maximum.slots.ToString());
            if (vehicleImages != null)
                for (int i = 0; i < vehicleImages.Length; i++)
                {
                    Sprite image = i == 0 ? current.image : i == 1 ? next.image : maximum.image;
                    if (vehicleImages[i] != null && image != null) vehicleImages[i].sprite = image;
                }
        }
        SetText(price, maxed ? "MAX" : configured ? cost == 0 ? "Free" : $"$ {cost:N0}" : "Unavailable");
        SetText(notEnoughMoney, missing > 0 ? $"You need ${missing:N0} more." : "");
        if (notEnoughMoneyPopup != null) notEnoughMoneyPopup.SetActive(missing > 0);
        if (upgradeButton != null) upgradeButton.interactable = affordable;
        if (upgradeButtonCanvas != null) upgradeButtonCanvas.alpha = affordable ? 1f : .5f;
        UpdateButtonMaterial(affordable ? 1f : .5f);
    }
    private static string Volume(float value) => value.ToString("0.##", CultureInfo.InvariantCulture) + " m\u00b3";
    private string Colored(string value) => $"<color=#{ColorUtility.ToHtmlStringRGBA(nextLevelColor)}>{value}</color>";
    private static void SetText(Text label, string value)
    {
        if (label == null) return;
        label.supportRichText = true;
        label.text = value;
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
        var rendered = upgradeButtonImage.materialForRendering;
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
