using UnityEngine;

/// <summary>Connects a separately jointed cargo trailer to its driving vehicle.</summary>
public sealed class VehicleCargoOwner : MonoBehaviour
{
    public BicycleController vehicle;
    public static BicycleController Resolve(Component source)
    {
        if(source==null) return null;
        var rig=source.GetComponentInParent<BicycleController>();
        if(rig!=null) return rig;
        var owner=source.GetComponentInParent<VehicleCargoOwner>();
        return owner!=null?owner.vehicle:null;
    }
    public static VehicleCargoRack Rack(Component source) => Resolve(source)?.GetComponent<VehicleCargoRack>();
    public static VehicleUpgrade Upgrade(Component source) => Resolve(source)?.GetComponent<VehicleUpgrade>();
}
