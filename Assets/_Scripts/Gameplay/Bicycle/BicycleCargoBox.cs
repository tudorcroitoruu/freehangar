using UnityEngine;
/// <summary>Marks a transportable box. E loads nearby boxes; aim at a loaded box and E unloads it.</summary>
public sealed class BicycleCargoBox : MonoBehaviour, IInteractable
{
    Transform originalParent;
    bool loaded;
    public bool Load(Transform slot)
    {
        if(slot==null || loaded)return false;
        var upgrades = slot.GetComponentInParent<VehicleUpgrade>();
        if (upgrades != null && !upgrades.CanLoadVolume(VehicleUpgrade.MeasureVolume(gameObject))) return false;
        originalParent=transform.parent; transform.SetParent(slot,true);transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;
        if(slot.GetComponentInParent<VehicleCargoRack>()!=null)VehicleCargoRack.Seat(transform,slot);
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
    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = new InteractionPromptData(transform.position + Vector3.up * .3f, "Unload box");
        return isActiveAndEnabled && loaded && interactor != null && interactor.GetComponent<PlayerBicycleInteraction>() != null;
    }
    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out _)) return false;
        var rider = interactor.GetComponent<PlayerBicycleInteraction>();
        var vehicle = GetComponentInParent<BicycleController>();
        if (vehicle != null) rider.bicycle = vehicle;
        return rider.TryUnloadBox(this);
    }
}
