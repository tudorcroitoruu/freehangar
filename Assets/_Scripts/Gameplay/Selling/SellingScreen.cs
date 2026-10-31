using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class SellingScreen : MonoBehaviour
{
    [SerializeField] private SellGrainElement elementPrefab;
    [SerializeField] private Transform elementsParent;
    [Space] [SerializeField] private Text totalWeight;
    [SerializeField] private Text totalValue;
    [SerializeField] private Text totalValueButton;
    [Space] [SerializeField] private Button cancelButton;
    [SerializeField] private Button buyButton;
    private readonly List<SellGrainElement> elements = new List<SellGrainElement>();
    private readonly List<GrainContainer> containers = new List<GrainContainer>();
    private readonly List<GrainContainer> currentContainers = new List<GrainContainer>();
    private readonly Dictionary<GrainContainer, float> quotedWeights = new Dictionary<GrainContainer, float>();
    private readonly Dictionary<GrainContainer, GrainType> quotedTypes = new Dictionary<GrainContainer, GrainType>();
    private readonly Dictionary<GrainType, float> quotedPrices = new Dictionary<GrainType, float>();
    private PlayerInteractor player;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private int quotedCash;
    private static SellingScreen current;
    private static int closedFrame = -1;
    public SellingZone Zone { get; private set; }
    public bool IsOpen => Zone != null;
    public static bool BlocksGameplayInput => (current != null && current.IsOpen) || closedFrame == Time.frameCount;

    public bool Open(SellingZone zone, PlayerInteractor interactor)
    {
        if (!enabled || zone == null || !zone.CanWeigh(interactor) || elementPrefab == null || elementsParent == null ||
            UpgradeManager.BlocksGameplayInput || UIPopups.BasePopupUI.HasActivePopup ||
            (transform.parent != null && !transform.parent.gameObject.activeInHierarchy)) return false;
        Zone = zone;
        player = interactor;
        current = this;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        gameObject.SetActive(true);
        Refresh();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return true;
    }

    private void OnEnable()
    {
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        if (buyButton != null) buyButton.onClick.AddListener(Sell);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (!Zone.CanWeigh(player)) { Close(); return; }
        if (!Application.isFocused || UIPopups.BasePopupUI.HasActivePopup) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        else if (!QuoteMatches()) Refresh();
        else if (buyButton != null) buyButton.interactable = CanPay();
    }

    private void Refresh()
    {
        ClearElements();
        Zone.CollectContainers(containers);
        quotedWeights.Clear();
        quotedTypes.Clear();
        quotedPrices.Clear();
        var quantities = new SortedDictionary<GrainType, float>();
        double weight = 0, value = 0;
        foreach (var container in containers)
        {
            var grain = container.AcceptedGrain;
            quantities.TryGetValue(grain, out float kilograms);
            quantities[grain] = kilograms + container.Kilograms;
            quotedWeights[container] = container.Kilograms;
            quotedTypes[container] = grain;
        }
        foreach (var entry in quantities)
        {
            Zone.TryGetSettings(entry.Key, out var settings);
            quotedPrices[entry.Key] = settings.grainPrice;
            var element = Instantiate(elementPrefab, elementsParent);
            element.gameObject.SetActive(true);
            element.SetData(settings, entry.Value);
            elements.Add(element);
            weight += entry.Value;
            value += entry.Value * (double)settings.grainPrice;
        }
        // The wallet stores whole dollars; round down once after summing all crops.
        quotedCash = value >= 1 && value < (double)int.MaxValue + 1 ? (int)Math.Floor(value) : 0;
        if (totalWeight != null) totalWeight.text = $"{weight:0.#} kg";
        if (totalValue != null) totalValue.text = $"$ {quotedCash:N2}";
        if (totalValueButton != null) totalValueButton.text = $"SELL ALL - ${quotedCash:N2}";
        if (buyButton != null) buyButton.interactable = CanPay();
    }

    private bool CanPay() => containers.Count > 0 && quotedCash > 0 && SaveSystem.Data != null && SaveSystem.Money <= int.MaxValue - quotedCash;

    private bool QuoteMatches()
    {
        Zone.CollectContainers(currentContainers);
        if (currentContainers.Count != containers.Count) return false;
        foreach (var container in currentContainers)
            if (!quotedWeights.TryGetValue(container, out float weight) || weight != container.Kilograms ||
                quotedTypes[container] != container.AcceptedGrain ||
                !Zone.TryGetSettings(container.AcceptedGrain, out var settings) ||
                quotedPrices[container.AcceptedGrain] != settings.grainPrice) return false;
        return true;
    }

    public void Sell()
    {
        if (!IsOpen || !Zone.CanWeigh(player) || UIPopups.BasePopupUI.HasActivePopup) return;
        if (!QuoteMatches()) { Refresh(); return; }
        if (!CanPay() || !SaveSystem.AddMoney(quotedCash)) return;
        if (buyButton != null) buyButton.interactable = false;
        foreach (var container in containers)
        {
            var wheelbarrow = container.GetComponentInParent<WheelbarrowCargo>();
            container.transform.SetParent(null, true);
            if (wheelbarrow != null) wheelbarrow.NotifyBagTaken(container);
            container.gameObject.SetActive(false);
            Destroy(container.gameObject);
        }
        if (DataHolderUI.Instance != null) DataHolderUI.Instance.RefreshDataPlayer();
        Close();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        ReleaseInput();
    }

    private void ClearElements()
    {
        foreach (var element in elements)
            if (element != null) { element.gameObject.SetActive(false); Destroy(element.gameObject); }
        elements.Clear();
    }

    private void OnDisable()
    {
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Close);
        if (buyButton != null) buyButton.onClick.RemoveListener(Sell);
        ClearElements();
        ReleaseInput();
    }

    private void ReleaseInput()
    {
        if (current != this) return;
        current = null;
        Zone = null;
        player = null;
        closedFrame = Time.frameCount;
        bool restore = Application.isFocused && !UIPopups.BasePopupUI.HasActivePopup;
        Cursor.lockState = restore ? previousCursorLock : CursorLockMode.None;
        Cursor.visible = restore ? previousCursorVisible : true;
    }
}

[Serializable]
public class GrainElementSettings
{
    public GrainType grainType;
    public Sprite grainSprite;
    [Min(0)] public float grainPrice;
}
