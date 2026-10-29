using System.Collections.Generic;
using UnityEngine;

/// <summary>Each rack bay supports either one sack or two boxes, seated on the physical surface.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BicycleController))]
public sealed class VehicleCargoRack : MonoBehaviour
{
    public Transform surface;
    public float baySpacing = .6f;
    private BicycleController rig;
    private readonly List<Transform> slots = new List<Transform>();
    private void Awake() => EnsureSlots();
    private int Limit => GetComponent<VehicleUpgrade>() is VehicleUpgrade upgrade && upgrade.IsConfigured ? upgrade.SlotCount : 2;
    private void EnsureSlots()
    {
        if (surface == null) return;
        if (rig == null) rig = GetComponent<BicycleController>();
        int required = Mathf.CeilToInt(Limit/2f)*2;
        while (slots.Count < required)
        {
            var slot = new GameObject("Rack contact " + slots.Count).transform;
            slot.SetParent(surface,false); slots.Add(slot);
        }
        rig.cargoSlots = slots.ToArray(); rig.exclusiveBagSlot = null;
        Reflow();
    }
    private static bool Occupied(Transform slot) => slot.childCount > 0;
    private static bool Bag(Transform slot) => Occupied(slot) && slot.GetChild(0).GetComponent<GrainContainer>() != null;
    public int UsedSlots
    {
        get { int total=0; foreach(var s in slots) if(Occupied(s)) total += Bag(s)?2:1; return total; }
    }
    public Transform FreeSlot(bool bag)
    {
        EnsureSlots();
        if (UsedSlots + (bag?2:1) > Limit) return null;
        for(int i=0;i<slots.Count;i+=2)
        {
            if(Bag(slots[i]) || Bag(slots[i+1]))continue;
            if(bag) { if(!Occupied(slots[i]) && !Occupied(slots[i+1]))return slots[i]; }
            else { if(!Occupied(slots[i]))return slots[i]; if(!Occupied(slots[i+1]))return slots[i+1]; }
        }
        return null;
    }
    private void LateUpdate() => Reflow();
    public void Reflow()
    {
        for(int i=0;i+1<slots.Count;i+=2)
        {
            int bay=i/2;
            Vector3 baseline=new Vector3(bay==0?0:(bay%2==1?1:-1)*((bay+1)/2)*baySpacing,0,0);
            slots[i].localPosition=baseline;
            float height=0;
            if(Occupied(slots[i])) { var item=slots[i].GetChild(0); Seat(item,slots[i]); height=ContactBounds(item,surface).size.y; }
            slots[i+1].localPosition=baseline+Vector3.up*height;
            if(Occupied(slots[i+1]))Seat(slots[i+1].GetChild(0),slots[i+1]);
        }
    }
    public static void Seat(Transform item,Transform support)
    {
        var bounds=ContactBounds(item,support);
        item.position += support.TransformVector(new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z));
    }
    private static Bounds ContactBounds(Transform item,Transform space)
    {
        var box=item.GetComponent<BoxCollider>(); Bounds result=default;
        Bounds bounds=box!=null?new Bounds(box.center,box.size):new Bounds(Vector3.zero,Vector3.one*.3f);
        for(int n=0;n<8;n++)
        {
            var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));
            var point=space.InverseTransformPoint(item.TransformPoint(corner));
            if(n==0)result=new Bounds(point,Vector3.zero);else result.Encapsulate(point);
        }
        return result;
    }
}
