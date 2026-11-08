using UnityEngine;
using System.Collections.Generic;

/// <summary>One big bag at any fill level on the forks; uses the shared driving rig and its engine loop.</summary>
[DefaultExecutionOrder(400), DisallowMultipleComponent, RequireComponent(typeof(BicycleController))]
public sealed class ForkliftController : MonoBehaviour
{
    [SerializeField] private Transform forkAnchor;
    [SerializeField, Range(.4f,1.5f)] private float enginePitchMultiplier=.72f;
    [SerializeField, Min(.2f)] private float pickupReach=1.7f;
    [SerializeField, Min(.1f)] private float liftHeight=.55f;
    [SerializeField, Min(.1f)] private float vehiclePickupSeconds=1.25f;
    [SerializeField, Min(.1f)] private float vehiclePickupReach=3f;
    public bool IsTransferring => transferring;
    private bool transferring;
    private float transferElapsed;
    private Vector3 transferStart;
    private Quaternion transferRotation, seatedRotation;
    private readonly HashSet<GrainContainer> candidates = new HashSet<GrainContainer>();
    private readonly HashSet<BicycleController> nearbyVehicles = new HashSet<BicycleController>();
    public float EnginePitchMultiplier => enginePitchMultiplier;
    public GrainContainer Cargo => cargo;
    public void RestoreLoadedBag(GrainContainer bag)
    {
        if (bag == null || forkAnchor == null || bag.transform.parent != forkAnchor) return;
        cargo = bag; cargoBody = bag.GetComponent<Rigidbody>();
        cargoColliders = bag.GetComponentsInChildren<Collider>(true);
        originalColliderStates = new bool[cargoColliders.Length];
        for (int i = 0; i < cargoColliders.Length; i++) { originalColliderStates[i] = cargoColliders[i].enabled; cargoColliders[i].enabled = false; }
        transferring = false;
        var bounds = bag.WorldBounds;
        bag.transform.position += forkAnchor.position + Vector3.up * liftHeight - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        liftBlend = 1; seatedLocalPosition = bag.transform.localPosition - Vector3.up * liftHeight;
        if (cargoBody != null) cargoBody.isKinematic = true;
        bag.SetCarried(true);
    }
    private BicycleController rig;
    private GrainContainer cargo;
    private Rigidbody cargoBody;
    private Collider[] cargoColliders, vehicleColliders;
    private Vector3 seatedLocalPosition;
    private float liftBlend;
    private GrainContainer lastDropped;
    private GrainContainer unloadTarget;
    private Transform loadTarget;
    private bool[] originalColliderStates;
    private Collider[] overlaps=new Collider[48];
    private readonly RaycastHit[] groundHits=new RaycastHit[32];
    private string blocked="";
    private float blockedUntil;
    private struct WheelVisual
    {
        public Transform node;
        public Quaternion rest;
        public Vector3 axle, steeringAxis;
        public float radius;
        public bool front;
    }
    private readonly List<WheelVisual> wheelVisuals = new List<WheelVisual>();
    private readonly List<WheelVisual> steeringVisuals = new List<WheelVisual>();
    public static bool CanLift(GrainContainer bag) => bag!=null && bag.isActiveAndEnabled && bag.IsBigBag && !bag.IsCarried && !bag.IsUnderAttack;
    private void Awake()
    {
        rig=GetComponent<BicycleController>();vehicleColliders=GetComponentsInChildren<Collider>(true);
        InitializeWheelVisuals();
    }
    private void InitializeWheelVisuals()
    {
        wheelVisuals.Clear(); steeringVisuals.Clear();
        foreach (var node in GetComponentsInChildren<Transform>(true))
        {
            bool front = node.name.Contains("Wheel_F.");
            bool rear = node.name.Contains("Wheel_R.");
            if ((front || rear) && !node.name.Contains("LOD"))
            {
                // Animate the axle parent once; every wheel LOD follows it.
                float radius = rig.wheelRadius * transform.lossyScale.y;
                var renderer = node.GetComponentInChildren<Renderer>(true);
                if (renderer != null) radius = Mathf.Max(.05f, renderer.bounds.extents.y);
                wheelVisuals.Add(new WheelVisual { node = node, rest = node.localRotation,
                    axle = node.InverseTransformDirection(transform.right),
                    steeringAxis = node.parent.InverseTransformDirection(transform.up), radius = radius, front = front });
            }
            else if (node.name.Contains("Steering_Wheel"))
                steeringVisuals.Add(new WheelVisual { node = node, rest = node.localRotation });
        }
    }
    public void RefreshWheelVisuals()
    {
        if (rig == null) return;
        foreach (var wheel in wheelVisuals)
        {
            if (wheel.node == null) continue;
            float angle = rig.WheelRotationAngle * rig.wheelRadius * transform.lossyScale.y / wheel.radius;
            wheel.node.localRotation = Quaternion.AngleAxis(wheel.front ? rig.CurrentSteeringAngle : 0, wheel.steeringAxis) *
                wheel.rest * Quaternion.AngleAxis(angle, wheel.axle);
        }
        foreach (var wheel in steeringVisuals)
            if (wheel.node != null)
                wheel.node.localRotation = wheel.rest * Quaternion.AngleAxis(-rig.CurrentSteeringAngle * 2.5f, Vector3.right);
    }
    private bool AcceptInput => rig!=null && rig.IsRidden && Time.timeScale>0 && Application.isFocused && Cursor.lockState==CursorLockMode.Locked && !UpgradeManager.BlocksGameplayInput && !UIPopups.BasePopupUI.HasActivePopup;
    private void Update()
    {
        if(!AcceptInput || forkAnchor==null)return;
        unloadTarget = null;
        loadTarget = null;
        if (transferring) return;
        if(lastDropped!=null && (lastDropped.WorldBounds.ClosestPoint(forkAnchor.position)-forkAnchor.position).sqrMagnitude>(pickupReach+.3f)*(pickupReach+.3f))lastDropped=null;
        var bag = FindBag();
        if(cargo==null)
        {
            if (bag != null && SourceVehicle(bag) != null) unloadTarget = bag;
            else TryAutoLift(bag);
        }
        else loadTarget = FindLoadingSlot();
        if (FarmInput.Down("Secondary"))
        {
            if (loadTarget != null) TryLoadOntoVehicle(loadTarget);
            else if (unloadTarget != null) TryLift(unloadTarget);
        }
    }
    public void DropCargo()
    {
        if (cargo == null || transferring) return;
        if (!TryDrop())
        {
            blocked="Make room to drop";blockedUntil=Time.time+1.2f;
        }
    }
    private void LateUpdate()
    {
        RefreshWheelVisuals();
        if(cargo!=null)
        {
            if (transferring)
            {
                transferElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(transferElapsed / Mathf.Max(.1f, vehiclePickupSeconds));
                Vector3 end = seatedLocalPosition + Vector3.up * liftHeight;
                Vector3 raised = transferStart + Vector3.up * .35f;
                Vector3 aboveForks = new Vector3(end.x, Mathf.Max(raised.y, end.y), end.z);
                // Lift the pallet clear of the bed before pulling it onto the forks.
                cargo.transform.localPosition = t < .25f
                    ? Vector3.Lerp(transferStart, raised, Mathf.SmoothStep(0, 1, t / .25f))
                    : t < .75f ? Vector3.Lerp(raised, aboveForks, Mathf.SmoothStep(0, 1, (t - .25f) / .5f))
                    : Vector3.Lerp(aboveForks, end, Mathf.SmoothStep(0, 1, (t - .75f) / .25f));
                cargo.transform.localRotation = Quaternion.Slerp(transferRotation, seatedRotation, Mathf.SmoothStep(0, 1, t));
                if (t >= 1f) { transferring = false; liftBlend = 1f; }
            }
            else
            {
                liftBlend=Mathf.MoveTowards(liftBlend,1,Time.deltaTime*2.5f);
                cargo.transform.localPosition=seatedLocalPosition+Vector3.up*(Mathf.SmoothStep(0,1,liftBlend)*liftHeight);
            }
        }
        if(!AcceptInput || forkAnchor==null)return;
        bool available=cargo!=null && !transferring;
        string action=Time.time<blockedUntil?blocked:transferring?"Unloading big bag":cargo!=null?"Lower and drop big bag":"Approach a vehicle or drive forks under a big bag";
        var data=new InteractionPromptData(forkAnchor.position+Vector3.up*.8f,action,available){InteractionKey=KeyCode.Q};
        if (!transferring && Time.time >= blockedUntil)
        {
            if (cargo != null && loadTarget != null)
                data = new InteractionPromptData(loadTarget.position + Vector3.up * .8f, "Load big bag onto vehicle", true) { InteractionKey = KeyCode.F };
            else if (cargo == null && unloadTarget != null)
                data = new InteractionPromptData(unloadTarget.WorldBounds.center, "Unload big bag from vehicle", true) { InteractionKey = KeyCode.F };
        }
        GameplayCanvas.Instance?.interactionPrompt?.Show(Camera.main,data);
    }
    private GrainContainer FindBag()
    {
        candidates.Clear(); nearbyVehicles.Clear();
        int count;
        // Detailed pickup meshes can exhaust the original 48-contact buffer.
        // Grow and retry instead of silently losing every vehicle target.
        while (true)
        {
            count = Physics.OverlapSphereNonAlloc(forkAnchor.position, Mathf.Max(pickupReach, vehiclePickupReach), overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count < overlaps.Length) break;
            System.Array.Resize(ref overlaps, overlaps.Length * 2);
        }
        for(int i=0;i<count;i++)
        {
            var bag=overlaps[i].GetComponentInParent<GrainContainer>();
            if (bag != null) candidates.Add(bag);
            var vehicle = VehicleCargoOwner.Resolve(overlaps[i]);
            if (vehicle == null || vehicle == rig || !nearbyVehicles.Add(vehicle)) continue;
            // Cargo colliders can be disabled while stowed, so inspect the vehicle's slots too.
            if (vehicle.cargoSlots != null)
                foreach (var slot in vehicle.cargoSlots)
                    if (slot != null) foreach (var loaded in slot.GetComponentsInChildren<GrainContainer>()) candidates.Add(loaded);
            if (vehicle.exclusiveBagSlot != null)
                foreach (var loaded in vehicle.exclusiveBagSlot.GetComponentsInChildren<GrainContainer>()) candidates.Add(loaded);
        }
        GrainContainer best=null;float distance=float.PositiveInfinity;
        foreach (var bag in candidates)
        {
            if(!CanLift(bag) || bag==lastDropped)continue;
            var source = SourceVehicle(bag);
            if (source != null && (source.IsRidden || Mathf.Abs(source.Speed) > .1f)) continue;
            var bounds=bag.WorldBounds;float d=(bounds.ClosestPoint(forkAnchor.position)-forkAnchor.position).sqrMagnitude;
            float reach = source != null ? vehiclePickupReach : pickupReach;
            if (d > reach * reach) continue;
            Vector3 delta=bag.WorldBounds.center-forkAnchor.position;
            if(Vector3.Dot(delta,transform.forward)<-.25f || (source == null && Mathf.Abs(delta.y)>2f) || d>=distance)continue;
            // Walls and other cargo block access to the candidate bag.
            int hits=Physics.RaycastNonAlloc(forkAnchor.position+Vector3.up*.25f,delta.normalized,groundHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            bool clear=hits<groundHits.Length;
            for(int j=0;j<hits && clear;j++)
            {
                var hit = groundHits[j].collider;
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(bag.transform)) continue;
                // The source bed/side panel is cleared by the lift animation; other cargo still blocks access.
                if (source != null && VehicleCargoOwner.Resolve(hit) == source && hit.GetComponentInParent<GrainContainer>() == null) continue;
                clear = false;
            }
            if(clear){best=bag;distance=d;}
        }
        return best;
    }
    public bool TryAutoLift(GrainContainer bag)
    {
        return bag!=null && bag!=lastDropped && forkAnchor!=null &&
            SourceVehicle(bag) == null && (bag.WorldBounds.ClosestPoint(forkAnchor.position)-forkAnchor.position).sqrMagnitude<=.04f && TryLift(bag);
    }
    private Transform FindLoadingSlot()
    {
        Transform best = null;
        float distance = float.PositiveInfinity;
        foreach (var vehicle in nearbyVehicles)
        {
            var first = vehicle.FreeBagCargoSlot(cargo);
            if (first == null) continue;
            if (vehicle.TryGetComponent<PickupCargoLayout>(out var layout))
            {
                // Bay order is not distance order when approaching from the rear or side.
                foreach (var slot in layout.largeSlots) Consider(slot);
            }
            else Consider(first);
        }
        return best;

        void Consider(Transform slot)
        {
            if (!CanLoadOntoVehicle(slot)) return;
            float d = (slot.position - forkAnchor.position).sqrMagnitude;
            if (d < distance) { best = slot; distance = d; }
        }
    }

