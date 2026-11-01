using UnityEngine;
/// <summary>Marks a transportable box. E loads nearby boxes; aim at a loaded box and E unloads it.</summary>
public sealed class BicycleCargoBox : MonoBehaviour, IInteractable
{
    Transform originalParent;
    bool loaded;
    public bool Load(Transform slot)
    {
        if(slot==null || loaded)return false;
        var upgrades = VehicleCargoOwner.Upgrade(slot);
        if (upgrades != null && !upgrades.CanLoadCargo()) return false;
        if (upgrades != null && TryGetComponent<GrainContainer>(out var container) && !upgrades.AcceptsContainer(container)) return false;
        var layout=VehicleCargoOwner.Resolve(slot)?.GetComponent<PickupCargoLayout>();
        if(layout!=null && !layout.CanLoadAt(slot,GetComponent<GrainContainer>())) return false;
        originalParent=transform.parent; transform.SetParent(slot,true);transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;
        if(VehicleCargoOwner.Rack(slot)!=null)VehicleCargoRack.Seat(transform,slot);
        if(TryGetComponent<Rigidbody>(out var body)){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;}
        VehicleCargoPlacement.Attach(transform, slot, VehicleCargoOwner.Rack(slot)!=null);
        loaded=true;return true;
    }
    public bool Unload(Vector3 position)
    {
        if(!loaded)return false;
        if(TryGetComponent<VehicleCargoPlacement>(out var placement)) placement.ReleaseScale();
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
        var vehicle = VehicleCargoOwner.Resolve(this);
        if (vehicle != null) rider.bicycle = vehicle;
        return rider.TryUnloadBox(this);
    }
}
