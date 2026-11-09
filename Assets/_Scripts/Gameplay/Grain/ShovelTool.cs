using UnityEngine;

/// <summary>Every use plays a gesture. Scoops transfer grain from a reachable, unobstructed target.</summary>
[DisallowMultipleComponent]
public sealed class ShovelTool : MonoBehaviour
{
    [SerializeField] private Transform bladeTip;
    [SerializeField] private Transform gripAnchor;
    [SerializeField, Range(.02f, .25f)] private float maximumGripTravel = .16f;
    [SerializeField, Range(5f, 60f)] private float maximumDownwardTilt = 25f;
    [SerializeField, Range(5f, 60f)] private float maximumUpwardTilt = 30f;
    [SerializeField, Range(5f, 70f)] private float maximumPourDownwardTilt = 40f;
    [SerializeField, Range(0f, 30f)] private float extraPourRoll = 12f;
    [SerializeField] private Transform loadVisual;
    [SerializeField] private ShovelGrainPhysics grainPhysics;
    [SerializeField, Min(.1f)] private float reach = 2.6f;
    [SerializeField, Min(.1f)] private float pourReach = 4f;
    [SerializeField, Range(.16f, .8f)] private float aimedGripTravel = .65f;
    [SerializeField, Min(.01f)] private float capacityKg = 6.5f;
    [SerializeField, Min(.01f)] private float weightKg = 1f;
    [SerializeField] private ShovelUpgradeSettings upgradeSettings;
    [SerializeField, Min(.05f)] private float scoopRadius = .4f;
    [SerializeField, Min(.01f)] private float scoopDepth = .18f;
    [SerializeField, Min(.1f)] private float actionDuration = 1.25f;
    [SerializeField] private LayerMask interactionMask = ~0;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly RaycastHit[] pourAssistHits = new RaycastHit[32];

