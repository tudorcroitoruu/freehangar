using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [SerializeField] private List<InventoryItemInstance> spawnedProducts = new List<InventoryItemInstance>();

    public IReadOnlyList<InventoryItemInstance> SpawnedProducts => spawnedProducts;

    public int GetActiveCount(string productID)
    {
        spawnedProducts.RemoveAll(item => item == null);
        int count = 0;
        foreach (var item in spawnedProducts)
            if (item.ProductID == productID && item.gameObject.activeInHierarchy) count++;
        return count;
    }

    public bool CanSpawn(string productID, int maxActiveInScene) => !string.IsNullOrWhiteSpace(productID) &&
        (maxActiveInScene <= 0 || GetActiveCount(productID) < maxActiveInScene);

    public InventoryItemInstance RegisterInstance(GameObject instance, string productID, int maxActiveInScene)
    {
        if (instance == null || string.IsNullOrWhiteSpace(productID)) return null;
        var item = instance.GetComponent<InventoryItemInstance>();
        if (item != null && spawnedProducts.Contains(item)) return item.ProductID == productID ? item : null;
        if ((item != null && item.Owner != null) || !CanSpawn(productID, maxActiveInScene)) return null;
        if (item == null) item = instance.AddComponent<InventoryItemInstance>();
        item.Initialize(this, productID, maxActiveInScene);
        spawnedProducts.Add(item);
        return item;
    }

    public void UnregisterInstance(InventoryItemInstance item)
    {
        if (item == null || !spawnedProducts.Remove(item)) return;
        item.ClearOwner();
    }

    internal bool CanActivate(InventoryItemInstance item) => item.MaxActiveInScene <= 0 ||
        GetActiveCount(item.ProductID) <= item.MaxActiveInScene;

    private void OnEnable()
    {
        foreach (var item in spawnedProducts)
            if (item != null) item.RestoreOwner(this);
    }
}
