using UnityEngine;

public class ShopProduct : MonoBehaviour, IInteractable
{
    public string SaveProductId => productID;
    public int MaxActiveInScene => maxActiveInScene;
    public enum DeliveryMode { Hands, AtSpawnPoint }
    public enum UnlockCondition { Always, HangarCleared, HangarOpened, VehicleUnlocked }

    [SerializeField] private string productID;
    [SerializeField] private string productName;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .5f;
    private Vector3 PromptPosition => (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset;
    [SerializeField, Min(0), Tooltip("Legacy fallback. A ShopManager with Economy Settings uses the container price from that asset instead.")]
    private int productPrice;
    [SerializeField] private bool toBuy = true;
    [SerializeField] private bool toRent;

    [Header("Unlock")]
    [SerializeField] private UnlockCondition unlockCondition = UnlockCondition.Always;
    [SerializeField, Min(1), Tooltip("Hangar used by the opening or clearance unlock condition. Collecting its key alone is not enough.")]
    private int requiredHangar = 2;
    [SerializeField, Range(0f, 100f), Tooltip("Required percentage of the selected hangar cleared. The highest achieved percentage is saved.")]
    private float requiredClearedPercent = 50f;
    [SerializeField, Tooltip("Vehicle ID required by the Vehicle Unlocked condition.")]
    private string requiredVehicleId = "motoroller";
    private VehicleUpgradeSettings vehicleUnlockSettings;

    [SerializeField] private bool showWhileLocked;
    public bool ShowWhileLocked => showWhileLocked;
    public bool Locked => unlockCondition != UnlockCondition.Always &&
        (!SaveSystem.savesLoaded || !IsUnlocked(SaveSystem.Data));

    public string UnlockHint()
    {
        if (!SaveSystem.savesLoaded || SaveSystem.Data == null) return "Loading shop availability…";
        return unlockCondition switch
        {
            UnlockCondition.HangarOpened => $"Open Hangar {requiredHangar} to buy the {productName}.",
            UnlockCondition.HangarCleared when requiredClearedPercent > 0 =>
                $"Clear {requiredClearedPercent:0.##}% of Hangar {requiredHangar} to buy the {productName}.",
            UnlockCondition.HangarCleared => $"Open Hangar {requiredHangar} to buy the {productName}.",
            UnlockCondition.VehicleUnlocked =>
                $"Unlock the {vehicleUnlockSettings?.Find(requiredVehicleId)?.displayName ?? requiredVehicleId} to buy the {productName}.",
            _ => ""
        };
    }

    public bool IsUnlocked(PlayerData data)
    {
        if (data == null) return false;
        switch (unlockCondition)
        {
            case UnlockCondition.Always: return true;
            case UnlockCondition.HangarCleared:
                return data.IsHangarOpen(requiredHangar) && data.GetHangarClearedPercent(requiredHangar) >= requiredClearedPercent;
            case UnlockCondition.HangarOpened: return data.IsHangarOpen(requiredHangar);
            case UnlockCondition.VehicleUnlocked:
                if (vehicleUnlockSettings == null) vehicleUnlockSettings = Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
                return vehicleUnlockSettings != null && vehicleUnlockSettings.IsUnlocked(requiredVehicleId, data);
            default: return false;
        }
    }

    [Header("Delivery")]
    [SerializeField] private GameObject objectPrefab;
    [SerializeField] private DeliveryMode deliveryMode = DeliveryMode.Hands;
    [SerializeField, Tooltip("Required when Delivery Mode is At Spawn Point.")]
    private Transform spawnPoint;
    [SerializeField, Tooltip("Additional delivery positions relative to the spawn point, in its local axes (metres).")]
    private Vector3[] additionalSpawnOffsets = System.Array.Empty<Vector3>();

    [SerializeField] private GameObject objectSpawnHolder;
    [SerializeField, Tooltip("Delivery clearance box in spawn point local axes. Keep its bottom above the floor.")]
    private Vector3 spawnAreaSize = Vector3.one;
    [SerializeField] private Vector3 spawnAreaOffset = new Vector3(0f, .55f, 0f);
    [SerializeField] private LayerMask spawnBlockingLayers = ~0;
    private readonly Collider[] spawnOverlaps = new Collider[64];
    private float nextSpawnAreaCheck;
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

    private void OnEnable() => RefreshSpawnHolder();

    private void Update()
    {
        if (deliveryMode != DeliveryMode.AtSpawnPoint || objectSpawnHolder == null ||
            Time.unscaledTime < nextSpawnAreaCheck) return;
        nextSpawnAreaCheck = Time.unscaledTime + .1f;
        RefreshSpawnHolder();
    }

    private Vector3 SpawnAreaHalfSize => new Vector3(
        Mathf.Max(.01f, spawnAreaSize.x), Mathf.Max(.01f, spawnAreaSize.y), Mathf.Max(.01f, spawnAreaSize.z)) * .5f;

    private Vector3 SpawnAreaCenter => spawnPoint.position + spawnPoint.rotation * spawnAreaOffset;

    private bool IsSpawnAreaClear() => TryFindDeliveryPosition(out _);

    private bool TryFindDeliveryPosition(out Vector3 position)
    {
        position = default;
        if (spawnPoint == null) return false;
        Physics.SyncTransforms();
        for (int slot = 0; slot <= additionalSpawnOffsets.Length; slot++)
        {
            var offset = slot == 0 ? Vector3.zero : additionalSpawnOffsets[slot - 1];
            position = spawnPoint.position + spawnPoint.rotation * offset;
            if (IsDeliveryPositionClear(position)) return true;
        }
        return false;
    }

    private bool IsDeliveryPositionClear(Vector3 position)
    {
        int count = Physics.OverlapBoxNonAlloc(position + spawnPoint.rotation * spawnAreaOffset, SpawnAreaHalfSize, spawnOverlaps,
            spawnPoint.rotation, spawnBlockingLayers, QueryTriggerInteraction.Ignore);
        // A full buffer may omit a blocker, so treat it as occupied.
        if (count == spawnOverlaps.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var hit = spawnOverlaps[i];
            if (hit == null || (objectSpawnHolder != null && hit.transform.IsChildOf(objectSpawnHolder.transform))) continue;
            return false;
        }
        return true;
    }

    private void RefreshSpawnHolder()
    {
        if (objectSpawnHolder == null) return;
        // The authored marker belongs to the original slot, not the overflow positions.
        Physics.SyncTransforms();
        bool visible = !Locked && deliveryMode == DeliveryMode.AtSpawnPoint && spawnPoint != null &&
            IsDeliveryPositionClear(spawnPoint.position);
        if (objectSpawnHolder.activeSelf != visible) objectSpawnHolder.SetActive(visible);
    }

    private void OnDrawGizmos()
    {
        if (deliveryMode != DeliveryMode.AtSpawnPoint || spawnPoint == null) return;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = Matrix4x4.TRS(SpawnAreaCenter, spawnPoint.rotation, Vector3.one);
        Gizmos.color = new Color(0f, 1f, 1f, .12f);
        Gizmos.DrawCube(Vector3.zero, SpawnAreaHalfSize * 2f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, SpawnAreaHalfSize * 2f);
        foreach (var offset in additionalSpawnOffsets)
        {
            Gizmos.matrix = Matrix4x4.TRS(SpawnAreaCenter + spawnPoint.rotation * offset, spawnPoint.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, SpawnAreaHalfSize * 2f);
        }
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(spawnPoint.position, .1f);
        Gizmos.DrawLine(spawnPoint.position, SpawnAreaCenter);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(SpawnAreaCenter + Vector3.up * (SpawnAreaHalfSize.y + .15f),
            $"{productName} - Spawn area");
#endif
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || (!toBuy && !toRent)) return false;

        if (Locked)
        {
            prompt = new InteractionPromptData(PromptPosition, $"{productName} — Locked", false);
            return showWhileLocked;
        }

        string action = toBuy ? "Buy" : "Rent";
        string price = $"${EconomyNumberFormat.Accounting(Price)}";
        if (!CanAfford()) price = $"<color=#D92D2D>{price}</color>";
        prompt = new InteractionPromptData(
            PromptPosition,
            $"{action} {productName}",
            toBuy) { SecondaryDescription = $"Price: {price}" };
        if (maxActiveInScene > 0)
        {
            if (inventoryManager == null) inventoryManager = GetComponentInParent<InventoryManager>(true);
            if (inventoryManager == null) inventoryManager = FindFirstObjectByType<InventoryManager>(FindObjectsInactive.Include);
            if (inventoryManager != null)
                prompt.SecondaryDescription += $"  |  Left: {Mathf.Max(0, maxActiveInScene - inventoryManager.GetActiveCount(productID))}/{maxActiveInScene}";
        }
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || !toBuy || Locked) return false;
        if (deliveryMode == DeliveryMode.AtSpawnPoint && spawnPoint != null && !IsSpawnAreaClear())
        {
            GameplayCanvas.Instance?.ShowWarning(WarningType.OccupiedArea);
            GameplayAudio.Instance?.UpgradeResult(false);
            return false;
        }
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
        if (deliveryMode == DeliveryMode.AtSpawnPoint) return IsSpawnAreaClear();

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
        var position = spawnPoint != null ? spawnPoint.position : transform.position;
        if (deliveryMode == DeliveryMode.AtSpawnPoint && !TryFindDeliveryPosition(out position))
        {
            if (spawnPoint != null) GameplayCanvas.Instance?.ShowWarning(WarningType.OccupiedArea);
            return false;
        }
        var instance = Instantiate(prefab, position, prefab.transform.rotation);
        if (instance == null) return false;
        // Track the purchased scene object; a failed handoff unregisters it below.
        var registration = inventoryManager.RegisterInstance(instance, productID, maxActiveInScene);
        if (registration == null)
        {
            Destroy(instance);
            return false;
        }
        if (deliveryMode == DeliveryMode.AtSpawnPoint)
        {
            if (objectSpawnHolder != null) objectSpawnHolder.SetActive(false);
            return true;
        }

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
