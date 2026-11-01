using UnityEngine;

/// <summary>One full big bag on the forks; uses the shared driving rig and its engine loop.</summary>
[DefaultExecutionOrder(400), DisallowMultipleComponent, RequireComponent(typeof(BicycleController))]
public sealed class ForkliftController : MonoBehaviour
{
    [SerializeField] private Transform forkAnchor;
    [SerializeField, Range(.4f,1.5f)] private float enginePitchMultiplier=.72f;
    [SerializeField, Min(.2f)] private float pickupReach=1.7f;
    [SerializeField, Min(.1f)] private float liftHeight=.55f;
    public float EnginePitchMultiplier => enginePitchMultiplier;
    public GrainContainer Cargo => cargo;
    private BicycleController rig;
    private GrainContainer cargo;
    private Rigidbody cargoBody;
    private Collider[] cargoColliders, vehicleColliders;
    private Vector3 seatedLocalPosition;
    private float liftBlend;
    private GrainContainer lastDropped;
    private bool[] originalColliderStates;
    private readonly Collider[] overlaps=new Collider[48];
    private readonly RaycastHit[] groundHits=new RaycastHit[32];
    private string blocked="";
    private float blockedUntil;
    public static bool CanLift(GrainContainer bag) => bag!=null && bag.isActiveAndEnabled && bag.IsBigBag && bag.IsFull && !bag.IsCarried && !bag.IsClosing;
    private void Awake(){rig=GetComponent<BicycleController>();vehicleColliders=GetComponentsInChildren<Collider>(true);}
    private bool AcceptInput => rig!=null && rig.IsRidden && Time.timeScale>0 && Application.isFocused && Cursor.lockState==CursorLockMode.Locked && !UpgradeManager.BlocksGameplayInput && !UIPopups.BasePopupUI.HasActivePopup;
    private void Update()
    {
        if(!AcceptInput || forkAnchor==null)return;
        if(lastDropped!=null && (lastDropped.WorldBounds.ClosestPoint(forkAnchor.position)-forkAnchor.position).sqrMagnitude>(pickupReach+.3f)*(pickupReach+.3f))lastDropped=null;
        if(cargo==null)
        {
            var bag=FindBag();
            // Only physical contact at the front forks engages automatic pickup.
            TryAutoLift(bag);
        }
        else if(Input.GetKeyDown(KeyCode.Q) && !TryDrop())
        {
            blocked="Make room to drop";blockedUntil=Time.time+1.2f;
        }
    }
    private void LateUpdate()
    {
        if(cargo!=null)
        {
            liftBlend=Mathf.MoveTowards(liftBlend,1,Time.deltaTime*2.5f);
            cargo.transform.localPosition=seatedLocalPosition+Vector3.up*(Mathf.SmoothStep(0,1,liftBlend)*liftHeight);
        }
        if(!AcceptInput || forkAnchor==null)return;
        bool available=cargo!=null;
        string action=Time.time<blockedUntil?blocked:cargo!=null?"Lower and drop big bag":"Drive forks into a full big bag";
        var data=new InteractionPromptData(forkAnchor.position+Vector3.up*.8f,action,available){InteractionKey=KeyCode.Q};
        GameplayCanvas.Instance?.interactionPrompt?.Show(Camera.main,data);
    }
    private GrainContainer FindBag()
    {
        int count=Physics.OverlapSphereNonAlloc(forkAnchor.position,pickupReach,overlaps,~0,QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length)return null;
        GrainContainer best=null;float distance=float.PositiveInfinity;
        for(int i=0;i<count;i++)
        {
            var bag=overlaps[i].GetComponentInParent<GrainContainer>();if(!CanLift(bag) || bag==lastDropped)continue;
            var bounds=bag.WorldBounds;float d=(bounds.ClosestPoint(forkAnchor.position)-forkAnchor.position).sqrMagnitude;
            Vector3 delta=bag.WorldBounds.center-forkAnchor.position;
            if(Vector3.Dot(delta,transform.forward)<-.25f || Mathf.Abs(delta.y)>2f || d>=distance)continue;
            // Do not lift a sack through a wall, another sack, or the far side of a vehicle.
            int hits=Physics.RaycastNonAlloc(forkAnchor.position+Vector3.up*.25f,delta.normalized,groundHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            bool clear=hits<groundHits.Length;
            for(int j=0;j<hits && clear;j++)if(!groundHits[j].transform.IsChildOf(transform) && !groundHits[j].transform.IsChildOf(bag.transform))clear=false;
            if(clear){best=bag;distance=d;}
        }
        return best;
    }
    public bool TryAutoLift(GrainContainer bag)
    {
        return bag!=null && bag!=lastDropped && forkAnchor!=null &&
            (bag.WorldBounds.ClosestPoint(forkAnchor.position)-forkAnchor.position).sqrMagnitude<=.04f && TryLift(bag);
    }
    public bool TryLift(GrainContainer bag)
    {
        if(cargo!=null || forkAnchor==null || !CanLift(bag) || (bag.WorldBounds.ClosestPoint(forkAnchor.position)-forkAnchor.position).sqrMagnitude>pickupReach*pickupReach)return false;
        if(bag.TryGetComponent<VehicleCargoPlacement>(out var placement))placement.ReleaseScale();
        cargo=bag;cargoBody=bag.GetComponent<Rigidbody>();if(cargoBody==null)cargoBody=bag.gameObject.AddComponent<Rigidbody>();
        cargoBody.isKinematic=true;cargoBody.useGravity=false;cargoColliders=bag.GetComponentsInChildren<Collider>(true);
        originalColliderStates=new bool[cargoColliders.Length];for(int i=0;i<cargoColliders.Length;i++)originalColliderStates[i]=cargoColliders[i].enabled;
        if(vehicleColliders==null)vehicleColliders=GetComponentsInChildren<Collider>(true);
        foreach(var own in vehicleColliders)foreach(var other in cargoColliders)if(own!=null && other!=null)Physics.IgnoreCollision(own,other,true);
        bag.transform.SetParent(forkAnchor,true);
        var bounds=bag.WorldBounds;bag.transform.position+=forkAnchor.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        seatedLocalPosition=bag.transform.localPosition;liftBlend=0;bag.SetCarried(true);GameplayAudio.Instance?.PickUp();return true;
    }
    public bool TryDrop()
    {
        if(cargo==null || forkAnchor==null)return false;
        Bounds bounds=cargo.WorldBounds;Vector3 half=bounds.extents;
        Vector3 center=default;bool found=false;
        // The floor or a truck bed can support the bag; the volume above must be clear.
        Vector3 origin=new Vector3(bounds.center.x,bounds.max.y+.3f,bounds.center.z);
        int count=Physics.RaycastNonAlloc(origin,Vector3.down,groundHits,4f,~0,QueryTriggerInteraction.Ignore);
        float nearest=float.PositiveInfinity;
        for(int i=0;i<count;i++)
        {
            var hit=groundHits[i];if(Ignore(hit.collider) || hit.normal.y<.7f || hit.distance>=nearest)continue;
            center=hit.point+Vector3.up*(half.y+.08f);nearest=hit.distance;found=true;
        }
        if(!found || count==groundHits.Length)return false;
        count=Physics.OverlapBoxNonAlloc(center,half*.98f,overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length)return false;
        for(int i=0;i<count;i++)if(!Ignore(overlaps[i]))return false;
        var bag=cargo;bag.transform.SetParent(null,true);bag.transform.position+=center-bag.WorldBounds.center;
        foreach(var own in vehicleColliders)foreach(var other in cargoColliders)if(own!=null && other!=null)Physics.IgnoreCollision(own,other,false);
        for(int i=0;i<cargoColliders.Length;i++)if(cargoColliders[i]!=null)cargoColliders[i].enabled=originalColliderStates[i];
        lastDropped=bag;bag.SetCarried(false);cargoBody.mass=Mathf.Max(1,bag.Kilograms+1);cargoBody.useGravity=true;cargoBody.isKinematic=false;
        cargoBody.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
        cargo=null;cargoBody=null;cargoColliders=null;originalColliderStates=null;Physics.SyncTransforms();GameplayAudio.Instance?.Drop();return true;
    }
    private bool Ignore(Collider collider) => collider==null || collider.transform.IsChildOf(transform) || (cargo!=null && collider.transform.IsChildOf(cargo.transform));
    private void OnDestroy()
    {
        if(cargo==null)return;
        cargo.transform.SetParent(null,true);cargo.SetCarried(false);
        if(cargoBody!=null){cargoBody.useGravity=true;cargoBody.isKinematic=false;}
    }
}
