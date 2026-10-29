using UnityEngine;


public class InventoryProduct : MonoBehaviour, IInteractable
{
    public enum DeliveryMode { Hands, AtSpawnPoint }
    [SerializeField] private string productID;
    [SerializeField] private string productName;

    [SerializeField] private GameObject[] visual;
    [SerializeField] private bool unlimitedCount;
    [SerializeField, Min(0), Tooltip("Maximum active instances of this product ID in the scene. Zero disables the limit.")]
    private int maxActiveInScene = 10;
    private InventoryManager inventoryManager;

    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private DeliveryMode deliveryMode = DeliveryMode.Hands;
    [SerializeField, Tooltip("Required delivery point when Delivery Mode is At Spawn Point.")]
    private Transform spawnPoint;

    public string ProductID => productID;
    public bool UnlimitedCount => unlimitedCount;
    public int MaxActiveInScene => maxActiveInScene;

    internal void SetInventoryManager(InventoryManager manager) => inventoryManager = manager;

    private void OnEnable()
    {
        SaveSystem.OwnedProductsChanged += RefreshOwnedVisuals;
        RefreshOwnedVisuals();
    }

    private void OnDisable() => SaveSystem.OwnedProductsChanged -= RefreshOwnedVisuals;

    private void RefreshOwnedVisuals() => SetOwned(unlimitedCount || (SaveSystem.Data?.GetOwnedProductCount(productID) ?? 0) > 0);

    public void SetOwned(bool owned)
    {
        if (visual == null) return;
        foreach (var item in visual)
            if (item != null && item.activeSelf != owned) item.SetActive(owned);
    }
    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        int count = unlimitedCount ? 0 : SaveSystem.Data?.GetOwnedProductCount(productID) ?? 0;
        bool available = unlimitedCount || count > 0;
        bool requiresFreeHands = false;
        string blockedReason = available ? GetDeliveryBlockReason(interactor, out requiresFreeHands) : null;
        string action = unlimitedCount ? $"Take 1 {productName}" : count > 0 ? $"Take 1 {productName}\nLeft: {count}" : $"{productName}\nOwn: 0";
        prompt = new InteractionPromptData(
            transform.position + Vector3.up * .5f,
            action,
            available && string.IsNullOrEmpty(blockedReason));
        prompt.RequiresFreeHands = requiresFreeHands;
        return isActiveAndEnabled;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || !CanDeliver(interactor) ||
            (!unlimitedCount && (SaveSystem.Data?.GetOwnedProductCount(productID) ?? 0) <= 0)) return false;

        var point = spawnPoint != null ? spawnPoint : transform;
        // Imported sacks need their authored axis correction, independent of the inventory shelf.
        var rotation = deliveryMode == DeliveryMode.Hands ? objectPrefab.transform.rotation : point.rotation;
        var instance = Instantiate(objectPrefab, point.position, rotation);
        if (instance == null) return false;
        var registration = inventoryManager.RegisterInstance(instance, productID, maxActiveInScene);
        if (registration == null)
        {
            Destroy(instance);
            return false;
        }
        if (deliveryMode == DeliveryMode.Hands && instance.GetComponent<IInteractable>() == null)
            instance.AddComponent<CarryableItem>();
        var container = instance.GetComponent<GrainContainer>();
        bool useContainerCarrier = deliveryMode == DeliveryMode.Hands && container != null && interactor.Bags != null;
        if (deliveryMode == DeliveryMode.Hands &&
            !(useContainerCarrier ? interactor.Bags.TryInteract(container) : interactor.Items.TryTake(instance)))
        {
            inventoryManager.UnregisterInstance(registration);
            Destroy(instance);
            return false;
        }
        if (unlimitedCount || SaveSystem.TryTakeProduct(productID)) return true;
        if (deliveryMode == DeliveryMode.Hands)
        {
            if (useContainerCarrier) interactor.Bags.CancelTake(container);
            else interactor.Items.CancelTake(instance);
        }
        inventoryManager.UnregisterInstance(registration);
        Destroy(instance);
        return false;
    }

    private bool CanDeliver(PlayerInteractor interactor) => string.IsNullOrEmpty(GetDeliveryBlockReason(interactor));

    private string GetDeliveryBlockReason(PlayerInteractor interactor) => GetDeliveryBlockReason(interactor, out _);

    private string GetDeliveryBlockReason(PlayerInteractor interactor, out bool requiresFreeHands)
    {
        requiresFreeHands = false;
        if (inventoryManager == null)
            inventoryManager = GetComponentInParent<InventoryManager>(true);
        if (inventoryManager == null)
            inventoryManager = FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
        if (objectPrefab == null || inventoryManager == null || string.IsNullOrWhiteSpace(productID)) return "Unavailable";
        if (!inventoryManager.CanSpawn(productID, maxActiveInScene)) return $"Scene limit reached ({maxActiveInScene})";
        if (deliveryMode == DeliveryMode.AtSpawnPoint) return spawnPoint != null ? null : "Unavailable";

        var container = objectPrefab.GetComponent<GrainContainer>();
        if (container != null && !container.CanBeCarried) return "Cannot carry this container";
        var carryable = objectPrefab.GetComponent<CarryableItem>();
        if (carryable != null && !carryable.CanBeCarried) return "Cannot carry this item";
        if (interactor == null || interactor.Items == null) return "Unavailable";
        requiresFreeHands = !interactor.Items.CanTake && interactor.HandsOccupied;
        return interactor.Items.CanTake ? null : "Free your hands first";
    }
}