    private bool CanLoadOntoVehicle(Transform slot)
    {
        if (cargo == null || transferring || forkAnchor == null || slot == null || slot.childCount != 0) return false;
        var vehicle = VehicleCargoOwner.Resolve(slot);
        if (vehicle == null || vehicle == rig || vehicle.IsRidden || Mathf.Abs(vehicle.Speed) > .1f ||
            !VehicleCargoOwner.CanLoadContainer(vehicle, cargo)) return false;
        var first = vehicle.FreeBagCargoSlot(cargo);
        if (first == null) return false;
        var layout = vehicle.GetComponent<PickupCargoLayout>();
        if (layout != null ? !layout.CanLoadAt(slot, cargo) : first != slot) return false;
        var rack = vehicle.GetComponent<VehicleCargoRack>();
        int cost = layout != null ? PickupCargoLayout.Units(cargo) : rack != null ? rack.BagSlotCost : 1;
        var upgrade = vehicle.GetComponent<VehicleUpgrade>();
        if (upgrade != null && (!upgrade.AcceptsContainer(cargo) || !upgrade.CanLoadCargo(cost))) return false;
        Vector3 delta = slot.position - forkAnchor.position;
        if (delta.sqrMagnitude > vehiclePickupReach * vehiclePickupReach || Vector3.Dot(delta, transform.forward) < -.25f) return false;
        Vector3 origin = forkAnchor.position + Vector3.up * .25f;
        delta = slot.position + Vector3.up * .25f - origin;
        int count = Physics.RaycastNonAlloc(origin, delta.normalized, groundHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        if (count == groundHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i].collider;
            if (Ignore(hit)) continue;
            if (VehicleCargoOwner.Resolve(hit) == vehicle && hit.GetComponentInParent<GrainContainer>() == null) continue;
            return false;
        }
        return true;
    }

