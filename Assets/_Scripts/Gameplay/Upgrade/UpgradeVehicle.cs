using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class UpgradeVehicle : MonoBehaviour
{
    [SerializeField] private GameObject currentLevelHolder;
    [SerializeField] private GameObject nextLevelHolder;
    [SerializeField] private GameObject maxLevelHolder;
    
    [Space, Header("Upgrade Data")]
    [SerializeField] private Image[] vehicleImages;
    [FormerlySerializedAs("currentLevelCapacity")]
    [SerializeField] private Text currentLevelMovementSpeed;
    [FormerlySerializedAs("nextLevelCapacity")]
    [SerializeField] private Text nextLevelMovementSpeed;
    [FormerlySerializedAs("maxLevelCapacity")]
    [SerializeField] private Text maxLevelMovementSpeed;
    [SerializeField] private Text currentLevelSlots;
    [SerializeField] private Text nextLevelSlots;
    [SerializeField] private Text maxLevelSlots;
    [SerializeField] private Color nextLevelColor = Color.forestGreen;
    [Header("Vehicle presentation")]

    [SerializeField] private Text currentLevelText;
    [SerializeField] private Text nextVehicleText;
    
    [Space, Header("Price")]
    [SerializeField] private Text price;
    [SerializeField] private Text notEnoughMoneyTitle;
    [SerializeField] private Text notEnoughMoney;
    [SerializeField] private GameObject notEnoughMoneyPopup;
    [SerializeField] private GameObject keyParent;
    [SerializeField] private Image keyImage;
    
    [Space, Header("Upgrade Button")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private CanvasGroup upgradeButtonCanvas;
    [SerializeField] private Image upgradeButtonImage;
    private VehicleUpgrade target;
    private bool waitingForUpgrade;
    private Material originalButtonMaterial, buttonMaterial;
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    public void SetTarget(VehicleUpgrade vehicle) { target = vehicle; Refresh(); }

    private void OnEnable()
    {
        if (upgradeButton != null) upgradeButton.onClick.AddListener(Upgrade);
        UpgradeButtonFeedback.Bind(upgradeButton, this);
        SaveSystem.MoneyChanged += OnMoneyChanged;
        VehicleUpgrade.VehicleReplaced += OnVehicleReplaced;
        Refresh();
    }
    private void OnDisable()
    {
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(Upgrade);
        SaveSystem.MoneyChanged -= OnMoneyChanged;
        VehicleUpgrade.VehicleReplaced -= OnVehicleReplaced;
    }
    private void OnMoneyChanged(int _) => Refresh();
    private void Update()
    {
        if (waitingForUpgrade && !SaveSystem.UpgradePurchaseLocked) Refresh();
    }
    private void OnVehicleReplaced(VehicleUpgrade previous, VehicleUpgrade next)
    {
        if (target == previous) SetTarget(next);
    }
    public void Upgrade()
    {
        if (!isActiveAndEnabled || SaveSystem.UpgradePurchaseLocked) return;
        bool purchased = TryUpgrade();
        GameplayAudio.Instance?.UpgradeResult(purchased);
    }
    public bool TryUpgrade()
    {
        if (!isActiveAndEnabled || SaveSystem.UpgradePurchaseLocked || target == null) return false;
        bool purchased = target.TryUpgrade();
        Refresh();
        return purchased;
    }
    public void Refresh()
    {
        bool configured = target != null && target.IsConfigured;
        bool maxed = configured && target.IsMaxed;
        bool available = configured && target.IsUnlocked && target.isActiveAndEnabled && !target.HasCargo && SaveSystem.Data != null && !maxed;
        int cost = configured && !target.UsesVoucher ? target.Price : 0;
        int missing = available ? Mathf.Max(0, cost - SaveSystem.Money) : 0;
        bool missingKey = configured && !target.HasRequiredKey;
        bool affordable = available && missing == 0 && !missingKey;
        waitingForUpgrade = SaveSystem.UpgradePurchaseLocked;
        bool canUpgrade = affordable && !waitingForUpgrade;
        if (currentLevelHolder != null) currentLevelHolder.SetActive(configured && !maxed);
        if (nextLevelHolder != null) nextLevelHolder.SetActive(configured && !maxed);
        if (maxLevelHolder != null) maxLevelHolder.SetActive(maxed);
        SetText(currentLevelText, configured ? target.Profile.displayName : "");
        var nextVehicle = configured ? target.NextVehicle : null;
        bool showKey = nextVehicle != null && nextVehicle.requiresKey;
        if (keyParent != null) keyParent.SetActive(showKey);
        if (keyImage != null)
        {
            keyImage.sprite = showKey ? nextVehicle.keySprite : null;
            keyImage.enabled = showKey && nextVehicle.keySprite != null;
        }
        SetText(nextVehicleText, nextVehicle != null ? nextVehicle.displayName : configured ? "MAX" : "");
        if (configured)
        {
            var current = target.Profile;
            var next = nextVehicle ?? current;
            var maximum = current;
            SetText(currentLevelMovementSpeed, $"Move speed: <b>{Speed(current.movementSpeed)}</b>");
            string nextSpeed = Speed(next.movementSpeed);
            if (next.movementSpeed != current.movementSpeed) nextSpeed = Colored(nextSpeed);
            SetText(nextLevelMovementSpeed, $"Move speed: <b>{nextSpeed}</b>");
            SetText(maxLevelMovementSpeed, $"Move speed: <b>{Speed(maximum.movementSpeed)}</b>");
            SetText(currentLevelSlots, $"Slots: <b>{current.slots}</b>");
            string nextSlots = next.slots.ToString();
            if (next.slots != current.slots) nextSlots = Colored(nextSlots);
            SetText(nextLevelSlots, $"Slots: <b>{nextSlots}</b>");
            SetText(maxLevelSlots, $"Slots: <b>{maximum.slots}</b>");
            if (vehicleImages != null)
                for (int i = 0; i < vehicleImages.Length; i++)
                {
                    Sprite image = i == 0 ? current.image : i == 1 ? next.image : maximum.image;
                    if (vehicleImages[i] != null && image != null) vehicleImages[i].sprite = image;
                }
        }
        SetText(price, configured && !target.IsUnlocked ? "Locked" : maxed ? "MAX" : configured ? cost == 0 ? "Free" : $"$ {EconomyNumberFormat.Accounting(cost)}" : "Unavailable");
        string requirementsTitle = missingKey && missing > 0 ? "Requirements not met."
            : missingKey ? "Vehicle key required."
            : missing > 0 ? "Not enough money" : "";
        string requirementsDetails = missingKey && missing > 0 ? "Not enough money and vehicle key missing."
            : missing > 0 ? $"$ {EconomyNumberFormat.Accounting(missing)}" : "";
        SetText(notEnoughMoneyTitle, requirementsTitle);
        SetText(notEnoughMoney, requirementsDetails);
        if (notEnoughMoneyTitle != null) notEnoughMoneyTitle.gameObject.SetActive(missing > 0 || missingKey);
        if (notEnoughMoney != null) notEnoughMoney.gameObject.SetActive(missing > 0);
        if (notEnoughMoneyPopup != null) notEnoughMoneyPopup.SetActive(missing > 0 || missingKey);
        if (upgradeButton != null) upgradeButton.interactable = canUpgrade;
        if (upgradeButtonCanvas != null) upgradeButtonCanvas.alpha = canUpgrade ? 1f : .5f;
        UpdateButtonMaterial(canUpgrade ? 1f : .5f);
    }
    private static string Speed(float value) => EconomyNumberFormat.Accounting(value * 3.6f) + " km/h";
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
