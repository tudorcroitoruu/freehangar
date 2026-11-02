using UnityEngine;

public class ShopProduct : MonoBehaviour, IInteractable
{
    public enum DeliveryMode { Hands, AtSpawnPoint }

    [SerializeField] private string productID;
    [SerializeField] private string productName;
    [SerializeField, Min(0), Tooltip("Legacy fallback. A ShopManager with Economy Settings uses the container price from that asset instead.")]
    private int productPrice;
    [SerializeField] private bool toBuy = true;
    [SerializeField] private bool toRent;

    [Header("Delivery")]
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private DeliveryMode deliveryMode = DeliveryMode.Hands;
    [SerializeField, Tooltip("Required when Delivery Mode is At Spawn Point.")]
    private Transform spawnPoint;
    [SerializeField, Tooltip("Optional. Automatically finds a shared manager, including on a disabled inventory depot.")]
    private InventoryManager inventoryManager;
    [SerializeField, Min(0), Tooltip("Maximum active instances of this product ID in the scene. Zero disables the limit.")]
    private int maxActiveInScene = 10;
    private ShopManager pricingShop;
    internal void ConfigurePricing(ShopManager shop) => pricingShop = shop;
    public int Price
    {
        get
        {
            if (pricingShop == null) pricingShop = GetComponentInParent<ShopManager>(true);
            return pricingShop != null ? pricingShop.ProductPrice(productID, productPrice) : productPrice;
        }
    }
    public GameObject ProductPrefab
    {
        get
        {
            if (pricingShop == null) pricingShop = GetComponentInParent<ShopManager>(true);
            return pricingShop != null ? pricingShop.ProductPrefab(productID, objectPrefab) : objectPrefab;
        }
    }

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || (!toBuy && !toRent)) return false;

        string action = toBuy ? "Buy" : "Rent";
        string price = $"$ {EconomyNumberFormat.Accounting(Price)}";
        if (!CanAfford()) price = $"<color=#D92D2D>{price}</color>";
        prompt = new InteractionPromptData(
            transform.position + Vector3.up * .5f,
            $"{action} {productName}\n{price}",
            toBuy);
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || !toBuy) return false;
        bool purchased = CanDeliver(interactor, out _) && SaveSystem.TryBuyProduct(productID, Price, () => TryDeliver(interactor));
        GameplayAudio.Instance?.UpgradeResult(purchased);
        return purchased;
    }

    private bool CanAfford() => SaveSystem.Data != null && Price >= 0 && SaveSystem.Money >= Price;

    private bool CanDeliver(PlayerInteractor interactor, out bool requiresFreeHands)
    {
        requiresFreeHands = false;
        var prefab = ProductPrefab;
        if (inventoryManager == null)
            inventoryManager = GetComponentInParent<InventoryManager>(true);
        if (inventoryManager == null)
            inventoryManager = FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
        if (prefab == null || inventoryManager == null || string.IsNullOrWhiteSpace(productID) ||
            !inventoryManager.CanSpawn(productID, maxActiveInScene)) return false;
        if (deliveryMode == DeliveryMode.AtSpawnPoint) return spawnPoint != null;

        var container = prefab.GetComponent<GrainContainer>();
        if (container != null && !container.CanBeCarried) return false;
        var carryable = prefab.GetComponent<CarryableItem>();
        if (carryable != null && !carryable.CanBeCarried) return false;
        if (interactor == null || interactor.Items == null) return false;
        requiresFreeHands = !interactor.Items.CanTake && interactor.HandsOccupied;
        return interactor.Items.CanTake;
    }

    private bool TryDeliver(PlayerInteractor interactor)
    {
        var prefab = ProductPrefab;
        if (prefab == null) return false;
        var point = spawnPoint != null ? spawnPoint : transform;
        // Preserve the authored axis correction of sacks when delivering to hands.
        var rotation = deliveryMode == DeliveryMode.Hands ? prefab.transform.rotation : point.rotation;
        var instance = Instantiate(prefab, point.position, rotation);
        if (instance == null) return false;
        // Track the purchased scene object; a failed handoff unregisters it below.
        var registration = inventoryManager.RegisterInstance(instance, productID, maxActiveInScene);
        if (registration == null)
        {
            Destroy(instance);
            return false;
        }
        if (deliveryMode == DeliveryMode.AtSpawnPoint) return true;

        if (instance.GetComponent<IInteractable>() == null) instance.AddComponent<CarryableItem>();
        var container = instance.GetComponent<GrainContainer>();
        bool useContainerCarrier = container != null && interactor.Bags != null;
        if (useContainerCarrier ? interactor.Bags.TryInteract(container) : interactor.Items.TryTake(instance))
            return true;

        inventoryManager.UnregisterInstance(registration);
        Destroy(instance);
        return false;
    }
}