    public bool TryLoadOntoVehicle(Transform slot)
    {
        if (!CanLoadOntoVehicle(slot)) return false;
        var bag = cargo;
        bag.transform.SetParent(slot, true);
        bag.transform.rotation = Quaternion.AngleAxis(slot.eulerAngles.y, Vector3.up) * bag.UprightRotation;
        bag.transform.position += slot.position - bag.WorldBounds.center;
        bool bottom = VehicleCargoOwner.Rack(slot) != null;
        if (bottom) VehicleCargoRack.SeatBase(bag.transform, slot);
        VehicleCargoPlacement.Attach(bag.transform, slot, bottom);
        foreach (var own in vehicleColliders)
            foreach (var other in cargoColliders)
                if (own != null && other != null) Physics.IgnoreCollision(own, other, false);
        for (int i = 0; i < cargoColliders.Length; i++)
            if (cargoColliders[i] != null) cargoColliders[i].enabled = originalColliderStates[i];
        cargoBody.isKinematic = true;
        bag.SetCarried(false);
        cargo = null; cargoBody = null; cargoColliders = null; originalColliderStates = null;
        loadTarget = null; unloadTarget = null;
        Physics.SyncTransforms();
        if (!WorldSaveRuntime.IsRestoring && bag.Kilograms > 0)
        {
            GoalChecker.ReportProgress(GoalType.SackLoaded);
            for (int i = 0; i < GrainCargo.TypeCount; i++)
                if (bag.Contents[(GrainType)i] > 0)
                    GoalChecker.ReportProgress(GoalType.GrainLoaded, bag.Contents[(GrainType)i], grainType: (GrainType)i);
        }
        GameAchievements.ForkliftDelivered(bag);
        GameplayAudio.Instance?.Drop();
        return true;
    }
    private BicycleController SourceVehicle(GrainContainer bag)
    {
        var vehicle = VehicleCargoOwner.Resolve(bag);
        return vehicle != null && vehicle != rig && vehicle.HasCargo(bag.transform) ? vehicle : null;
    }
    public bool TryLift(GrainContainer bag)
    {
        if(cargo!=null || forkAnchor==null || !CanLift(bag))return false;
        var source = SourceVehicle(bag);
        float reach = source != null ? vehiclePickupReach : pickupReach;
        if ((bag.WorldBounds.ClosestPoint(forkAnchor.position)-forkAnchor.position).sqrMagnitude > reach*reach ||
            (source != null && (source.IsRidden || Mathf.Abs(source.Speed) > .1f))) return false;
        Vector3 startPosition = bag.transform.position;
        Quaternion startRotation = bag.transform.rotation;
        if(bag.TryGetComponent<VehicleCargoPlacement>(out var placement))placement.ReleaseScale();
        cargo=bag;cargoBody=bag.GetComponent<Rigidbody>();if(cargoBody==null)cargoBody=bag.gameObject.AddComponent<Rigidbody>();
        cargoBody.isKinematic=true;cargoBody.useGravity=false;cargoColliders=bag.GetComponentsInChildren<Collider>(true);
        originalColliderStates=new bool[cargoColliders.Length];for(int i=0;i<cargoColliders.Length;i++)originalColliderStates[i]=cargoColliders[i].enabled;
        foreach (var collider in cargoColliders) collider.enabled = false;
        if(vehicleColliders==null)vehicleColliders=GetComponentsInChildren<Collider>(true);
        foreach(var own in vehicleColliders)foreach(var other in cargoColliders)if(own!=null && other!=null)Physics.IgnoreCollision(own,other,true);
        bag.transform.SetParent(forkAnchor,true);
        var bounds=bag.WorldBounds;bag.transform.position+=forkAnchor.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        seatedLocalPosition=bag.transform.localPosition;liftBlend=0;
        transferring = source != null;
        if (transferring)
        {
            seatedRotation = bag.transform.localRotation;
            transferStart = forkAnchor.InverseTransformPoint(startPosition);
            transferRotation = Quaternion.Inverse(forkAnchor.rotation) * startRotation;
            transferElapsed = 0f;
            bag.transform.SetPositionAndRotation(startPosition, startRotation);
        }
        bag.SetCarried(true);GameplayAudio.Instance?.PickUp();return true;
    }
    public bool TryDrop()
    {
        if(cargo==null || forkAnchor==null || transferring)return false;
        Bounds bounds=cargo.WorldBounds;Vector3 half=bounds.extents;
        Vector3 center=bounds.center;bool found=false;
        // Check for support below, but release from the current position without snapping to it.
        Vector3 origin=new Vector3(bounds.center.x,bounds.max.y+.3f,bounds.center.z);
        int count=Physics.RaycastNonAlloc(origin,Vector3.down,groundHits,4f,~0,QueryTriggerInteraction.Ignore);
        float nearest=float.PositiveInfinity;
        for(int i=0;i<count;i++)
        {
            var hit=groundHits[i];if(Ignore(hit.collider) || hit.normal.y<.7f || hit.distance>=nearest)continue;
            if(hit.point.y>bounds.min.y+.02f)return false;
            nearest=hit.distance;found=true;
        }
        if(!found || count==groundHits.Length)return false;
        count=Physics.OverlapBoxNonAlloc(center,half*.98f,overlaps,Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
        if(count==overlaps.Length)return false;
        for(int i=0;i<count;i++)if(!Ignore(overlaps[i]))return false;
        var bag=cargo;bag.transform.SetParent(null,true);
        foreach(var own in vehicleColliders)foreach(var other in cargoColliders)if(own!=null && other!=null)Physics.IgnoreCollision(own,other,false);
        for(int i=0;i<cargoColliders.Length;i++)if(cargoColliders[i]!=null)cargoColliders[i].enabled=originalColliderStates[i];
        lastDropped=bag;bag.SetCarried(false);cargoBody.mass=Mathf.Max(1,bag.Kilograms+1);cargoBody.useGravity=true;cargoBody.isKinematic=false;
        cargoBody.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
        cargo=null;cargoBody=null;cargoColliders=null;originalColliderStates=null;Physics.SyncTransforms();GameAchievements.ForkliftDelivered(bag);GameplayAudio.Instance?.Drop();return true;
    }
    private bool Ignore(Collider collider) => collider==null || collider.transform.IsChildOf(transform) || (cargo!=null && collider.transform.IsChildOf(cargo.transform));
    private void OnDestroy()
    {
        if(cargo==null)return;
        if (cargoColliders != null && originalColliderStates != null)
            for (int i = 0; i < cargoColliders.Length; i++)
                if (cargoColliders[i] != null) cargoColliders[i].enabled = originalColliderStates[i];
        cargo.transform.SetParent(null,true);cargo.SetCarried(false);
        if(cargoBody!=null){cargoBody.useGravity=true;cargoBody.isKinematic=false;}
    }
}
