using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [SerializeField] private ShopProduct[] _shopProducts;
    private PlayerData loadedData;

    private void OnEnable()
    {
        SaveSystem.HangarProgressChanged += RefreshProductAvailability;
        RefreshProductAvailability();
    }

    private void OnDisable() => SaveSystem.HangarProgressChanged -= RefreshProductAvailability;

    private void Update()
    {
        var data = SaveSystem.savesLoaded ? SaveSystem.Data : null;
        if (loadedData != data) RefreshProductAvailability();
    }

    public void RefreshProductAvailability()
    {
        loadedData = SaveSystem.savesLoaded ? SaveSystem.Data : null;
        if (_shopProducts == null) return;
        for (int i = 0; i < Mathf.Min(5, _shopProducts.Length); i++)
        {
            var product = _shopProducts[i];
            if (product == null) continue;
            bool active = loadedData != null && (i == 0
                ? loadedData.firstHangarHalfCleared : loadedData.IsHangarOpen(i + 1));
            if (product.gameObject.activeSelf != active) product.gameObject.SetActive(active);
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
