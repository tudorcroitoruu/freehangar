using UnityEngine;

/// <summary>Applies the vehicle's authored container/slot offsets without accumulating drift.</summary>
[DefaultExecutionOrder(20), DisallowMultipleComponent]
public sealed class VehicleCargoPlacement : MonoBehaviour
{
    private Transform support;
    private Quaternion baseRotation;
    private Vector3 baseScale, baseWorldScale;
    private bool seatAtBottom;
    private VehicleUpgradeSettings.VehicleType profile;
    private string containerId;
    private int slotIndex;

    public static void Attach(Transform item, Transform slot, bool bottom)
    {
        var upgrade=VehicleCargoOwner.Upgrade(slot);
        if(upgrade==null) return;
        var state=item.GetComponent<VehicleCargoPlacement>() ?? item.gameObject.AddComponent<VehicleCargoPlacement>();
        state.baseScale=item.localScale; state.baseWorldScale=item.lossyScale;
        state.support=slot; state.baseRotation=Quaternion.Inverse(slot.rotation)*item.rotation;
        state.seatAtBottom=bottom; state.profile=upgrade.Profile;
        var cargo=item.GetComponent<GrainContainer>();
        state.containerId=cargo!=null?cargo.CargoTypeId:"box";
        var rig=VehicleCargoOwner.Resolve(slot);
        state.slotIndex=rig!=null && rig.cargoSlots!=null?System.Array.IndexOf(rig.cargoSlots,slot):0;
        if(rig!=null && rig.TryGetComponent<PickupCargoLayout>(out var layout)) state.slotIndex=layout.PoseIndex(slot);
        state.slotIndex=Mathf.Max(0,state.slotIndex);
        state.enabled=true; state.Apply();
    }
    private void OnTransformParentChanged()
    {
        if(support==null || transform.parent==support) return;
        ReleaseScale();
    }
    public void ReleaseScale()
    {
        if(support==null) return;
        // Remove the rack's pose before the carrier derives the bag's upright orientation.
        // Otherwise picking up a sideways bag makes that sideways pose its new baseline.
        transform.rotation=support.rotation*baseRotation;
        Vector3 parentScale=transform.parent!=null?transform.parent.lossyScale:Vector3.one;
        transform.localScale=new Vector3(baseWorldScale.x/parentScale.x,baseWorldScale.y/parentScale.y,baseWorldScale.z/parentScale.z);
        support=null; enabled=false;
    }
    private void LateUpdate() => Apply();
    public void Apply()
    {
        if(support==null || transform.parent!=support) { enabled=false; return; }
        if(VehicleCargoOwner.Resolve(support)?.GetComponent<VehicleReset>() is VehicleReset reset && reset.IsResetting) return;
        Place(transform,support,profile,containerId,slotIndex,baseRotation,seatAtBottom,baseScale);
    }
    public static void Place(Transform item, Transform slot, VehicleUpgradeSettings.VehicleType profile,
        string containerId, int index, Quaternion baseRotation, bool bottom, Vector3 baseScale)
    {
        var pose=profile?.FindCargoPose(containerId,index);
        item.localScale=Vector3.Scale(baseScale,pose?.EffectiveScale ?? Vector3.one);
        bool empty = item.TryGetComponent<GrainContainer>(out var container) && container.IsEmpty;
        item.rotation=slot.rotation*baseRotation*Quaternion.Euler(empty ? Vector3.zero : pose?.rotation ?? Vector3.zero);
        item.position=slot.position;
        if(bottom) VehicleCargoRack.SeatBase(item,slot);
        else if(item.TryGetComponent<GrainContainer>(out var cargo)) item.position+=slot.position-cargo.WorldBounds.center;
        if(pose!=null && !empty) item.position+=slot.TransformVector(pose.position);
        if (empty && bottom) StackEmptyBag(item, slot);
    }

    private static void StackEmptyBag(Transform item, Transform slot)
    {
        if (slot.parent == null) return;
        bool earlierCargo = false;
        // Earlier occupied slots form the stack; full containers retain their authored poses.
        for (int i = slot.GetSiblingIndex() - 1; i >= 0; i--)
        {
            var previousSlot = slot.parent.GetChild(i);
            if (previousSlot.childCount == 0) continue;
            var previous = previousSlot.GetChild(0);
            if (!previous.TryGetComponent<GrainContainer>(out var cargo)) continue;
            earlierCargo = true;
            if (!cargo.IsEmpty) continue;
            Bounds below = VehicleCargoRack.ContactBounds(previous, slot);
            Bounds current = VehicleCargoRack.ContactBounds(item, slot);
            item.position += slot.TransformVector(new Vector3(below.center.x - current.center.x,
                below.max.y - current.min.y, below.center.z - current.center.z));
            return;
        }
        var rack = VehicleCargoOwner.Rack(slot);
        if (!earlierCargo && rack != null && rack.surface == slot.parent)
            item.position += rack.surface.TransformPoint(rack.SlotPosition(0)) - slot.position;
    }
}
