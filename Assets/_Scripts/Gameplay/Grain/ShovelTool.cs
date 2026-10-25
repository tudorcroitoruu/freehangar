using UnityEngine;

/// <summary>Every use plays a gesture. Only confirmed contact transfers grain.</summary>
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
    [SerializeField, Range(0f, .75f)] private float bagAimPadding = .35f;
    [SerializeField, Min(.01f)] private float capacityKg = 6.5f;
    [SerializeField, Min(.05f)] private float scoopRadius = .4f;
    [SerializeField, Min(.01f)] private float scoopDepth = .18f;
    [SerializeField, Min(.1f)] private float actionDuration = 1.25f;
    [SerializeField] private LayerMask interactionMask = ~0;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly RaycastHit[] bagHits = new RaycastHit[32];
    private GrainPile pendingPile;
    private GrainContainer pendingContainer;
    private Vector3 targetPoint, restPosition, tipInHands, gripInHands;
    private Quaternion restRotation;
    private Quaternion levelCarryRotation;
    private float carryBlend;
    private float elapsed, transferredFraction, availableAtStart;
    private bool queued;
    private Camera actionCamera;
    private Transform actor;
    public float Kilograms { get; private set; }
    public float CapacityKg => capacityKg;
    public bool IsBusy { get; private set; }
    public float ActionProgress => IsBusy ? Mathf.Clamp01(elapsed / actionDuration) : 0;
    public Vector3 PositionOffset { get; private set; }
    public Quaternion RotationOffset { get; private set; } = Quaternion.identity;
    public float CarryBlend => carryBlend;

    private void Awake()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
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

    public bool TryUse(Camera camera, Transform player)
    {
        if (!isActiveAndEnabled || camera == null || player == null || bladeTip == null || transform.parent == null || Time.timeScale <= 0) return false;
        if (IsBusy) { queued = true; return true; }
        actionCamera = camera;
        actor = player;
        pendingPile = null; pendingContainer = null;
        Vector3 restTip = transform.parent.TransformPoint(restPosition + restRotation * tipInHands);
        targetPoint = restTip + camera.transform.forward * .38f - Vector3.up * .06f;
        if (FindHit(camera, player, out RaycastHit hit))
        {
            targetPoint = hit.point;
            var pile = hit.collider.GetComponentInParent<GrainPile>();
            var container = hit.collider.GetComponentInParent<GrainContainer>();
            if (container != null && container.IsOpen && container.isActiveAndEnabled &&
                Kilograms > .0001f && container.Kilograms < container.CapacityKg)
            {
                pendingContainer = container;
                targetPoint = container.PourPoint + Vector3.up * .35f + camera.transform.right * .3f;
                if (grainPhysics != null) grainPhysics.SetPourTarget(container.PourPoint, container.GetComponent<Collider>());
            }
            else if (pile != null && pile.isActiveAndEnabled && Kilograms < capacityKg - .0001f) pendingPile = pile;
        }
        elapsed = transferredFraction = 0;
        availableAtStart = pendingContainer != null ? Kilograms : Mathf.Max(0, capacityKg - Kilograms);
        if (pendingPile != null && grainPhysics != null) grainPhysics.BeginScoop();
        IsBusy = true;
        return true;
    }

    private bool FindHit(Camera camera, Transform player, out RaycastHit nearest)
    {
        nearest = default;
        float queryReach = Kilograms > .0001f ? Mathf.Max(reach, pourReach) : reach;
        Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
        int count = Physics.RaycastNonAlloc(ray, hits,
            queryReach, interactionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(player) || hits[i].distance >= distance) continue;
            nearest = hits[i]; distance = hits[i].distance;
        }
        // A direct bag hit always wins. Otherwise allow aiming just outside its edges.
        if (distance < float.PositiveInfinity && nearest.collider.GetComponentInParent<GrainContainer>() != null) return true;
        if (Kilograms > .0001f && bagAimPadding > 0 && FindNearbyBag(ray, player, out RaycastHit bag))
        {
            nearest = bag;
            return true;
        }
        if (distance == float.PositiveInfinity) return false;
        // Resolve the nearest obstruction first; the longer range applies only to containers.
        return distance <= reach || nearest.collider.GetComponentInParent<GrainContainer>() != null;
    }

    public GrainContainer FindBagForHandling(Camera camera, Transform player)
    {
        if (camera == null || player == null) return null;
        Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
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
        if (direct != null && direct.CanHandle) return direct;
        return FindNearbyBag(ray, player, out RaycastHit assisted, true) ? assisted.collider.GetComponentInParent<GrainContainer>() : null;
    }

    private bool FindNearbyBag(Ray ray, Transform player, out RaycastHit selected, bool forHandling = false)
    {
        selected = default;
        int count = Physics.SphereCastNonAlloc(ray, bagAimPadding, bagHits, pourReach, interactionMask, QueryTriggerInteraction.Ignore);
        if (count == bagHits.Length) return false;
        float best = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var collider = bagHits[i].collider;
            if (collider.transform.IsChildOf(player)) continue;
            var container = collider.GetComponentInParent<GrainContainer>();
            if (container == null || !container.isActiveAndEnabled || container.IsCarried) continue;
            if (forHandling ? !container.CanHandle : !container.IsOpen || container.IsClosing || container.Kilograms >= container.CapacityKg) continue;
            if (Vector3.Distance(ray.origin, collider.ClosestPoint(ray.origin)) > pourReach) continue;
            Vector3 center = collider.bounds.center;
            float along = Vector3.Dot(center - ray.origin, ray.direction);
            if (along < 0) continue;
            float score = (collider.ClosestPoint(ray.GetPoint(along)) - ray.GetPoint(along)).sqrMagnitude + along * .0001f;
            if (score >= best) continue;
            // Padding is only for aim selection; a real ray must still reach the bag.
            if (!HasBagSight(ray.origin, center, player, container) && !HasBagSight(ray.origin, container.PourPoint, player, container)) continue;
            selected = bagHits[i]; best = score;
        }
        return best < float.PositiveInfinity;
    }

    private bool HasBagSight(Vector3 origin, Vector3 point, Transform player, GrainContainer container)
    {
        Vector3 direction = point - origin;
        int count = Physics.RaycastNonAlloc(origin, direction.normalized, hits, direction.magnitude + .02f,
            interactionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return false;
        float nearest = float.PositiveInfinity;
        Collider blocker = null;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(player) || hits[i].distance >= nearest) continue;
            nearest = hits[i].distance; blocker = hits[i].collider;
        }
        return blocker != null && blocker.GetComponentInParent<GrainContainer>() == container;
    }

    public void Tick(float deltaTime)
    {
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
            ? Mathf.InverseLerp(.38f, .56f, phase) : Mathf.InverseLerp(.32f, .67f, phase));
        float amount = (fraction - transferredFraction) * availableAtStart;
        transferredFraction = fraction;
        if (amount > 0 && actionCamera != null && actor != null && FindHit(actionCamera, actor, out RaycastHit hit))
        {
            if (pendingPile != null && hit.collider.GetComponentInParent<GrainPile>() == pendingPile &&
                Vector2.Distance(new Vector2(hit.point.x, hit.point.z), new Vector2(targetPoint.x, targetPoint.z)) <= scoopRadius)
            {
                Vector3 point = targetPoint; point.y = pendingPile.SurfaceHeight(point);
                float collected = pendingPile.Scoop(point, scoopRadius, scoopDepth, Mathf.Min(amount, capacityKg - Kilograms));
                Kilograms += collected;
                if (collected > 0 && grainPhysics != null)
                    grainPhysics.EmitScoop(pendingPile, point, scoopRadius, collected / capacityKg);
            }
            else if (pendingContainer != null && hit.collider.GetComponentInParent<GrainContainer>() == pendingContainer)
                Kilograms -= pendingContainer.Add(Mathf.Min(amount, Kilograms));
            if (grainPhysics != null) grainPhysics.SetFill(Kilograms / capacityKg);
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

    private void UpdatePose(float phase)
    {
        Vector3 restTip = restPosition + restRotation * tipInHands;
        Vector3 contact = transform.parent.InverseTransformPoint(targetPoint);
        if (pendingPile != null) contact += transform.parent.InverseTransformVector(Vector3.down * .10f + actionCamera.transform.forward * .08f);
        Vector3 lifted = contact + transform.parent.InverseTransformVector(Vector3.up * .22f - actionCamera.transform.forward * .13f);
        Vector3 a, b;
        Quaternion ra, rb;
        float t;
        Quaternion windup = Quaternion.Euler(-9, -3, 7);
        Quaternion insert = pendingContainer != null ? Quaternion.Euler(-8, -5, -15) : Quaternion.Euler(8, 3, -7);
        Quaternion lift = pendingContainer != null ? Quaternion.Euler(-15, -8, -25) : Quaternion.Euler(-15, -4, 5);
        if (pendingContainer != null)
        {
            // Raise the blade over a tall/full rim by turning at the grip, rather than
            // translating the entire shovel forward or firing grain through the sack wall.
            Vector3 restGrip = restPosition + restRotation * gripInHands;
            Quaternion aim = Quaternion.Inverse(restRotation) *
                Quaternion.FromToRotation(restTip - restGrip, contact - restGrip) * restRotation;
            aim = Quaternion.RotateTowards(Quaternion.identity, aim, 45f);
            insert = aim * Quaternion.Euler(-4, 0, -10 - extraPourRoll * .7f);
            lift = aim * Quaternion.Euler(-7, 0, -18 - extraPourRoll);
        }
        if (phase < .16f)
        { a = restTip; b = restTip + new Vector3(.025f, .055f, -.1f); ra = Quaternion.identity; rb = windup; t = phase / .16f; }
        else if (phase < .38f)
        { a = restTip + new Vector3(.025f, .055f, -.1f); b = contact; ra = windup; rb = insert; t = (phase - .16f) / .22f; }
        else if (pendingPile != null && phase < .56f)
        { a = contact; b = contact; ra = insert; rb = insert; t = 0; }
        else if (pendingPile != null && phase < .78f)
        { a = contact; b = lifted; ra = insert; rb = lift; t = (phase - .56f) / .22f; }
        else if (pendingPile != null)
        { a = lifted; b = restTip; ra = lift; rb = Quaternion.identity; t = (phase - .78f) / .22f; }
        else if (phase < .68f)
        { a = contact; b = pendingContainer != null ? contact : lifted; ra = insert; rb = lift; t = (phase - .38f) / .3f; }
        else
        { a = pendingContainer != null ? contact : lifted; b = restTip; ra = lift; rb = Quaternion.identity; t = (phase - .68f) / .32f; }
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

    public void ComposePresentation(float dt, Vector2 sway, float turnRoll, Vector3 idle,
        out Vector3 offset, out Quaternion rotation)
    {
        bool preparingLoad = pendingPile != null && ActionProgress > .2f;
        float target = pendingContainer == null && (Kilograms > .0001f || preparingLoad) ? 1f : 0f;
        carryBlend = Mathf.MoveTowards(carryBlend, target, dt * 6f);
        Quaternion action = IsBusy ? RotationOffset : Quaternion.identity;
        float swayScale = Mathf.Lerp(1f, .3f, carryBlend);
        Quaternion motion = Quaternion.Euler(sway.y * swayScale, -sway.x * swayScale, turnRoll * swayScale);
        rotation = motion * Quaternion.Slerp(Quaternion.identity, levelCarryRotation, carryBlend) * action;
        rotation = LimitDownwardTilt(rotation);
        // Compose every cosmetic rotation at the same handle anchor, preserving its reach limit.
        offset = (IsBusy ? PositionOffset : idle) + restRotation * (action * gripInHands - rotation * gripInHands);
    }

    private Quaternion LimitDownwardTilt(Quaternion rotation)
    {
        // Limit the actual handle-to-blade angle relative to the horizon. A local Euler
        // clamp would still let the whole shovel hang vertically when the camera looks down.
        Vector3 shaft = tipInHands - gripInHands;
        if (shaft.sqrMagnitude < .0001f || transform.parent == null) return rotation;
        Quaternion frame = transform.parent.rotation * restRotation;
        Vector3 direction = (frame * rotation * shaft).normalized;
        float limit = pendingContainer != null ? maximumPourDownwardTilt : maximumDownwardTilt;
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
        IsBusy = queued = false;
        pendingPile = null; pendingContainer = null;
        PositionOffset = Vector3.zero;
        RotationOffset = Quaternion.identity;
    }

    private void OnDisable() => CancelAction();
}
