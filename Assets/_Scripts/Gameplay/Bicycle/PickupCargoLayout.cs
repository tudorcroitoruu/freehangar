using System.Collections.Generic;
using UnityEngine;

/// <summary>Each pickup bay accepts one large container or two smaller containers side by side.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BicycleController))]
public sealed class PickupCargoLayout : MonoBehaviour
{
    public Transform[] largeSlots = System.Array.Empty<Transform>();
    [Min(.01f)] public float smallContainerSeparation = .42f;
    public bool splitAlongLength = true;
    [SerializeField, HideInInspector] private Transform[] anchors;
    public int LargeCapacity => largeSlots?.Length ?? 0;
    public int SmallCapacity => LargeCapacity * 2;
    private void Awake() => EnsureSlots();
    public void EnsureSlots()
    {
        bool valid=anchors!=null && anchors.Length==LargeCapacity*3;
        for(int bay=0;bay<LargeCapacity && valid;bay++)
            valid=largeSlots[bay]!=null && anchors[bay*3]==largeSlots[bay] && anchors[bay*3+1]!=null && anchors[bay*3+2]!=null;
        if(valid)
        {
            RefreshSmallPositions();
            GetComponent<BicycleController>().cargoSlots=anchors;
            return;
        }
        anchors=new Transform[LargeCapacity*3];
        for(int bay=0;bay<LargeCapacity;bay++)
        {
            var center=largeSlots[bay]; if(center==null) continue;
            anchors[bay*3]=center;
            for(int side=0;side<2;side++)
            {
                string slotName="Small container "+(bay*2+side+1);
                var slot=center.parent!=null?center.parent.Find(slotName):null;
                if(slot==null)
                {
                    slot=new GameObject(slotName).transform;
                    slot.SetParent(center.parent,false);
                }
                slot.localRotation=center.localRotation;
                slot.localPosition=center.localPosition+center.localRotation*((splitAlongLength?Vector3.forward:Vector3.right)*(side==0?-.5f:.5f)*smallContainerSeparation);
                anchors[bay*3+side+1]=slot;
            }
        }
        GetComponent<BicycleController>().cargoSlots=anchors;
    }
    private void RefreshSmallPositions()
    {
        for(int bay=0;bay<LargeCapacity;bay++)
        {
            var center=largeSlots[bay];if(center==null)continue;
            for(int side=0;side<2;side++)
            {
                var slot=anchors[bay*3+side+1];if(slot==null)continue;
                slot.localRotation=center.localRotation;
                slot.localPosition=center.localPosition+center.localRotation*((splitAlongLength?Vector3.forward:Vector3.right)*(side==0?-.5f:.5f)*smallContainerSeparation);
            }
        }
    }
    public static int Units(GrainContainer container) => container!=null?container.PickupSpaceUnits:1;
    private bool Occupied(int index) => anchors[index]!=null && anchors[index].childCount>0;
    public Transform FreeSlot(GrainContainer container) => FreeSlotByUnits(Units(container));
    public Transform FreeSlotByUnits(int units)
    {
        EnsureSlots();
        if(units<1 || units>2) return null;
        // Fill partially used bays first so intact bays remain available to Big Bags.
        if(units==1) for(int bay=0;bay<LargeCapacity;bay++)
        {
            int i=bay*3; if(Occupied(i)) continue;
            if(Occupied(i+1) && !Occupied(i+2)) return anchors[i+2];
            if(Occupied(i+2) && !Occupied(i+1)) return anchors[i+1];
        }
        for(int bay=0;bay<LargeCapacity;bay++)
        {
            int i=bay*3;
            if(Occupied(i) || Occupied(i+1) || Occupied(i+2)) continue;
            return anchors[i+(units==2?0:1)];
        }
        return null;
    }
    public bool CanLoadAt(Transform slot, GrainContainer container)
    {
        EnsureSlots(); int index=System.Array.IndexOf(anchors,slot);
        if(index<0 || Occupied(index)) return false;
        int bay=index/3*3;
        if(Units(container)==2) return index%3==0 && !Occupied(bay+1) && !Occupied(bay+2);
        return index%3!=0 && !Occupied(bay);
    }
    public int PoseIndex(Transform slot)
    {
        EnsureSlots();int i=System.Array.IndexOf(anchors,slot);
        return i<0?0:i%3==0?i/3:(i/3)*2+(i%3-1);
    }
    public List<Transform> PreviewSlots(GrainContainer container)
    {
        EnsureSlots();var result=new List<Transform>();
        for(int bay=0;bay<LargeCapacity;bay++)
            if(Units(container)==2) { if(anchors[bay*3]!=null) result.Add(anchors[bay*3]); }
            else {if(anchors[bay*3+1]!=null)result.Add(anchors[bay*3+1]);if(anchors[bay*3+2]!=null)result.Add(anchors[bay*3+2]);}
        return result;
    }
}
