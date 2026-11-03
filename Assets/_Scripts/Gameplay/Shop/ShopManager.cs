using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [SerializeField] private ShopProduct[] _shopProducts;
    [SerializeField] private GrainEconomySettings economySettings;
    [SerializeField, Min(.01f)] private float availabilityRefreshInterval = 3f;
    public GrainEconomySettings EconomySettings => economySettings;
    private PlayerData loadedData;
    private float nextAvailabilityCheck;
    private bool availabilityRefreshPending;
    private readonly Dictionary<ShopProduct, bool> productUnlockStates = new Dictionary<ShopProduct, bool>();

    private void OnEnable()
    {
        ConfigureProductPricing();
        SaveSystem.HangarProgressChanged += RequestAvailabilityRefresh;
        SaveSystem.MoneyChanged += OnMoneyChanged;
        RefreshProductAvailability();
    }

    public void ConfigureProductPricing()
    {
        if (_shopProducts == null) return;
        foreach (var product in _shopProducts) if (product != null) product.ConfigurePricing(this);
    }

    public int ProductPrice(string productId, int legacyPrice) => economySettings != null && economySettings.TryGetContainer(productId, out var entry) ? entry.price : legacyPrice;
    public GameObject ProductPrefab(string productId, GameObject legacyPrefab) => economySettings != null && economySettings.TryGetContainer(productId, out var entry) ? entry.prefab : legacyPrefab;

    private void OnDisable()
    {
        SaveSystem.HangarProgressChanged -= RequestAvailabilityRefresh;
        SaveSystem.MoneyChanged -= OnMoneyChanged;
    }

    // Vehicle purchases (including vouchers) publish this after their unlock is saved in PlayerData.
    private void OnMoneyChanged(int _) => RequestAvailabilityRefresh();

    private void RequestAvailabilityRefresh() => availabilityRefreshPending = true;

    private void Update()
    {
        if (Time.unscaledTime < nextAvailabilityCheck) return;
        nextAvailabilityCheck = Time.unscaledTime + Mathf.Max(.01f, availabilityRefreshInterval);
        var data = SaveSystem.savesLoaded ? SaveSystem.Data : null;
        if (availabilityRefreshPending || loadedData != data) RefreshProductAvailability();
    }

    public void RefreshProductAvailability()
    {
        availabilityRefreshPending = false;
        nextAvailabilityCheck = Time.unscaledTime + Mathf.Max(.01f, availabilityRefreshInterval);
        var data = SaveSystem.savesLoaded ? SaveSystem.Data : null;
        if (loadedData != data) productUnlockStates.Clear();
        loadedData = data;
        if (_shopProducts == null) return;
        foreach (var product in _shopProducts)
        {
            if (product == null) continue;
            bool active = product.IsUnlocked(loadedData);
            bool newlyUnlocked = active && productUnlockStates.TryGetValue(product, out bool wasUnlocked) && !wasUnlocked;
            productUnlockStates[product] = active;
            if (product.gameObject.activeSelf != active) product.gameObject.SetActive(active);
            if (newlyUnlocked) GameplayCanvas.Instance?.ShowAchievement(WarningType.NewContainerOpened);
        }
    }

    /// <summary>Shows or hides a product managed by this shop, including its interaction.</summary>
    public bool SetProductActive(ShopProduct product, bool active)
    {
        if (product == null || _shopProducts == null || System.Array.IndexOf(_shopProducts, product) < 0)
            return false;

        product.gameObject.SetActive(active);
        return true;
    }

    public void SetAllProductsActive(bool active)
    {
        if (_shopProducts == null) return;
        foreach (var product in _shopProducts)
            if (product != null) product.gameObject.SetActive(active);
    }
}
