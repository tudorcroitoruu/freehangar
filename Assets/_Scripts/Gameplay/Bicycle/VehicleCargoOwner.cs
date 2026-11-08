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
    public static bool CanLoadContainer(Component source, GrainContainer container)
    {
        if (container == null || container.IsClosing) return false;
        if (container.CanLoadOnVehicle) return true;
        var vehicle = Resolve(source);
        return vehicle != null && (vehicle.GetComponent<TrikeController>() != null ||
            vehicle.GetComponent<LawnmowerController>() != null || vehicle.GetComponent<PickupController>() != null);
    }
}