    private GrainContainer FindPourContainer(Camera camera, Transform player)
    {
        // A direct container hit keeps its normal full/mismatched-grain behavior.
        if (FindHit(camera, player, out var direct))
        {
            var container = direct.collider.GetComponentInParent<GrainContainer>();
            if (container != null) return container;
        }
        Ray ray = PlayerAim.CenterRay(camera);
        int count = Physics.SphereCastNonAlloc(ray, .35f, pourAssistHits, pourReach,
            interactionMask, QueryTriggerInteraction.Ignore);
        if (count == pourAssistHits.Length) return null;
        GrainContainer best = null;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = pourAssistHits[i];
            if (hit.transform.IsChildOf(player) || hit.distance >= nearest) continue;
            var container = hit.collider.GetComponentInParent<GrainContainer>();
            if (!CanPourInto(container) || !CanReachPoint(camera, player, container.PourPoint,
                    pourReach, hit.collider)) continue;
            best = container;
            nearest = hit.distance;
        }
        return best;
    }
    private GrainPile pendingPile;
    private bool dropping;
    private Collider dropSurface;
    private GrainPile dropReceiver;
    private readonly GrainCargo dropBatch = new GrainCargo();
    private bool smallPileScoop;
    private bool pourSoundPlayed;
    private bool dropCommitted;
    private GrainContainer pendingContainer;
    private Vector3 targetPoint, restPosition, tipInHands, gripInHands;
    private Vector3 scoopDirection, scoopTipInParent;
    private Quaternion restRotation;
    private Quaternion levelCarryRotation;
    private float carryBlend;
    private float elapsed, transferredFraction, availableAtStart;
    private bool queued;
    private bool scoopReported;
    private bool scoopSoundPlayed;
    private readonly GrainCargo strokeGrain = new GrainCargo();
    private Camera actionCamera;
    private Transform actor;
    private VacuumTool[] vacuums;
    private HeldGrainToolLevels heldLevels;
    public VacuumTool ActiveVacuum
    {
        get
        {
            if(vacuums==null)vacuums=GetComponentsInChildren<VacuumTool>(true);
            foreach(var vacuum in vacuums)if(vacuum!=null&&vacuum.isActiveAndEnabled)return vacuum;
            return null;
        }
    }
    private float ScoopScale
    {
        get
        {
            var settings = UpgradeSettings;
            float startingCapacity = settings != null && settings.IsConfigured ? settings.Capacity(0) : capacityKg;
            return Mathf.Clamp(Mathf.Pow(CapacityKg / Mathf.Max(.01f, startingCapacity), 1f / 3f), 1f, 2.4f);
        }
    }
    // World-space contact dimensions; the pile must not multiply this radius again.
    private float EffectiveScoopRadius => scoopRadius * ScoopScale * 3f;
    private float EffectiveScoopDepth => scoopDepth * Mathf.Sqrt(ScoopScale) * 3f;
    public bool IsVacuum => ActiveVacuum != null;
    public Vector3 VacuumDropPoint => pendingContainer != null ? pendingContainer.PourPoint : targetPoint;
    private readonly GrainCargo cargo = new GrainCargo();
    private readonly GrainCargo scoopCargo = new GrainCargo();
    // Keep the old private preload setter for editor fixtures; cargo owns runtime mass.
    public float Kilograms { get => cargo.Total; private set { cargo.Clear(); cargo.Add(GrainType.Corn, value); } }
    public GrainCargo Cargo => cargo;
    internal GrainCargo VacuumCollectedCargo => scoopCargo;
    private float nextVacuumMixWarning;
    private void ClearTinyRemainder()
    {
        // Only called between actions: don't discard the small increments of an active scoop.
        if (cargo.Total <= 0f || cargo.Total >= .05f) return;
        cargo.Clear();
        strokeGrain.Clear();
        if (grainPhysics != null) grainPhysics.SetFill(0f);
    }
    private ShovelUpgradeSettings UpgradeSettings
    {
        get
        {
            upgradeSettings = GrainEconomySettings.ResolveShovel(upgradeSettings);
            return upgradeSettings;
        }
    }
    public float CapacityKg => ActiveVacuum != null ? ActiveVacuum.capacityKg : UpgradeSettings != null && UpgradeSettings.IsConfigured
        ? UpgradeSettings.Capacity(SaveSystem.Data?.shovelUpgradeLevel ?? 0) : capacityKg;
    internal ShovelUpgradeSettings ProgressionSettings => UpgradeSettings;
    public float WeightKg => UpgradeSettings != null && UpgradeSettings.IsConfigured
        ? UpgradeSettings.Weight(SaveSystem.Data?.shovelUpgradeLevel ?? 0) : weightKg;
    public bool IsBusy { get; private set; }
    public bool IsScooping => !IsVacuum && IsBusy && pendingPile != null;
    public event System.Action GrainScooped;
    public float ActionProgress => IsBusy ? Mathf.Clamp01(elapsed / actionDuration) : 0;
    public Vector3 PositionOffset { get; private set; }
    public Quaternion RotationOffset { get; private set; } = Quaternion.identity;
    public float CarryBlend => carryBlend;

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        heldLevels = GetComponent<HeldGrainToolLevels>();
        CacheShovelAnchors();
    }

    public void BindShovel(HeldShovelPose pose)
    {
        if (pose == null) return;
        CancelAction();
        if (grainPhysics != null) grainPhysics.SetFill(0);
        bladeTip = pose.bladeTip;
        gripAnchor = pose.gripAnchor;
        grainPhysics = pose.grainPhysics;
        carryBlend = 0;
        CacheShovelAnchors();
        if (grainPhysics != null) grainPhysics.SetFill(Kilograms / CapacityKg);
    }

    private void CacheShovelAnchors()
    {
        if (bladeTip != null) tipInHands = transform.InverseTransformPoint(bladeTip.position);
        gripInHands = gripAnchor != null ? transform.InverseTransformPoint(gripAnchor.position) : Vector3.zero;
        if (grainPhysics != null)
        {
            Vector3 bowlUp = transform.InverseTransformDirection(grainPhysics.transform.up);
            float roll = Mathf.Atan2(bowlUp.x, bowlUp.y) * Mathf.Rad2Deg;
            levelCarryRotation = Quaternion.AngleAxis(Mathf.Clamp(roll, -35f, 35f), Vector3.forward);
        }
        else levelCarryRotation = Quaternion.identity;
        if (loadVisual != null) loadVisual.gameObject.SetActive(false);
    }

    public bool CanPourInto(GrainContainer container)
    {
        if (!isActiveAndEnabled || container == null || !container.isActiveAndEnabled ||
            !container.IsOpen || container.IsFull || container.IsClosing || container.IsCarried ||
            Kilograms <= .0001f) return false;
        if (container.GetComponentInParent<WheelbarrowCargo>() != null) return false;
        for (int i = 0; i < GrainCargo.TypeCount; i++)
            if (cargo[(GrainType)i] > .0001f && container.Accepts((GrainType)i)) return true;
        return false;
    }

    public bool TryUse(Camera camera, Transform player)
    {
        if(ActiveVacuum != null)
        {
            if(!isActiveAndEnabled || camera==null || player==null || Time.timeScale<=0 || ActiveVacuum.Emptying)return false;
            ActiveVacuum.RequestSuction(camera,player);return true;
        }
        if (!isActiveAndEnabled || camera == null || player == null || bladeTip == null || transform.parent == null || Time.timeScale <= 0) return false;
        if (IsBusy) { queued = true; return true; }
        ClearTinyRemainder();
        actionCamera = camera;
        actor = player;
        pendingPile = null; pendingContainer = null; smallPileScoop = false;
        Vector3 restTip = transform.parent.TransformPoint(restPosition + restRotation * tipInHands);
        targetPoint = restTip + camera.transform.forward * .38f - Vector3.up * .06f;
        if (FindHit(camera, player, out RaycastHit hit))
        {
            targetPoint = hit.point;
            var pile = hit.collider.GetComponentInParent<GrainPile>();
            var container = hit.collider.GetComponentInParent<GrainContainer>();
            Vector3 scoopPoint = hit.point;
            // Prefer compatible stock before choosing the closest pile. Otherwise
            // an overlapping spill of another crop can steal the entire stroke.
            if (cargo.Total > 0 && container == null &&
                (pile != null || (hit.normal.y > .65f && hit.collider.attachedRigidbody == null)))
            {
                strokeGrain.Clear();
                strokeGrain.Add(cargo.Dominant, 1);
                float nearest = Mathf.Clamp(EffectiveScoopRadius * 3f, 1.25f, 2.5f);
                foreach (var candidate in FindObjectsByType<GrainPile>(FindObjectsSortMode.None))
                {
                    if (!candidate.TryNearestSurface(hit.point, nearest, out var point, strokeGrain) ||
                        !CanReachPoint(camera, player, point, reach, candidate.GetComponent<Collider>())) continue;
                    nearest = Vector3.Distance(hit.point, point);
                    pile = candidate; scoopPoint = point;
                }
            }
            if (pile == null && container == null && hit.normal.y > .65f && hit.collider.attachedRigidbody == null)
            {
                float nearest = 1.25f;
                foreach (var candidate in FindObjectsByType<GrainPile>(FindObjectsSortMode.None))
                {
                    if (!candidate.TryNearestSurface(hit.point, nearest, out var point) ||
                        !CanReachPoint(camera, player, point, reach, candidate.GetComponent<Collider>())) continue;
                    nearest = Vector3.Distance(hit.point, point); pile = candidate; scoopPoint = point;
                }
            }
            if (pile != null && pile.isActiveAndEnabled && pile.IsInsideBoundary(scoopPoint))
            {
                if (Kilograms >= CapacityKg - .0001f) return false;
                pendingPile = pile;
                targetPoint = scoopPoint;
                smallPileScoop = pile.IsDroppedPile || pile.Size * Mathf.Max(pile.WorldScale.x, pile.WorldScale.z) <= 2f;
                targetPoint.y = pile.SurfaceHeight(targetPoint);
                scoopDirection = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                if (scoopDirection.sqrMagnitude < .001f) scoopDirection = player.forward;
            }
        }
        elapsed = transferredFraction = 0;
        scoopReported = false;
        scoopSoundPlayed = false;
        strokeGrain.Clear();
        if (pendingPile != null)
        {
            if (cargo.Total > 0) strokeGrain.Add(cargo.Dominant, 1);
        }
        availableAtStart = pendingContainer != null ? Kilograms : Mathf.Max(0, CapacityKg - Kilograms);
        if (pendingPile != null && grainPhysics != null) grainPhysics.BeginScoop();
        IsBusy = true;
        return true;
    }

    public bool TryDrop(Camera camera, Transform player)
    {
        if(ActiveVacuum != null && !ActiveVacuum.Emptying)IsBusy=false;
        if (IsBusy || Kilograms <= .0001f || !isActiveAndEnabled || camera == null || player == null ||
            bladeTip == null || transform.parent == null || Time.timeScale <= 0) return false;
        var targetContainer = FindPourContainer(camera, player);
        if (targetContainer != null && targetContainer.isActiveAndEnabled &&
            !targetContainer.IsBigBag && targetContainer.HasAssignedGrain && cargo[targetContainer.AcceptedGrain] <= .0001f)
        {
            GameplayCanvas.Instance?.ShowWarning(WarningType.mixGrainNotAllowedInContainer);
            return false;
        }
        if (CanPourInto(targetContainer))
        {
            actionCamera = camera; actor = player;
            pendingPile = null;
            pendingContainer = targetContainer;
            targetPoint = pendingContainer.PourPoint + Vector3.up * .35f + camera.transform.right * .3f;
            dropping = false; dropCommitted = false; queued = false;
            pourSoundPlayed = false;
            elapsed = transferredFraction = 0; availableAtStart = Kilograms;
            if (grainPhysics != null) grainPhysics.SetPourTarget(pendingContainer.PourPoint, pendingContainer.GetComponent<Collider>());
            IsBusy = true;
            if(ActiveVacuum != null)ActiveVacuum.BeginEmpty();
            return true;
        }
        if (!FindDropHit(camera, player, out RaycastHit hit) ||
            hit.collider.GetComponentInParent<GrainContainer>() != null || hit.collider.attachedRigidbody != null) return false;
        var pile = hit.collider.GetComponentInParent<GrainPile>();
        // Excavation can leave steep grain faces. They are valid receivers even
        // when too steep to count as walkable ground; ordinary walls are not.
        if (pile != null)
        {
            if (!pile.isActiveAndEnabled || !pile.IsInsideBoundary(hit.point) || hit.normal.y < 0f) return false;
        }
        else if (hit.normal.y < .65f) return false;
        actionCamera = camera; actor = player;
        dropReceiver = pile;
        pendingPile = null; pendingContainer = null;
        targetPoint = hit.point; dropSurface = hit.collider;
        dropping = true; dropCommitted = false; queued = false;
        pourSoundPlayed = false;
        elapsed = transferredFraction = 0; availableAtStart = Kilograms;
        if (grainPhysics != null) grainPhysics.SetPourTarget(targetPoint, hit.collider);
        IsBusy = true;
        if(ActiveVacuum != null)ActiveVacuum.BeginEmpty();
        return true;
    }

    private void CommitDrop(float maximumKg = float.MaxValue)
    {
        // Follow the growing surface so later portions do not target a point buried inside it.
        if (dropReceiver != null) targetPoint.y = dropReceiver.SurfaceHeight(targetPoint);
        if (dropSurface == null || !dropSurface.enabled || !dropSurface.gameObject.activeInHierarchy ||
            !CanReachPoint(actionCamera, actor, targetPoint, pourReach, dropSurface)) return;
        GrainPile receiver = dropReceiver != null ? dropReceiver : dropSurface.GetComponentInParent<GrainPile>();
        GrainPile source = receiver;
        Vector3 point = targetPoint;
        if (receiver == null)
        {
            float nearest = .45f;
            foreach (var pile in FindObjectsByType<GrainPile>(FindObjectsSortMode.None))
            {
                if (source == null) source = pile;
                if (!pile.TryNearestSurface(point, nearest, out Vector3 surface)) continue;
                if (Physics.Linecast(point + Vector3.up * .08f, surface + Vector3.up * .08f, out var obstruction,
                    interactionMask, QueryTriggerInteraction.Ignore) && obstruction.collider.GetComponentInParent<GrainPile>() != pile) continue;
                nearest = Vector3.Distance(point, surface); receiver = pile; targetPoint = surface;
            }
            if (receiver != null) point = targetPoint;
        }
        bool created = receiver == null;
        if (created)
        {
            if (source == null) return; // Keep cargo if no grain rendering assets are available.
            receiver = GrainPile.CreateDroppedPile(point, source, availableAtStart);
        }
        if (grainPhysics != null) grainPhysics.SetPourGrain(cargo.Dominant);
        cargo.TransferTo(dropBatch, maximumKg);
        if (!receiver.Deposit(point, dropBatch, GrainPile.DropRadius(availableAtStart)))
        {
            dropBatch.TransferTo(cargo, float.MaxValue);
            if (created) Destroy(receiver.gameObject);
        }
        else
        {
            dropReceiver = receiver;
            dropSurface = receiver.GetComponent<Collider>();
        }
        if (grainPhysics != null) grainPhysics.SetFill(Kilograms / CapacityKg);
    }

    private bool CanReachPoint(Camera camera, Transform player, Vector3 point, float distance, Collider receiver)
    {
        Vector3 delta = point + Vector3.up * .025f - camera.transform.position;
        if (delta.magnitude > distance) return false;
        int count = Physics.RaycastNonAlloc(camera.transform.position, delta.normalized, hits, delta.magnitude,
            interactionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        for (int i = 0; i < count; i++)
            if (!hits[i].transform.IsChildOf(player) && hits[i].collider != receiver) return false;
        return true;
    }

    private bool FindDropHit(Camera camera, Transform player, out RaycastHit hit)
    {
        // Ground pouring gets the same reach as bag pouring.
        if (FindHit(camera, player, out hit, true)) return true;
        // Looking ahead: pour onto the ground a short distance in front of the player.
        Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
        Vector3 origin = camera.transform.position + forward * 1.1f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, pourReach, interactionMask, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(player) || hits[i].distance >= nearest) continue;
            nearest = hits[i].distance; hit = hits[i];
        }
        return count < hits.Length && nearest < float.PositiveInfinity &&
            CanReachPoint(camera, player, hit.point, pourReach, hit.collider);
    }

    private void OnEnable() => GoalContext.ReportValue(this, GoalType.ShovelEquipped, 1f);

    private bool FindHit(Camera camera, Transform player, out RaycastHit nearest, bool groundPour = false)
    {
        nearest = default;
        float queryReach = Kilograms > .0001f ? Mathf.Max(reach, pourReach) : reach;
        Ray ray = PlayerAim.CenterRay(camera);
        int count = Physics.RaycastNonAlloc(ray, hits,
            queryReach, interactionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(player) || hits[i].distance >= distance) continue;
            nearest = hits[i]; distance = hits[i].distance;
        }
        if (distance == float.PositiveInfinity) return false;
        // Resolve the nearest obstruction first; the longer range applies only to containers.
        return groundPour || distance <= reach || nearest.collider.GetComponentInParent<GrainContainer>() != null;
    }

    public GrainContainer FindBagForHandling(Camera camera, Transform player)
    {
        if (camera == null || player == null) return null;
        if (Kilograms > .0001f)
        {
            var pourTarget = FindPourContainer(camera, player);
            if (CanPourInto(pourTarget)) return pourTarget;
        }
        Ray ray = PlayerAim.CenterRay(camera);
        int count = Physics.RaycastNonAlloc(ray, hits, pourReach, interactionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return null;
        float distance = float.PositiveInfinity;
        Collider closest = null;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(player) || hits[i].distance >= distance) continue;
            closest = hits[i].collider; distance = hits[i].distance;
        }
        var direct = closest != null ? closest.GetComponentInParent<GrainContainer>() : null;
        if (direct != null && direct.isActiveAndEnabled && !direct.IsCarried) return direct;
        return null;
    }

    public void Tick(float deltaTime)
    {
        if(ActiveVacuum != null)
        {
            if(deltaTime>0)ActiveVacuum.Tick(this,deltaTime);
            IsBusy=ActiveVacuum.Emptying || ActiveVacuum.Sucking;
            if (!IsBusy) ClearTinyRemainder();
            return;
        }
        if (!IsBusy) ClearTinyRemainder();
        if (grainPhysics != null) grainPhysics.SetFill(Kilograms / CapacityKg);
        if (!IsBusy || deltaTime <= 0) return;
        elapsed += deltaTime;
        if (pendingContainer != null)
        {
            targetPoint = pendingContainer.PourPoint + Vector3.up * .35f + actionCamera.transform.right * .3f;
            if (grainPhysics != null) grainPhysics.SetPourTarget(pendingContainer.PourPoint, pendingContainer.GetComponent<Collider>());
        }
        float phase = Mathf.Clamp01(elapsed / actionDuration);
        UpdatePose(phase);
        float fraction = Mathf.SmoothStep(0, 1, pendingPile != null
            ? Mathf.InverseLerp(.32f, .66f, phase) : Mathf.InverseLerp(.32f, .67f, phase));
        if (dropping)
        {
            // Bound mesh rebuilds while distributing the load over the pouring motion.
            float dropFraction = Mathf.Floor(fraction * 6f) / 6f;
            if (!dropCommitted && dropFraction > transferredFraction)
            {
                float before = Kilograms;
                CommitDrop((dropFraction - transferredFraction) * availableAtStart);
                transferredFraction += (before - Kilograms) / Mathf.Max(.0001f, availableAtStart);
                dropCommitted = Kilograms <= .0001f;
                if (Kilograms < before - .0001f) PlayPourSound();
            }
            if (phase >= 1f) CancelAction();
            return;
        }
        float amount = (fraction - transferredFraction) * availableAtStart;
        if (pendingPile == null) transferredFraction = fraction;
        RaycastHit hit = default;
        // The aimed, reachable surface owns the scoop. The cosmetic handle-travel
        // limit can leave the blade above low grain when standing, and excavation
        // lowers it further. Neither should require the player to crouch.
        bool scrapeTarget = CanScrapeTarget();
        bool aimedAtContainer = pendingContainer != null && actionCamera != null && actor != null &&
            FindPourContainer(actionCamera, actor) == pendingContainer;
        if (amount > 0 && actionCamera != null && actor != null && (aimedAtContainer || scrapeTarget || FindHit(actionCamera, actor, out hit)))
        {
            if (pendingPile != null && phase <= .67f && (scrapeTarget || (hit.collider.GetComponentInParent<GrainPile>() == pendingPile && HasScoopContact())))
            {
                Vector3 point = targetPoint; point.y = pendingPile.SurfaceHeight(point);
                scoopCargo.Clear();
                // A spilled heap can be wider than the old 2m "small pile" cutoff.
                // Scrape its remaining footprint without waiting for grain to settle
                // back into the empty contact cell. Authored hangar piles stay local.
                float radius = pendingPile.IsDroppedPile
                    ? Mathf.Max(EffectiveScoopRadius, pendingPile.Size * Mathf.Max(pendingPile.WorldScale.x, pendingPile.WorldScale.z) * 1.5f)
                    : smallPileScoop ? Mathf.Max(.65f, EffectiveScoopRadius) : EffectiveScoopRadius;
                float collected = pendingPile.ScoopCompatibleWith(point, radius,
                    smallPileScoop ? Mathf.Max(.3f, EffectiveScoopDepth) : EffectiveScoopDepth,
                    Mathf.Min(amount, CapacityKg - Kilograms), scoopCargo, strokeGrain, out bool mixBlocked);
                if (mixBlocked && scoopReported)
                {
                    // One click transfers over several frames. Exhausting its
                    // selected crop is a completed partial scoop, not a second
                    // attempt to mix crops. Finish the gesture without a warning.
                    transferredFraction = 1f;
                }
                else if (mixBlocked && Kilograms > .0001f)
                {
                    GameplayCanvas.Instance?.ShowWarning(WarningType.mixGrainNotAllowedInShovel);
                    CancelAction();
                    return;
                }
                GoalContext.Resolve(pendingPile, out int hangar, out _);
                for (int type = 0; type < GrainCargo.TypeCount; type++)
                {
                    float kg = scoopCargo[(GrainType)type];
                    cargo.Add((GrainType)type, kg);
                    if (kg > 0) GoalChecker.ReportProgress(GoalType.GrainCollected, kg, hangar, (GrainType)type);
                }
                // A partial removal is not a completed transfer. Retry the remainder
                // within this stroke instead of losing it to animation progress.
                transferredFraction += collected / Mathf.Max(.0001f, availableAtStart);
                if (collected > 0 && !scoopReported)
                {
                    strokeGrain.Clear();
                    strokeGrain.Add(scoopCargo.Dominant, 1);
                    scoopReported = true;
                }
                // Dust below the automatic cleanup threshold is not a usable load.
                // Wait for the accumulated scoop, rather than sounding on its first tiny increment.
                if (collected > 0 && Kilograms >= .05f && !scoopSoundPlayed)
                {
                    scoopSoundPlayed = true;
                    GameplayAudio.Instance?.ShovelGet();
                    GrainScooped?.Invoke();
                }
                if (collected > 0 && grainPhysics != null)
                    grainPhysics.EmitScoop(pendingPile, point, EffectiveScoopRadius, collected / CapacityKg);
            }
            else if (pendingContainer != null && aimedAtContainer &&
                pendingContainer.isActiveAndEnabled && pendingContainer.IsOpen &&
                !pendingContainer.IsClosing && !pendingContainer.IsCarried &&
                pendingContainer.GetComponentInParent<WheelbarrowCargo>() == null)
            {
                if (grainPhysics != null) grainPhysics.SetPourGrain(pendingContainer.HasAssignedGrain && !pendingContainer.IsBigBag ? pendingContainer.AcceptedGrain : cargo.Dominant);
                float before = Kilograms;
                cargo.PourInto(pendingContainer, Mathf.Min(amount, Kilograms));
                if (Kilograms < before - .0001f) PlayPourSound();
                // Complete floating-point dust through the same capacity/type checks; never discard it.
                float residue = cargo[pendingContainer.IsBigBag ? cargo.Dominant : pendingContainer.AcceptedGrain];
                if (fraction >= 1f && residue > 0 && residue < .0001f) cargo.PourInto(pendingContainer, residue);
            }
            if (grainPhysics != null) grainPhysics.SetFill(Kilograms / CapacityKg);
        }
        if (pendingPile != null && grainPhysics != null)
            grainPhysics.ReleaseScoopDebris(Mathf.InverseLerp(.58f, .82f, phase));
        if (phase >= 1f)
        {
            bool repeat = queued;
            CancelAction();
            if (repeat) TryUse(actionCamera, actor);
        }
    }

    internal void PlayPourSound()
    {
        if (pourSoundPlayed) return;
        pourSoundPlayed = true;
        GameplayAudio.Instance?.FillGrain();
    }

    internal float VacuumCollect(Camera camera,Transform player,float maximum,out Vector3 point)
    {
        point=Vector3.zero;
        if(!FindHit(camera,player,out var hit))return 0;
        var pile=hit.collider.GetComponentInParent<GrainPile>();
        if(pile==null||!pile.isActiveAndEnabled||!pile.IsInsideBoundary(hit.point))return 0;
        point=hit.point;point.y=pile.SurfaceHeight(point);
        scoopCargo.Clear();
        float collected=pile.ScoopCompatibleWith(point,ActiveVacuum.suctionRadius,ActiveVacuum.suctionDepth,
            Mathf.Min(maximum,Mathf.Max(0,CapacityKg-Kilograms)),scoopCargo,cargo,out bool mixBlocked);
        if (mixBlocked && Time.time >= nextVacuumMixWarning)
        {
            nextVacuumMixWarning = Time.time + 1.5f;
            GameplayCanvas.Instance?.ShowWarning(WarningType.mixGrainNotAllowedInShovel);
        }
        GoalContext.Resolve(pile,out int hangar,out _);
        for(int i=0;i<GrainCargo.TypeCount;i++)
        {
            float kg=scoopCargo[(GrainType)i];cargo.Add((GrainType)i,kg);
            if(kg>0)GoalChecker.ReportProgress(GoalType.GrainCollected,kg,hangar,(GrainType)i);
        }
        return collected;
    }

    internal float VacuumEmptyStep(float phase,float dt)
    {
        float fraction = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(VacuumTool.PourStart, VacuumTool.PourEnd, phase));
        float maximum = Mathf.Max(0, availableAtStart * fraction - transferredFraction);
        // At the end, transfer the actual remaining cargo instead of subtracting
        // accumulated float increments from the initial amount.
        if (fraction >= 1f) maximum = Kilograms;
        if (maximum <= 0) return 0;
        float before=Kilograms;
        if(pendingContainer!=null)
        {
            if(CanPourInto(pendingContainer)&&CanReachPoint(actionCamera,actor,pendingContainer.PourPoint,pourReach,pendingContainer.GetComponent<Collider>()))
                cargo.PourInto(pendingContainer,maximum);
        }
        else if(dropping) CommitDrop(maximum);
        transferredFraction += before-Kilograms;
        return before-Kilograms;
    }

    private void UpdatePose(float phase)
    {
        if (pendingPile != null) { UpdateScoopPose(phase); return; }
        Vector3 restTip = restPosition + restRotation * tipInHands;
        Vector3 contact = transform.parent.InverseTransformPoint(targetPoint);
        Vector3 lifted = contact + transform.parent.InverseTransformVector(Vector3.up * .22f - actionCamera.transform.forward * .13f);
        Vector3 a, b;
        Quaternion ra, rb;
        float t;
        // LVL_2-4 use authored blade poses: roll toward the sack from the right.
        float pourRollDirection = pendingContainer != null && heldLevels != null && heldLevels.UsesAuthoredShovelPose ? -1f : 1f;
        Quaternion windup = Quaternion.Euler(-9, -3, 7 * pourRollDirection);
        Quaternion insert = (pendingContainer != null || dropping) ? Quaternion.Euler(-8, -5, -15) : Quaternion.Euler(8, 3, -7);
        Quaternion lift = (pendingContainer != null || dropping) ? Quaternion.Euler(-15, -8, -25) : Quaternion.Euler(-15, -4, 5);
        if (pendingContainer != null || dropping)
        {
            // Raise the blade over a tall/full rim by turning at the grip, rather than
            // translating the entire shovel forward or firing grain through the sack wall.
            Vector3 restGrip = restPosition + restRotation * gripInHands;
            Quaternion aim = Quaternion.Inverse(restRotation) *
                Quaternion.FromToRotation(restTip - restGrip, contact - restGrip) * restRotation;
            aim = Quaternion.RotateTowards(Quaternion.identity, aim, 45f);
            insert = aim * Quaternion.Euler(-4, 0, (-10 - extraPourRoll * .7f) * pourRollDirection);
            lift = aim * Quaternion.Euler(-7, 0, (-18 - extraPourRoll) * pourRollDirection);
        }
        if (phase < .16f)
        { a = restTip; b = restTip + new Vector3(.025f, .055f, -.1f); ra = Quaternion.identity; rb = windup; t = phase / .16f; }
        else if (phase < .38f)
        { a = restTip + new Vector3(.025f, .055f, -.1f); b = contact; ra = windup; rb = insert; t = (phase - .16f) / .22f; }
        else if (phase < .68f)
        { a = contact; b = (pendingContainer != null || dropping) ? contact : lifted; ra = insert; rb = lift; t = (phase - .38f) / .3f; }
        else
        { a = (pendingContainer != null || dropping) ? contact : lifted; b = restTip; ra = lift; rb = Quaternion.identity; t = (phase - .68f) / .32f; }
        t = Mathf.SmoothStep(0, 1, t);
        RotationOffset = Quaternion.Slerp(ra, rb, t);
        // Aim selects the interaction, but cannot pull the handle out of the player's hands.
        // Solve the desired blade pose, then constrain its handle displacement in camera space.
        Vector3 desiredOffset = Vector3.Lerp(a, b, t) - (restPosition + restRotation * RotationOffset * tipInHands);
        Vector3 pivotCorrection = restRotation * (gripInHands - RotationOffset * gripInHands);
        Vector3 gripTravel = desiredOffset - pivotCorrection;
        gripTravel.x = Mathf.Clamp(gripTravel.x, -.065f, .065f);
        gripTravel.y = Mathf.Clamp(gripTravel.y, -.10f, .10f);
        PositionOffset = pivotCorrection + Vector3.ClampMagnitude(gripTravel, maximumGripTravel);
    }

    private bool HasScoopContact()
    {
        if (!pendingPile.isActiveAndEnabled || !pendingPile.IsInsideBoundary(bladeTip.position)) return false;
        Vector3 difference = bladeTip.position - targetPoint;
        float surface = pendingPile.SurfaceHeight(bladeTip.position);
        return new Vector2(difference.x, difference.z).sqrMagnitude <= EffectiveScoopRadius * EffectiveScoopRadius &&
            bladeTip.position.y <= surface + .06f && bladeTip.position.y >= surface - EffectiveScoopDepth - .12f;
    }

    private bool CanScrapeTarget()
    {
        if (actionCamera == null || actor == null || pendingPile == null || !pendingPile.isActiveAndEnabled ||
            !pendingPile.IsInsideBoundary(targetPoint)) return false;
        Vector3 point = targetPoint;
        point.y = pendingPile.SurfaceHeight(point);
        return CanReachPoint(actionCamera, actor, point, reach, pendingPile.GetComponent<Collider>());
    }

    private void UpdateScoopPose(float phase)
    {
        Vector3 restTip = transform.parent.TransformPoint(restPosition + restRotation * tipInHands);
        Vector3 windup = restTip - scoopDirection * .12f + Vector3.up * .07f;
        Vector3 entry = targetPoint - scoopDirection * .04f + Vector3.up * .035f;
        Vector3 buried = targetPoint + scoopDirection * .12f - Vector3.up * Mathf.Min(.18f, scoopDepth);
        Vector3 lifted = targetPoint - scoopDirection * .13f + Vector3.up * .26f;
        Vector3 a, b;
        float t, roll;
        if (phase < .12f)
        { a = restTip; b = windup; t = phase / .12f; roll = Mathf.Lerp(0, -4, t); }
        else if (phase < .34f)
        { a = windup; b = entry; t = (phase - .12f) / .22f; roll = Mathf.Lerp(-4, 2, t); }
        else if (phase < .47f)
        { a = entry; b = buried; t = (phase - .34f) / .13f; roll = Mathf.Lerp(2, -5, t); }
        else if (phase < .55f)
        { a = buried; b = buried; t = (phase - .47f) / .08f; roll = Mathf.Lerp(-5, -3, t); }
        else if (phase < .80f)
        { a = buried; b = lifted; t = (phase - .55f) / .25f; roll = Mathf.Lerp(-3, 5, t); }
        else
        { a = lifted; b = restTip; t = (phase - .80f) / .20f; roll = Mathf.Lerp(5, 0, t); }
        float eased = Mathf.SmoothStep(0, 1, t);
        // Accelerate into the pile, then brace briefly before levering the load out.
        if (phase >= .34f && phase < .47f) eased = t * t;
        Vector3 tip = Vector3.Lerp(a, b, eased);
        // A short levering arc gives the filled blade weight on the way out.
        if (phase >= .55f && phase < .80f) tip += Vector3.up * (Mathf.Sin(t * Mathf.PI) * .045f);
        scoopTipInParent = transform.parent.InverseTransformPoint(tip);
        Vector3 restGrip = restPosition + restRotation * gripInHands;
        Vector3 shaft = tipInHands - gripInHands;
        RotationOffset = Quaternion.Inverse(restRotation) * Quaternion.FromToRotation(restRotation * shaft,
            scoopTipInParent - restGrip) * restRotation * Quaternion.AngleAxis(roll, shaft.normalized);
        PositionOffset = ScoopOffset(RotationOffset);
    }

    private Vector3 ScoopOffset(Quaternion rotation)
    {
        Vector3 pivot = restRotation * (gripInHands - rotation * gripInHands);
        Vector3 travel = scoopTipInParent - (restPosition + restRotation * rotation * tipInHands) - pivot;
        travel.x = Mathf.Clamp(travel.x, -aimedGripTravel, aimedGripTravel);
        travel.y = Mathf.Clamp(travel.y, -.10f, .10f);
        return pivot + Vector3.ClampMagnitude(travel, aimedGripTravel);
    }

    public void ComposePresentation(float dt, Vector2 sway, float turnRoll, Vector3 idle,
        out Vector3 offset, out Quaternion rotation)
    {
        if(ActiveVacuum != null)
        {
            offset=idle+ActiveVacuum.PositionOffset;
            rotation=Quaternion.Euler(sway.y*.25f,-sway.x*.25f,turnRoll*.2f)*ActiveVacuum.RotationOffset;
            return;
        }
        bool preparingLoad = pendingPile != null && ActionProgress > .2f;
        float target = !dropping && pendingContainer == null && (Kilograms > .0001f || preparingLoad) ? 1f : 0f;
        carryBlend = Mathf.MoveTowards(carryBlend, target, dt * 6f);
        Quaternion action = IsBusy ? RotationOffset : Quaternion.identity;
        float swayScale = Mathf.Lerp(1f, .3f, carryBlend);
        Quaternion motion = Quaternion.Euler(sway.y * swayScale, -sway.x * swayScale, turnRoll * swayScale);
        // The leveling references belong to LVL_1. Applying them to the other
        // authored shovel models changes their Editor poses even while idle.
        bool authoredPose = heldLevels != null && heldLevels.UsesAuthoredShovelPose;
        rotation = motion * (authoredPose ? Quaternion.identity :
            Quaternion.Slerp(Quaternion.identity, levelCarryRotation, carryBlend)) * action;
        if (!authoredPose) rotation = LimitDownwardTilt(rotation);
        if (IsScooping)
        {
            // Solve contact after leveling/tilt limits, so cosmetic carry rotation cannot
            // pull the blade back out of the grain during the cutting stroke.
            offset = ScoopOffset(rotation);
            return;
        }
        // Compose every cosmetic rotation at the same handle anchor, preserving its reach limit.
        offset = (IsBusy ? PositionOffset : idle) + restRotation * (action * gripInHands - rotation * gripInHands);
        // Loaded authored shovels keep their empty right-hand pose. Pouring and
        // scooping already use this same rest pose as their animation endpoint.
    }

    private Quaternion LimitDownwardTilt(Quaternion rotation)
    {
        // Limit the actual handle-to-blade angle relative to the horizon. A local Euler
        // clamp would still let the whole shovel hang vertically when the camera looks down.
        Vector3 shaft = tipInHands - gripInHands;
        if (shaft.sqrMagnitude < .0001f || transform.parent == null) return rotation;
        Quaternion frame = transform.parent.rotation * restRotation;
        Vector3 direction = (frame * rotation * shaft).normalized;
        float limit = (pendingContainer != null || dropping) ? maximumPourDownwardTilt : maximumDownwardTilt;
        float elevation = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;
        float limitedElevation = Mathf.Clamp(elevation, -limit, maximumUpwardTilt);
        if (Mathf.Abs(elevation - limitedElevation) < .0001f) return rotation;
        Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (horizontal.sqrMagnitude < .0001f)
            horizontal = Vector3.ProjectOnPlane(transform.parent.forward, Vector3.up);
        if (horizontal.sqrMagnitude < .0001f) horizontal = Vector3.forward;
        Vector3 limited = horizontal.normalized * Mathf.Cos(limitedElevation * Mathf.Deg2Rad)
            + Vector3.up * Mathf.Sin(limitedElevation * Mathf.Deg2Rad);
        Quaternion correction = Quaternion.FromToRotation(direction, limited);
        return Quaternion.Inverse(frame) * correction * frame * rotation;
    }

    public void CancelAction()
    {
        if(vacuums!=null)foreach(var vacuum in vacuums)if(vacuum!=null)vacuum.Cancel();
        IsBusy = queued = dropping = smallPileScoop = false;
        ClearTinyRemainder();
        dropSurface = null;
        pendingPile = null; pendingContainer = null;
        PositionOffset = Vector3.zero;
        RotationOffset = Quaternion.identity;
    }

    private void OnDisable()
    {
        GoalContext.ReportValue(this, GoalType.ShovelEquipped, 0f);
        CancelAction();
    }
}
