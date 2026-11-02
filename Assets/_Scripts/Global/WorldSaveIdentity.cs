using UnityEngine;

// Assigned before gameplay can reparent objects. Vehicle upgrades transfer this identity.
[DisallowMultipleComponent]
public sealed class WorldSaveIdentity : MonoBehaviour
{
    public string Id { get; internal set; }
    public static void Transfer(GameObject from, GameObject to)
    {
        var identity = from.GetComponent<WorldSaveIdentity>();
        if (identity == null) return;
        (to.GetComponent<WorldSaveIdentity>() ?? to.AddComponent<WorldSaveIdentity>()).Id = identity.Id;
    }
}
