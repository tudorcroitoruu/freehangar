using UnityEngine;

/// <summary>Identity and live object reference for a product delivered by the inventory.</summary>
[DisallowMultipleComponent]
public sealed class InventoryItemInstance : MonoBehaviour
{
    [SerializeField] private string productID;
    [SerializeField] private string instanceID;
    [SerializeField] private int maxActiveInScene;
    private InventoryManager owner;

    public string ProductID => productID;
    public string InstanceID => instanceID;
    internal InventoryManager Owner => owner;
    internal int MaxActiveInScene => maxActiveInScene;

    internal void Initialize(InventoryManager manager, string id, int limit)
    {
        owner = manager;
        productID = id;
        maxActiveInScene = limit;
        instanceID = System.Guid.NewGuid().ToString("N");
    }

    internal void ClearOwner() => owner = null;
    internal void RestoreOwner(InventoryManager manager) => owner = manager;

    private void OnEnable()
    {
        // Re-enabling an old instance must not exceed the limit after another one was delivered.
        if (owner != null && !owner.CanActivate(this)) gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (owner != null) owner.UnregisterInstance(this);
    }
}
