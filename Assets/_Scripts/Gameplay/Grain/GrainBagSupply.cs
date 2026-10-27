using UnityEngine;

/// <summary>A fixed sack station supplies a fresh empty receiver when its finished bag is carried away.</summary>
[DisallowMultipleComponent]
public sealed class GrainBagSupply : MonoBehaviour
{
    [SerializeField] private GrainContainer emptyBagPrefab;
    [SerializeField] private GrainContainer currentBag;
    public GrainContainer CurrentBag => currentBag;
    private void Update()
    {
        if (emptyBagPrefab == null || (currentBag != null && !currentBag.IsCarried)) return;
        currentBag = Instantiate(emptyBagPrefab, transform.position, transform.rotation, transform);
        currentBag.name = "Empty sorting sack";
    }
}
