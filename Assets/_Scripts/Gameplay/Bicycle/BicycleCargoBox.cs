using UnityEngine;
/// <summary>Marks a transportable box. E loads nearby boxes; aim at a loaded box and E unloads it.</summary>
public sealed class BicycleCargoBox : MonoBehaviour
{
    Transform originalParent;
    bool loaded;
    public bool Load(Transform slot)
    {
        if(slot==null || loaded)return false;
        originalParent=transform.parent; transform.SetParent(slot,true);transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;
        if(TryGetComponent<Rigidbody>(out var body)){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;}
        loaded=true;return true;
    }
    public bool Unload(Vector3 position)
    {
        if(!loaded)return false;
        transform.SetParent(originalParent,true);transform.position=position;
        if(TryGetComponent<Rigidbody>(out var body)){body.isKinematic=false;body.useGravity=true;}
        loaded=false;return true;
    }
    public bool IsLoaded => loaded;
}
