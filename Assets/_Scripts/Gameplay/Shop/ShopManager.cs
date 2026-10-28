using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [SerializeField] private ShopProduct[] _shopProducts;

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
