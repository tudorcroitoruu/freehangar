using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class SellingScreen : MonoBehaviour
{
    [SerializeField] private AudioClip cashInClip;
    [SerializeField] private GameObject soldBagEffect;
    [SerializeField] private UnityEngine.Audio.AudioMixerGroup cashInMixerGroup;
    [SerializeField] private SellGrainElement elementPrefab;
    [SerializeField] private Transform elementsParent;
    [Space] [SerializeField] private Text totalWeight;
    [SerializeField] private Text totalValue;
    [SerializeField] private Text totalValueButton;
    [Space] [SerializeField] private Button cancelButton;
    [SerializeField] private Button sellButton;
    private readonly List<SellGrainElement> elements = new List<SellGrainElement>();
    private readonly List<GrainContainer> containers = new List<GrainContainer>();
    private readonly List<GrainContainer> currentContainers = new List<GrainContainer>();
    private readonly Dictionary<GrainContainer, float> quotedWeights = new Dictionary<GrainContainer, float>();
    private readonly Dictionary<GrainContainer, float[]> quotedTypes = new Dictionary<GrainContainer, float[]>();
    private readonly Dictionary<GrainType, float> quotedPrices = new Dictionary<GrainType, float>();
    private PlayerInteractor player;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;
    private bool cursorCaptured;
    private bool saleInProgress;
    private int quotedCash;
    private int quotedSellMultiplier = 1;
    private static SellingScreen current;
    private static int closedFrame = -1;
    public SellingZone Zone { get; private set; }
    public bool IsOpen => Zone != null;
    public static bool BlocksGameplayInput => (current != null && current.IsOpen) || closedFrame == Time.frameCount;

    public bool Open(SellingZone zone, PlayerInteractor interactor)
    {
        if (IsOpen || !enabled || zone == null || !zone.CanWeigh(interactor) || elementPrefab == null || elementsParent == null ||
            UpgradeManager.BlocksGameplayInput || UIPopups.BasePopupUI.HasActivePopup ||
            (transform.parent != null && !transform.parent.gameObject.activeInHierarchy)) return false;
        Zone = zone;
        saleInProgress = false;
        player = interactor;
        current = this;
        gameObject.SetActive(true);
        Refresh();
        AudioManager.Instance?.PlayConfirm();
        GoalChecker.ReportValue(GoalType.BuyerReached, 1);
        return true;
    }

    private void OnEnable()
    {
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorCaptured = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);
        if (sellButton != null) sellButton.onClick.AddListener(Sell);
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (!Zone.CanWeigh(player)) { Zone.PingBlockers(); Close(); return; }
        if (!Application.isFocused || UIPopups.BasePopupUI.HasActivePopup) return;
        if (FarmInput.Down("Secondary") || Input.GetKeyDown(KeyCode.Return)) { Sell(); return; }
        if (FarmInput.Back) Close();
        else if (!QuoteMatches()) Refresh();
        else if (sellButton != null) sellButton.interactable = CanPay();
    }

    private void Refresh()
    {
        ClearElements();
        Zone.CollectContainers(containers);
        quotedWeights.Clear();
        quotedTypes.Clear();
        quotedPrices.Clear();
        quotedSellMultiplier=PlayerPerks.SellMultiplier;
        var quantities = new SortedDictionary<GrainType, float>();
        double weight = 0;
        var cropValues = new List<double>();
        foreach (var container in containers)
        {
            var composition = container.CopyComposition();
            for (int i = 0; i < composition.Length; i++)
            {
                if (composition[i] <= 0) continue;
                var grain = (GrainType)i;
                quantities.TryGetValue(grain, out float kilograms);
                quantities[grain] = kilograms + composition[i];
            }
            quotedWeights[container] = container.Kilograms;
            quotedTypes[container] = composition;
        }
        foreach (var entry in quantities)
        {
            Zone.TryGetSettings(entry.Key, out var settings);
            quotedPrices[entry.Key] = settings.grainPrice;
            var element = Instantiate(elementPrefab, elementsParent);
            element.gameObject.SetActive(true);
            element.SetData(settings, entry.Value, quotedSellMultiplier);
            elements.Add(element);
            weight += entry.Value;
            cropValues.Add(PlayerPerks.SellCash(EconomyNumberFormat.CropValue(entry.Value, settings.grainPrice),quotedSellMultiplier));
        }
        // Add the same whole-dollar amounts shown in the crop rows, without a hidden fractional remainder.
        quotedCash = EconomyNumberFormat.SaleTotal(cropValues);
        if (totalWeight != null) totalWeight.text = $"{EconomyNumberFormat.Accounting(weight)} kg";
        if (totalValue != null) totalValue.text = $"$ {EconomyNumberFormat.Accounting(quotedCash)}";
        if (totalValueButton != null) totalValueButton.text = $"SELL ALL - ${EconomyNumberFormat.Accounting(quotedCash)}";
        if (sellButton != null) sellButton.interactable = CanPay();
    }

    private bool CanPay() => !saleInProgress && containers.Count > 0 && quotedCash > 0 && SaveSystem.Data != null && SaveSystem.Money <= int.MaxValue - quotedCash;

    private bool QuoteMatches()
    {
        if(quotedSellMultiplier!=PlayerPerks.SellMultiplier)return false;
        Zone.CollectContainers(currentContainers);
        if (currentContainers.Count != containers.Count) return false;
        foreach (var container in currentContainers)
        {
            if (!quotedWeights.TryGetValue(container, out float weight) || weight != container.Kilograms) return false;
            for (int i = 0; i < GrainCargo.TypeCount; i++)
            {
                var grain = (GrainType)i;
                float amount = container.Contents[grain];
                if (quotedTypes[container][i] != amount) return false;
                if (amount > 0 && (!Zone.TryGetSettings(grain, out var settings) ||
                    !quotedPrices.TryGetValue(grain, out float price) || price != settings.grainPrice)) return false;
            }
        }
        return true;
    }

    public void Sell()
    {
        if (saleInProgress || !IsOpen || UIPopups.BasePopupUI.HasActivePopup) return;
        if (!Zone.CanWeigh(player)) { Zone.PingBlockers(); return; }
        if (!QuoteMatches()) { Refresh(); return; }
        if (!CanPay()) return;
        // Lock before AddMoney: its callbacks can invoke Sell again in the same frame.
        saleInProgress = true;
        if (sellButton != null) sellButton.interactable = false;
        var gameplayCanvas = GameplayCanvas.Instance;
        bool paid = gameplayCanvas != null ? gameplayCanvas.AddMoney(quotedCash) : SaveSystem.AddMoney(quotedCash);
        if (!paid)
        {
            saleInProgress = false;
            if (sellButton != null) sellButton.interactable = CanPay();
            return;
        }
        // GameplayCanvas plays the coin cue in sync with the balance transfer.
        // The gameplay source survives Close() and can play alongside the cash cue.
        GameplayAudio.Instance?.UpgradeResult(true);
        GameAchievements.GrainSold(quotedCash);
        var soldQuantities = new Dictionary<GrainType, float>();
        foreach (var entry in quotedWeights)
        {
            var composition = quotedTypes[entry.Key];
            for (int i = 0; i < composition.Length; i++)
            {
                if (composition[i] <= 0) continue;
                var grain = (GrainType)i;
                soldQuantities.TryGetValue(grain, out float kilograms);
                soldQuantities[grain] = kilograms + composition[i];
            }
        }
        SoldCargoAnimation.Begin(containers, soldBagEffect);
        // Report only paid sales, after cargo is excluded from world saves.
        foreach (var entry in soldQuantities)
            GoalChecker.ReportProgress(GoalType.GrainSold, entry.Value, grainType: entry.Key);
        GoalChecker.ReportProgress(GoalType.MoneyEarned, quotedCash);
        Close();
    }

    public void Close()
    {
        if (IsOpen && !saleInProgress) AudioManager.Instance?.PlayCancel();
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
        if (cursorCaptured)
        {
            cursorCaptured = false;
            bool restore = Application.isFocused && !UIPopups.BasePopupUI.HasActivePopup;
            Cursor.lockState = restore ? previousCursorLock : CursorLockMode.None;
            Cursor.visible = restore ? previousCursorVisible : true;
        }
        if (cancelButton != null) cancelButton.onClick.RemoveListener(Close);
        if (sellButton != null) sellButton.onClick.RemoveListener(Sell);
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
    }
}

[Serializable]
public class GrainElementSettings
{
    public GrainType grainType;
    public Sprite grainSprite;
    [Min(0)] public float grainPrice;
}
