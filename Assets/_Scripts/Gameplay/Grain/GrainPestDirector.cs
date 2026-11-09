using System.Collections;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(500)]
public sealed class GrainPest : MonoBehaviour
{
    private static readonly System.Collections.Generic.HashSet<GrainPest> activePests = new();

    public static bool IsAttacking(GrainContainer container)
    {
        if (container == null) return false;
        foreach (var pest in activePests)
            if (pest != null && pest.Target == container && pest.IsFeeding) return true;
        return false;
    }

    private void OnEnable() => activePests.Add(this);
    private static GrainPile[] hangarPiles;
    private static float nextHangarScan;
    public static bool IsNearHangar(Vector3 point)
    {
        if (hangarPiles == null || Time.unscaledTime >= nextHangarScan)
        {
            hangarPiles = FindObjectsByType<GrainPile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            nextHangarScan = Time.unscaledTime + 1f;
        }
        foreach (var pile in hangarPiles)
        {
            if (pile == null || pile.IsDroppedPile) continue;
            GoalContext.Resolve(pile, out int hangar, out _);
            if (SaveSystem.Data == null || !SaveSystem.Data.IsHangarOpen(hangar)) continue;
            Vector3 local = pile.transform.InverseTransformPoint(point);
            float half = pile.Size * .5f;
            Vector3 nearest = pile.transform.TransformPoint(new Vector3(
                Mathf.Clamp(local.x, -half, half), local.y, Mathf.Clamp(local.z, -half, half)));
            if ((point - nearest).sqrMagnitude <= 25f) return true;
        }
        return false;
    }
    public static bool CanAttack(GrainContainer target) => target != null && target.isActiveAndEnabled &&
        !target.IsCarried && float.IsFinite(target.Kilograms) && target.Kilograms > .01f &&
        !PlayerPerks.NoAttacksActive;
    public GrainContainer Target { get; private set; }
    public bool IsCrow { get; private set; }
    public bool IsFeeding => isActiveAndEnabled && feedingNow && !fleeing && !climbing &&
        (IsCrow ? crowPerched : onBag) &&
        CanAttack(Target) && (IsCrow || IsNearHangar(Target.transform.position)) &&
        Target.WorldBounds.SqrDistance(transform.position) <= 1.5f * 1.5f && !PlayerPerks.NoAttacksActive;
    private float nextBite;
    private float lastSuccessfulFeed;
    private CapsuleCollider hitCollider;
    private float speed;
    private Animator animator;
    private RuntimeAnimatorController flying, feeding;
    private bool fleeing, feedingNow;
    private GameplayCanvas warningCanvas;
    private bool warningVisible;
    private float fleeUntil;
    private Vector3 fleeDirection;
    private bool hasSlot, wantsToClimb, climbing, onBag;
    private float slotAngle, climbStart, climbDelay, biteAmount = 1f;
    private Vector3 climbFrom;
    private float beginAt, climbDuration, nextShuffle, restUntil;
    private bool crowApproaching;
    private Vector3 crowWaypoint, crowLandingOffset, previousTargetPosition;
    private bool crowPerched;
    private float targetSpeed;
    private GrainEconomySettings economy;
    public void ConfigureEconomy(GrainEconomySettings settings) => economy = settings;
    private float BiteInterval => economy != null ? economy.BiteInterval : 3f;
    private float RatGroupBite => economy != null ? economy.RatBiteKg : 1f;
    private float BirdGroupBite => economy != null ? economy.BirdBiteKg : 1f;
    private float nextIdleSound;
    private float hitEffectAt = -1f;
    private bool lethalHit;
    private Vector3 hitRestScale;
    public Vector3 HitImpulseDirection { private get; set; }
    private Vector3 hitOrigin, hitVelocity;
    private Renderer[] hitRenderers;
    private MaterialPropertyBlock[] hitOriginalBlocks;
    private MaterialPropertyBlock hitBlock;

    public void PlayHitReaction(bool lethal = false)
    {
        if (lethalHit) return;
        if (hitEffectAt >= 0f)
        {
            if (!lethal) return;
            transform.localScale = hitRestScale;
            for (int i = 0; i < hitRenderers.Length; i++)
                if (hitRenderers[i]) hitRenderers[i].SetPropertyBlock(hitOriginalBlocks[i]);
        }
        if (lethal) ScaredAway();
        lethalHit = lethal;
        hitEffectAt = Time.time;
        hitRestScale = transform.localScale;
        hitOrigin = transform.position;
        Vector3 direction = HitImpulseDirection.sqrMagnitude > .001f ? HitImpulseDirection.normalized : -transform.forward;
        hitVelocity = direction * 3f + Vector3.up * 2f;
        hitRenderers = GetComponentsInChildren<Renderer>(true);
        hitOriginalBlocks = new MaterialPropertyBlock[hitRenderers.Length];
        hitBlock = new MaterialPropertyBlock();
        for (int i = 0; i < hitRenderers.Length; i++)
        {
            hitOriginalBlocks[i] = new MaterialPropertyBlock();
            hitRenderers[i].GetPropertyBlock(hitOriginalBlocks[i]);
        }
        UpdateHitReaction();
    }

    private void UpdateHitReaction()
    {
        if (hitEffectAt < 0f) return;
        float phase = Time.time - hitEffectAt;
        transform.localScale = hitRestScale * (1f + (phase < .3f ? .4f * Mathf.Sin(phase / .3f * Mathf.PI) : 0f));
        if (lethalHit)
            transform.position = hitOrigin + hitVelocity * phase + Vector3.down * (4.9f * phase * phase);
        else
        {
            // Broom hits recoil before fleeing; ordinary movement must not overwrite the hop.
            float recoilTime = Mathf.Min(phase, .6f);
            transform.position = hitOrigin + Vector3.ProjectOnPlane(hitVelocity, Vector3.up) * recoilTime +
                Vector3.up * (.35f * Mathf.Sin(recoilTime / .6f * Mathf.PI));
        }
        for (int i = 0; i < hitRenderers.Length; i++)
        {
            var renderer = hitRenderers[i];
            if (!renderer) continue;
            renderer.SetPropertyBlock(hitOriginalBlocks[i]);
            if (phase >= .3f) continue;
            renderer.GetPropertyBlock(hitBlock);
            hitBlock.SetColor("_HitFlashColor", new Color(4f, 0f, 0f, 1f));
            hitBlock.SetColor("_BaseColor", Color.red);
            hitBlock.SetColor("_EmissiveColor", Color.red * 4f);
            hitBlock.SetColor("_EmissionColor", Color.red * 4f);
            renderer.SetPropertyBlock(hitBlock);
        }
        if (phase < .6f) return;
        hitEffectAt = -1f;
        if (lethalHit) Destroy(gameObject);
    }
    private Vector3 CrowPerchPoint()
    {
        Vector3 point = FeedingPoint(new Vector2(crowLandingOffset.x, crowLandingOffset.z));
        // Grain can be deep inside a partly filled barrel/crate. Keep the bird
        // above the rim rather than hiding its model inside the container.
        point.y = Mathf.Max(point.y, Target.WorldBounds.max.y) + .03f;
        return point;
    }
    private Vector3 FeedingPoint(Vector2 offset)
    {
        if (Target.TryGetComponent<BagCornSurface>(out var surface) && surface.TryFeedingPoint(offset, out var point))
            return point;
        var box = Target.GetComponent<BoxCollider>();
        // Collider.bounds is empty while vehicle cargo colliders are disabled.
        // WorldBounds is calculated from local geometry even during transport.
        Bounds bounds = Target.WorldBounds;
        Vector3 top = bounds.center + new Vector3(offset.x * bounds.extents.x, bounds.extents.y + 1f, offset.y * bounds.extents.z);
        if (box != null && box.enabled && box.Raycast(new Ray(top, Vector3.down), out var hit, bounds.size.y + 2f)) return hit.point;
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }
    private void LateUpdate()
    {
        UpdateHitReaction();
        RefreshWarning();
        // Follow the container after vehicle motion and cargo seating have been applied.
        if (IsCrow && crowPerched && !fleeing && Target != null && Target.isActiveAndEnabled)
        {
            transform.position = CrowPerchPoint();
            transform.rotation = Quaternion.Euler(0, Target.transform.eulerAngles.y, 0);
        }
        if (!IsCrow && onBag && !fleeing && Target != null && Target.isActiveAndEnabled)
            transform.position = SlotPoint(true);
    }
    private void EatFromTarget()
    {
        if (!CanAttack(Target) || (!IsCrow && !IsNearHangar(Target.transform.position))) { ScaredAway(); return; }
        if (!IsCrow && !onBag) return;
        if (PlayerPerks.NoAttacksActive || Time.time < nextBite) return;
        Target.ConsumeByPest(biteAmount * (IsCrow ? BirdGroupBite : RatGroupBite));
        lastSuccessfulFeed = Time.time;
        if (!CanAttack(Target)) { ScaredAway(); return; }
        if (!IsCrow && animator != null) animator.SetTrigger("IdleBreak");
        nextBite = Time.time + BiteInterval;
        if (!IsCrow) restUntil = Time.time + Random.Range(.15f, .75f);
    }
    public void ConfigureCrowArrival(Vector3 start, Vector3 doorway, int index)
    {
        transform.position = start;
        crowWaypoint = doorway;
        crowApproaching = true;
        ConfigureCrowGroup(index, 3);
    }
    public void ConfigureCrowGroup(int index, int count)
    {
        count = Mathf.Max(1, count);
        float angle = index * Mathf.PI * 2f / count;
        crowLandingOffset = count == 1 ? Vector3.zero :
            new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .65f;
        biteAmount = 1f / count;
    }
    public bool IsClimbing => climbing;
    public void SetGroupBiteShare(int count) => biteAmount = 1f / Mathf.Max(1, count);
    public bool IsOnBag => onBag;
    public void ConfigureBagSlot(int index, int count, bool climb)
    {
        hasSlot = true; wantsToClimb = climb;
        slotAngle = index * Mathf.PI * 2f / count;
        beginAt = Time.time + Random.Range(.15f, 2.8f);
        climbDelay = beginAt + Random.Range(.8f, 4.5f);
        climbDuration = Random.Range(1.2f, 1.9f);
        nextShuffle = Time.time + Random.Range(5f, 12f);
        nextBite = beginAt + Random.Range(1.5f, 3f);
        biteAmount = 1f / Mathf.Max(1, count);
    }
    private Vector3 SlotPoint(bool upper)
    {
        if (upper)
        {
            Vector3 point = FeedingPoint(new Vector2(Mathf.Cos(slotAngle), Mathf.Sin(slotAngle)) * .4f);
            point.y = Mathf.Max(point.y, Target.WorldBounds.max.y) + .03f;
            return point;
        }
        return GroundRat(RatApproachPoint(Target, slotAngle));
    }
    public static Vector3 RatApproachPoint(GrainContainer target, float angle)
    {
        var vehicle = VehicleCargoOwner.Resolve(target);
        if (vehicle != null && vehicle.GetComponent<PickupController>() != null &&
            vehicle.TryGetComponent<BoxCollider>(out var envelope))
        {
            // The root envelope remains available even when its contacts are disabled.
            // Approach outside the rear bumper, never underneath an elevated cargo slot.
            Vector3 local = vehicle.transform.InverseTransformPoint(target.WorldBounds.center);
            Bounds body = new Bounds(envelope.center, envelope.size);
            local.x = Mathf.Clamp(local.x + Mathf.Cos(angle) * .35f, body.min.x + .2f, body.max.x - .2f);
            local.y = body.min.y;
            local.z = body.min.z - .6f;
            return vehicle.transform.TransformPoint(local);
        }
        Bounds b = target.WorldBounds;
        Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        float radius = Mathf.Max(b.extents.x,b.extents.z) + .45f;
        Vector3 result = b.center + radial * radius;
        result.y = target.transform.position.y + .1f;
        return result;
    }
    public void Initialize(GrainContainer target, bool crow, RuntimeAnimatorController fly, RuntimeAnimatorController eat)
    {
        Target = target; previousTargetPosition = target.transform.position; IsCrow = crow; speed = crow ? 7f : Random.Range(1.8f, 2.6f);
        lastSuccessfulFeed = Time.time;
        biteAmount = 1f;
        beginAt = Time.time + Random.Range(.1f, 1.5f);
        climbDuration = 1.45f;
        animator = GetComponentInChildren<Animator>(); flying = fly; feeding = eat;
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = Random.Range(.88f, 1.12f);
            if (flying != null) animator.runtimeAnimatorController = flying;
        }
        nextBite = Time.time + BiteInterval;
        nextIdleSound = Time.time + Random.Range(.5f, 2f);
        // Pests move by script. Hit detection must not push the dynamic cargo
        // they overlap while feeding or climbing.
        foreach (var body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
        }
        hitCollider = gameObject.AddComponent<CapsuleCollider>();
        // Fit the body, excluding the empty space around wings/tails. Rat size
        // also receives the spawn's 1.5x transform scale.
        hitCollider.center = crow ? new Vector3(0f, .28f, 0f) : new Vector3(0f, .12f, .04f);
        hitCollider.direction = crow ? 1 : 2;
        hitCollider.radius = crow ? .14f : .09f;
        hitCollider.height = crow ? .56f : .42f;
        hitCollider.isTrigger = true;
    }
    private Vector3 GroundRat(Vector3 position)
    {
        if (FarmChicken.TryGroundPoint(position, out var ground)) position.y = ground.y + .1f;
        return position;
    }
    public static bool RatApproachClear(Vector3 from, Vector3 to)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .3f));
        for (int i = 0; i <= steps; i++)
        {
            Vector3 point = Vector3.Lerp(from, to, (float)i / steps);
            if (!FarmChicken.TryGroundPoint(point, out var ground) ||
                !FarmChicken.IsGrainClearAt(ground)) return false;
        }
        return true;
    }
    private void Update()
    {
        if (!IsCrow && !FarmChicken.IsGrainClearAt(transform.position - Vector3.up * .1f))
        {
            ClearWarning();
            feedingNow = false;
            Destroy(gameObject);
            return;
        }
        if (lethalHit || hitEffectAt >= 0f) return;
        if (!fleeing && Time.time - lastSuccessfulFeed > 30f) ScaredAway();
        if(PlayerPerks.NoAttacksActive && !fleeing) ScaredAway();
        if (fleeing)
        {
            Vector3 flat = Vector3.ProjectOnPlane(fleeDirection, Vector3.up);
            if (flat.sqrMagnitude > .001f)
                transform.rotation = Quaternion.LookRotation(flat);
            float acceleration = Mathf.Clamp01((Time.time - (fleeUntil - 3f)) / .3f);
            Vector3 fleeStep = transform.position + (IsCrow ? fleeDirection : flat.normalized) * speed * acceleration * Time.deltaTime;
            transform.position = IsCrow ? fleeStep : GroundRat(fleeStep);
            if (Time.time >= fleeUntil) Destroy(gameObject);
            return;
        }
        if (!CanAttack(Target) || (!IsCrow && !IsNearHangar(Target.transform.position))) { ScaredAway(); return; }
        if (Time.time >= nextIdleSound && (!IsCrow || IsFeeding))
        {
            GameplayAudio.Instance?.PestIdle(IsCrow, transform.position);
            nextIdleSound = Time.time + Random.Range(IsCrow ? 5f : 6f, IsCrow ? 9f : 10f);
        }
        float dt = Time.deltaTime;
        if (dt > .0001f)
            targetSpeed = Mathf.Min(100f, Vector3.Distance(Target.transform.position,previousTargetPosition) / dt);
        previousTargetPosition = Target.transform.position;
        if (IsCrow && crowPerched)
        {
            EatFromTarget();
            return;
        }
        if (!IsCrow && (Time.time < beginAt || Time.time < restUntil))
        {
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
        }
        if (hasSlot && onBag && !climbing && Time.time >= nextShuffle)
        {
            slotAngle += Random.Range(-.4f, .4f);
            nextShuffle = Time.time + Random.Range(5f, 12f);
        }
        Vector3 destination = Target.WorldBounds.center + Vector3.up * (IsCrow ? Target.WorldBounds.extents.y - .05f : -Target.WorldBounds.extents.y + .15f);
        if (!IsCrow) destination.y = Target.transform.position.y + .1f;
        if (hasSlot && !IsCrow)
        {
            destination = SlotPoint(onBag);
            if (climbing)
            {
                float t = Mathf.Clamp01((Time.time - climbStart) / climbDuration);
                Vector3 end = SlotPoint(true);
                Vector3 p = Vector3.Lerp(climbFrom, end, Mathf.SmoothStep(0,1,t));
                var vehicle = VehicleCargoOwner.Resolve(Target);
                if (vehicle != null && vehicle.GetComponent<PickupController>() != null)
                {
                    Vector3 edge = climbFrom;
                    edge.y = Mathf.Max(climbFrom.y, end.y) + .15f;
                    p = t < .5f ? Vector3.Lerp(climbFrom, edge, Mathf.SmoothStep(0, 1, t * 2)) :
                        Vector3.Lerp(edge, end, Mathf.SmoothStep(0, 1, (t - .5f) * 2));
                }
                p.y += Mathf.Sin(t * Mathf.PI) * .18f;
                if (!FarmChicken.IsGrainClearAt(p - Vector3.up * .1f)) { ScaredAway(); return; }
                transform.position = p;
                Vector3 inward = Target.WorldBounds.center - p; inward.y = 0;
                if (inward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(inward) * Quaternion.Euler(-35f * Mathf.Sin(Mathf.PI*t),0,0);
                if (t >= 1) { climbing = false; onBag = true; feedingNow = false; nextBite = Time.time + BiteInterval; }
                return;
            }
            if (wantsToClimb && !onBag && Time.time >= climbDelay && Vector3.Distance(transform.position,destination) < .2f)
            {
                climbing = true; climbStart = Time.time; climbFrom = transform.position;
                if (animator != null) { animator.ResetTrigger("IdleBreak"); animator.SetFloat("Speed", .65f); animator.CrossFadeInFixedTime("Blend Tree", .12f); }
                return;
            }
        }
        if (IsCrow)
        {
            destination = CrowPerchPoint();
            if (crowApproaching)
            {
                if (Vector3.Distance(transform.position,crowWaypoint) > .3f) destination = crowWaypoint;
                else crowApproaching = false;
            }
        }
        if (!IsCrow && !onBag) destination = GroundRat(destination);
        Vector3 delta = destination - transform.position;
        if (delta.magnitude > (crowApproaching ? .15f : hasSlot ? .18f : IsCrow ? .08f : 1.3f))
        {
            feedingNow = false;
            if (IsCrow && animator != null && flying != null && animator.runtimeAnimatorController != flying) animator.runtimeAnimatorController = flying;
            if (!IsCrow && animator != null) animator.SetFloat("Speed", 1f);
            Vector3 step = Vector3.MoveTowards(transform.position, destination, (IsCrow ? Mathf.Max(speed,targetSpeed + 5f) : speed) * Time.deltaTime);
            if (!IsCrow && !onBag && !RatApproachClear(transform.position, step)) { ScaredAway(); return; }
            Vector3 dir = step - transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(dir, Vector3.up);
            if (forward.sqrMagnitude > .0001f)
                transform.rotation = IsCrow ? Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward), Time.deltaTime * 5f)
                    : Quaternion.LookRotation(forward);
            transform.position = !IsCrow && !onBag ? GroundRat(step) : step;
            nextBite = Time.time + BiteInterval;
        }
        else
        {
            if (!IsCrow && !onBag)
            {
                feedingNow = false;
                if (animator != null) animator.SetFloat("Speed", 0f);
                return;
            }
            if (hasSlot)
            {
                Vector3 inward = Target.WorldBounds.center - transform.position; inward.y = 0;
                if (inward.sqrMagnitude > .001f) transform.rotation = Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(inward),Time.deltaTime*5f);
            }
            if (!feedingNow)
            {
                feedingNow = true;
                if (IsCrow) { crowPerched = true; crowApproaching = false; nextBite = Time.time + BiteInterval; }
                if (animator != null)
                {
                    if (IsCrow && feeding != null) animator.runtimeAnimatorController = feeding;
                    else if (!IsCrow) { animator.SetFloat("Speed", 0f); animator.SetTrigger("IdleBreak"); }
                }
            }
            EatFromTarget();
        }
    }
    public bool ScaredAway(Vector3? threatPosition = null)
    {
        if (fleeing) return false;
        ClearWarning();
        if (IsCrow) StartCoroutine(PlayFlyAwaySound());
        if (IsCrow && animator != null && flying != null) animator.runtimeAnimatorController = flying;
        if (!IsCrow && animator != null)
        {
            animator.ResetTrigger("IdleBreak");
            animator.SetFloat("Speed", 1f);
            animator.CrossFadeInFixedTime("Blend Tree", .12f);
        }
        climbing = false; onBag = false; crowPerched = false;
        fleeing = true; Target = null; fleeUntil = Time.time + 3f;
        Vector3 away = threatPosition.HasValue ? transform.position - threatPosition.Value : -transform.forward;
        away.y = 0;
        if (away.sqrMagnitude < .001f) away = -transform.forward;
        fleeDirection = (away.normalized + (IsCrow ? Vector3.up : Vector3.zero)).normalized;
        if (hitCollider != null) hitCollider.enabled = false;
        return true;
    }

    private void RefreshWarning()
    {
        var canvas = GameplayCanvas.Instance;
        if (warningCanvas != canvas)
        {
            ClearWarning();
            warningCanvas = canvas;
        }
        bool visible = IsFeeding;
        if (warningCanvas == null || visible == warningVisible) return;
        warningCanvas.SetWarning(IsCrow ? WarningType.CrowsAttack : WarningType.RatsAttack, this, visible);
        warningVisible = visible;
    }

    private void ClearWarning()
    {
        if (warningVisible && warningCanvas != null)
            warningCanvas.SetWarning(IsCrow ? WarningType.CrowsAttack : WarningType.RatsAttack, this, false);
        warningVisible = false;
        warningCanvas = null;
    }

    private void OnDisable()
    {
        activePests.Remove(this);
        ClearWarning();
    }

    private IEnumerator PlayFlyAwaySound()
    {
        yield return new WaitForSeconds(.15f);
        GameplayAudio.Instance?.CrowFlyAway(transform.position);
    }
}

public sealed class GrainPestDirector : MonoBehaviour
{
    [SerializeField] private GrainEconomySettings economySettings;
    public GameObject ratPrefab, crowPrefab;
    public GrainContainer tutorialBagTemplate;
    public Material tutorialBagMaterial;
    private Texture2D markerTexture;
    public RuntimeAnimatorController ratAnimation, crowFlyAnimation, crowEatAnimation;
    public HangarKeyDoor hangar3, hangar4;
    public Camera playerCamera;
    public PlayerInteractor player;
    [SerializeField, Min(1f)] private float minInterval = 60f, maxInterval = 180f;
    [SerializeField, Min(1f)] private float minAttackDuration = 20f, maxAttackDuration = 35f;
    private float nextAttack, attackTimeRemaining;
    private bool encounterActive;
    private readonly System.Collections.Generic.List<GrainPest> encounterPests = new System.Collections.Generic.List<GrainPest>();
    public float LastAttackEnded { get; private set; } = -1f;
    public float SecondsUntilNextAttack => encounterActive ? 0f : Mathf.Max(0f, nextAttack - Time.time);
    private const string IntroId = "farm.pests.intro";
    private const string CrowIntroId = "farm.pests.crow.intro";
    private bool crowIntroDone, crowLesson;
    private bool introRunning, introDone, showBroomHint;
    private GrainContainer[] preparedRatBags;
    private PlayerController introController;
    private bool introControllerWasEnabled;

    [Space, Header("HUD Indicators")] 
    [SerializeField] private GameObject ratsIndicator;
    [SerializeField] private GameObject crowsIndicator;
    private AttackIndicatorGroup ratsHud, crowsHud;

    private sealed class AttackIndicator
    {
        private readonly GameObject indicator;
        private readonly Transform originalParent;
        private readonly Vector3 originalPosition, originalScale;
        private readonly Quaternion originalRotation;
        public GrainContainer Target { get; private set; }

        public AttackIndicator(GameObject indicator)
        {
            this.indicator = indicator;
            if (indicator == null) return;
            originalParent = indicator.transform.parent;
            originalPosition = indicator.transform.localPosition;
            originalRotation = indicator.transform.localRotation;
            originalScale = indicator.transform.localScale;
            Hide();
        }

        public void Show(GrainContainer target)
        {
            if (indicator == null) return;
            Target = target;
            // Follow the cargo's moving hierarchy without making the reusable indicator
            // a child of a container that can be sold or destroyed.
            var parent = target.transform.parent != null ? target.transform.parent : originalParent;
            if (indicator.transform.parent != parent) indicator.transform.SetParent(parent, true);
            indicator.transform.position = target.WorldBounds.center;
            if (!indicator.activeSelf) indicator.SetActive(true);
        }

        public void Hide()
        {
            if (indicator == null) return;
            if (Target == null && !indicator.activeSelf && indicator.transform.parent == originalParent) return;
            Target = null;
            indicator.SetActive(false);
            indicator.transform.SetParent(originalParent, false);
            indicator.transform.localPosition = originalPosition;
            indicator.transform.localRotation = originalRotation;
            indicator.transform.localScale = originalScale;
        }

        public AttackIndicator CreateDuplicate()
        {
            if (indicator == null) return null;
            var copy = Instantiate(indicator, originalParent, false);
            copy.SetActive(false);
            copy.transform.localPosition = originalPosition;
            copy.transform.localRotation = originalRotation;
            copy.transform.localScale = originalScale;
            return new AttackIndicator(copy);
        }

        public void DestroyDuplicate()
        {
            if (indicator != null) Destroy(indicator);
        }
    }

    private sealed class AttackIndicatorGroup
    {
        private readonly System.Collections.Generic.List<AttackIndicator> indicators = new();
        private readonly System.Collections.Generic.HashSet<GrainContainer> targets = new();

        public AttackIndicatorGroup(GameObject template)
        {
            if (template != null) indicators.Add(new AttackIndicator(template));
        }

        public void Refresh(System.Collections.Generic.List<GrainPest> pests, bool crow)
        {
            if (indicators.Count == 0) return;
            targets.Clear();
            foreach (var pest in pests)
                if (pest != null && pest.IsCrow == crow && pest.IsFeeding) targets.Add(pest.Target);

            // Keep existing assignments so markers do not switch between containers.
            foreach (var indicator in indicators)
            {
                if (indicator.Target != null && targets.Remove(indicator.Target)) indicator.Show(indicator.Target);
                else indicator.Hide();
            }

            foreach (var target in targets)
            {
                var available = indicators.Find(indicator => indicator.Target == null);
                if (available == null)
                {
                    available = indicators[0].CreateDuplicate();
                    if (available == null) continue;
                    indicators.Add(available);
                }
                available.Show(target);
            }
        }

        public void Hide()
        {
            foreach (var indicator in indicators) indicator.Hide();
            targets.Clear();
        }

        public void DestroyDuplicates()
        {
            for (int i = 1; i < indicators.Count; i++) indicators[i].DestroyDuplicate();
        }
    }

    private void Awake()
    {
        ratsHud = new AttackIndicatorGroup(ratsIndicator);
        crowsHud = new AttackIndicatorGroup(crowsIndicator);
    }

    private void RefreshIndicator(AttackIndicatorGroup indicator, bool crow)
    {
        indicator?.Refresh(encounterPests, crow);
    }
#if UNITY_EDITOR
    private bool replayIntroForTesting, replayCrowForTesting;
    public void ReplayCrowIntroForTesting() { if (introRunning) return; replayCrowForTesting = true; crowIntroDone = false; }
    public void ReplayIntroForTesting() { if (introRunning) return; replayIntroForTesting = true; introDone = false; }
#endif
    private void Start() { Schedule(); CreateMarker(); }
    private void CreateMarker()
    {
        markerTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        markerTexture.name = "Pulsing red attack marker";
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float r = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
            float alpha = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.70f, 1f, r));
            markerTexture.SetPixel(x, y, new Color(1f, .04f, .025f, alpha));
        }
        markerTexture.Apply(); markerTexture.hideFlags = HideFlags.HideAndDontSave;
    }
    private void OnDestroy()
    {
        ratsHud?.DestroyDuplicates();
        crowsHud?.DestroyDuplicates();
        if (markerTexture != null) Destroy(markerTexture);
    }
    private void Schedule() => nextAttack = Time.time + Random.Range(Mathf.Max(1f,minInterval), Mathf.Max(minInterval,maxInterval));
    private void EndEncounter()
    {
        foreach (var pest in encounterPests) if (pest != null) pest.ScaredAway();
        encounterPests.Clear();
        ratsHud?.Hide();
        crowsHud?.Hide();
        encounterActive = false;
        showBroomHint = false;
        LastAttackEnded = Time.time;
        Schedule();
    }
    private void Update()
    {
        if (LoadingScreen.IsLoading || (GameManager.Instance != null && GameManager.Instance.IsIntroPlaying)) return;
        if (!SaveSystem.savesLoaded || SaveSystem.Data == null) return;
        if(PlayerPerks.NoAttacksActive)
        {
            if(encounterActive) { EndEncounter();AudioManager.Instance?.StopAttackMusic(); }
            return;
        }
        if (player == null) player = FindFirstObjectByType<PlayerInteractor>();
        if (playerCamera == null && player != null)
        {
            var controller = player.GetComponent<PlayerController>();
            if (controller != null) playerCamera = controller.characterCamera;
        }
        bool introAlreadySeen = BuriedFind.WasFound(SaveSystem.Data, IntroId);
#if UNITY_EDITOR
        if (replayIntroForTesting) introAlreadySeen = false;
#endif
        if (!introDone && !introRunning && hangar3 != null && hangar3.IsUnlocked && !introAlreadySeen &&
            player != null && Vector3.Distance(player.transform.position, hangar3.transform.position) < 12f)
        { StartCoroutine(Introduce()); return; }
        bool crowSeen = BuriedFind.WasFound(SaveSystem.Data, CrowIntroId);
#if UNITY_EDITOR
        if (replayCrowForTesting) crowSeen = false;
#endif
        if (!crowIntroDone && !introRunning && !crowSeen && hangar4 != null && hangar4.IsUnlocked &&
            player != null && Vector3.Distance(player.transform.position,hangar4.transform.position) < 14f)
        { StartCoroutine(IntroduceCrows()); return; }
        if (introRunning) return;
        if (encounterActive)
        {
            encounterPests.RemoveAll(p => p == null || p.Target == null);
            // Travel, climbing and the introduction must not consume the feeding encounter.
            if (encounterPests.Any(p => p.IsFeeding)) attackTimeRemaining -= Time.deltaTime;
            if (attackTimeRemaining <= 0f || encounterPests.Count == 0) EndEncounter();
            return;
        }
        if (Time.time < nextAttack) return;
        Schedule();
        bool ratsAvailable = hangar3 != null && hangar3.IsUnlocked;
        bool crowsAvailable = hangar4 != null && hangar4.IsUnlocked;
        if (!ratsAvailable && !crowsAvailable) return;
        var targets = FindObjectsByType<GrainContainer>(FindObjectsSortMode.None)
            .Where(GrainPest.CanAttack).ToArray();
        if (targets.Length == 0) return;
        var ratTargets = ratsAvailable ? targets.Where(x => GrainPest.IsNearHangar(x.transform.position)).ToArray()
            : System.Array.Empty<GrainContainer>();
        if (ratTargets.Length == 0 && !crowsAvailable) return;
        var vehicleCargo = crowsAvailable ? targets.Where(x =>
        {
            var vehicle = x.GetComponentInParent<BicycleController>();
            return vehicle != null && vehicle.IsRidden;
        }).ToArray() : System.Array.Empty<GrainContainer>();
        // Driving cargo remains eligible even when its colliders are disabled or it is parented to a rack.
        bool attackVehicle = vehicleCargo.Length > 0;
        bool crow = attackVehicle || (crowsAvailable && (ratTargets.Length == 0 || Random.value < .5f));
        var candidates = attackVehicle ? vehicleCargo : crow ? targets : ratTargets;
        var target = candidates[Random.Range(0, candidates.Length)];
        if (crow)
        {
            int count = Random.Range(1, 5);
            for (int i = 0; i < count; i++)
            {
                var bird = Spawn(target, true);
                if (bird != null) bird.ConfigureCrowGroup(i, count);
            }
        }
        else SpawnRatGroup(target, false);
    }
    private void SpawnRatGroup(GrainContainer bag, bool nearby)
    {
        const int count = 6;
        var occupiedSlots = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            Vector3 spawn = default;
            int slot = 0;
            bool found = false;
            int firstSlot = Random.Range(0, 24);
            for (int attempt = 0; attempt < 24; attempt++)
            {
                slot = (firstSlot + attempt) % 24;
                if (occupiedSlots.Contains(slot)) continue;
                float angle = slot * Mathf.PI * 2f / 24f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 approach = GrainPest.RatApproachPoint(bag, angle);
                var vehicle = VehicleCargoOwner.Resolve(bag);
                if (vehicle != null && vehicle.GetComponent<PickupController>() != null)
                    radial = Vector3.ProjectOnPlane(approach - vehicle.transform.position, Vector3.up).normalized;
                Vector3 candidate = approach + radial * (nearby ? 0f : 3f);
                candidate.y = approach.y;
                if (!FarmChicken.TryGroundPoint(candidate, out var ground) ||
                    !GrainPest.RatApproachClear(ground, approach)) continue;
                spawn = ground + Vector3.up * .1f;
                found = true;
                break;
            }
            if (!found) continue;
            var rat = Spawn(bag, false);
            if (rat == null) continue;
            rat.transform.position = spawn;
            occupiedSlots.Add(slot);
            rat.ConfigureBagSlot(slot, 24, true);
            rat.SetGroupBiteShare(count);
        }
    }
    private GrainPest Spawn(GrainContainer target, bool crow)
    {
        var prefab = crow ? crowPrefab : ratPrefab;
        if (prefab == null || !GrainPest.CanAttack(target)) return null;
        if (!crow && !GrainPest.IsNearHangar(target.transform.position)) return null;
        Vector3 direction = Random.insideUnitSphere; direction.y = 0; direction = direction.sqrMagnitude < .01f ? Vector3.forward : direction.normalized;
        Vector3 pos = target.transform.position + direction * (crow ? 14f : 8f) + Vector3.up * (crow ? 9f : .15f);
        var go = Instantiate(prefab, pos, Quaternion.identity);
        if (!crow) go.transform.localScale *= 1.5f;
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        var pest = go.AddComponent<GrainPest>();
        pest.ConfigureEconomy(economySettings);
        pest.Initialize(target, crow, crow ? crowFlyAnimation : ratAnimation, crow ? crowEatAnimation : ratAnimation);
        if (!encounterActive)
        {
            encounterActive = true;
            attackTimeRemaining = Random.Range(Mathf.Max(1f,minAttackDuration),Mathf.Max(minAttackDuration,maxAttackDuration));
        }
        encounterPests.Add(pest);
        return pest;
    }
    private IEnumerator Introduce()
    {
        PrepareRatIntroduction();
        if (preparedRatBags == null) yield break;
        introRunning = true;
        crowLesson = false;
        if (player == null || playerCamera == null || tutorialBagTemplate == null || !player.BeginDiscovery(this))
        { introRunning = false; yield break; }
        introController = player.GetComponent<PlayerController>();
        introControllerWasEnabled = introController != null && introController.enabled;
        if (introControllerWasEnabled) introController.enabled = false;
        yield return new WaitForSeconds(1.7f);
        var bags = preparedRatBags;
        for (int i = 0; i < bags.Length; i++)
        {
            Vector3 delta = bags[i].WorldBounds.center - playerCamera.transform.position;
            Quaternion start = playerCamera.transform.rotation;
            Quaternion end = delta.sqrMagnitude > .01f ? Quaternion.LookRotation(delta) : start;
            float t = 0;
            while (t < 1f) { t += Time.deltaTime; playerCamera.transform.rotation = Quaternion.Slerp(start,end,Mathf.SmoothStep(0,1,t)); yield return null; }
            yield return new WaitForSeconds(1.2f);
        }
        Vector3 midpoint = (bags[0].WorldBounds.center + bags[1].WorldBounds.center) * .5f;
        Quaternion finish = Quaternion.LookRotation(midpoint - playerCamera.transform.position);
        float back = 0; Quaternion from = playerCamera.transform.rotation;
        while (back < .7f) { back += Time.deltaTime; playerCamera.transform.rotation = Quaternion.Slerp(from,finish,Mathf.SmoothStep(0,1,back/.7f)); yield return null; }
        Vector3 euler = finish.eulerAngles;
        player.transform.rotation = Quaternion.Euler(0, euler.y, 0);
        playerCamera.transform.rotation = finish;
        var controller = player.GetComponent<PlayerController>();
        if (controller != null) controller.ResetLook();
        RestoreIntroController();
        player.EndDiscovery(this);
        bool saveIntro = true;
#if UNITY_EDITOR
        if (replayIntroForTesting) { saveIntro = false; replayIntroForTesting = false; }
#endif
        if (saveIntro && SaveSystem.Data != null && !BuriedFind.WasFound(SaveSystem.Data, IntroId))
        {
            var data = SaveSystem.Data; var ids = data.foundSecrets ?? System.Array.Empty<string>();
            System.Array.Resize(ref ids, ids.Length + 1); ids[ids.Length-1] = IntroId; data.foundSecrets = ids; SaveSystem.SaveData();
        }
        introDone = true; introRunning = false; showBroomHint = true; Schedule();
    }
    public void PrepareRatIntroduction()
    {
        if (preparedRatBags != null || introDone || hangar3 == null || tutorialBagTemplate == null ||
            ratPrefab == null || SaveSystem.Data == null || PlayerPerks.NoAttacksActive) return;
        bool seen = BuriedFind.WasFound(SaveSystem.Data, IntroId);
#if UNITY_EDITOR
        if (replayIntroForTesting) seen = false;
#endif
        if (seen) return;
        var building = hangar3.GetComponentInParent<HangarGrainVisibility>();
        if (building == null) return;
        if (encounterActive) EndEncounter();
        preparedRatBags = new GrainContainer[2];
        for (int i = 0; i < preparedRatBags.Length; i++)
        {
            var go = Instantiate(tutorialBagTemplate.gameObject);
            go.name = "Rat tutorial grain bag";
            // Keep both bags within the rats' five-metre range of the grain area.
            go.transform.position = building.transform.position + building.transform.forward * 18.5f +
                building.transform.right * (i == 0 ? -2f : 2f);
            go.SetActive(true);
            var bag = go.GetComponent<GrainContainer>();
            // Match the oats at Hangar 3's entrance so the lesson bags can be topped up nearby.
            bag.SetDemonstrationFill(GrainType.Oats, bag.CapacityKg * .5f);
            preparedRatBags[i] = bag;
            SpawnRatGroup(bag, true);
        }
    }

    private IEnumerator IntroduceCrows()
    {
        if (encounterActive) EndEncounter();
        introRunning = true; crowLesson = true;
        if (player == null || playerCamera == null || tutorialBagTemplate == null || !player.BeginDiscovery(this))
        { introRunning = false; yield break; }
        introController = player.GetComponent<PlayerController>();
        introControllerWasEnabled = introController != null && introController.enabled;
        if (introControllerWasEnabled) introController.enabled = false;
        yield return new WaitForSeconds(1.7f);
        var building = hangar4.GetComponentInParent<HangarGrainVisibility>();
        if (building == null) { RestoreIntroController(); player.EndDiscovery(this); introRunning = false; yield break; }
        var root = building.transform;
        var go = Instantiate(tutorialBagTemplate.gameObject);
        go.name = "Crow tutorial grain bag";
        go.transform.position = root.position + root.forward * 28f;
        go.SetActive(true);
        var bag = go.GetComponent<GrainContainer>();
        bag.SetDemonstrationFill(GrainType.Rice, bag.CapacityKg * .5f);
        Quaternion startLook = playerCamera.transform.rotation;
        Quaternion targetLook = Quaternion.LookRotation(bag.WorldBounds.center - playerCamera.transform.position);
        float turn = 0;
        while (turn < .8f) { turn += Time.deltaTime; playerCamera.transform.rotation = Quaternion.Slerp(startLook,targetLook,Mathf.SmoothStep(0,1,turn/.8f)); yield return null; }
        for (int i = 0; i < 3; i++)
        {
            var crow = Spawn(bag, true);
            if (crow != null)
            {
                Vector3 arrival = root.position + root.forward * (43f+i*3f) + root.right*(i-1)*2f + Vector3.up*(8f+i);
                Vector3 doorway = root.position + root.forward*29f + root.right*(i-1)*.7f + Vector3.up*3.8f;
                crow.ConfigureCrowArrival(arrival,doorway,i);
            }
            yield return new WaitForSeconds(.7f);
        }
        yield return new WaitForSeconds(3f);
        player.transform.rotation = Quaternion.Euler(0,targetLook.eulerAngles.y,0);
        playerCamera.transform.rotation = targetLook;
        if (introController != null) introController.ResetLook();
        RestoreIntroController(); player.EndDiscovery(this);
        bool save = true;
#if UNITY_EDITOR
        if (replayCrowForTesting) { save = false; replayCrowForTesting = false; }
#endif
        if (save && SaveSystem.Data != null && !BuriedFind.WasFound(SaveSystem.Data,CrowIntroId))
        {
            var data = SaveSystem.Data; var ids = data.foundSecrets ?? System.Array.Empty<string>();
            System.Array.Resize(ref ids,ids.Length+1); ids[ids.Length-1]=CrowIntroId; data.foundSecrets=ids; SaveSystem.SaveData();
        }
        crowIntroDone = true; introRunning = false; showBroomHint = true; Schedule();
    }
    private void RestoreIntroController()
    {
        if (introController != null && introControllerWasEnabled) introController.enabled = true;
        introController = null; introControllerWasEnabled = false;
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        if (encounterActive) EndEncounter();
        ratsHud?.Hide();
        crowsHud?.Hide();
        RestoreIntroController();
        introRunning = false;
        GameplayCanvas.Instance?.SetWarning(WarningType.RatsAttack, this, false);
        GameplayCanvas.Instance?.SetWarning(WarningType.CrowsAttack, this, false);
        if (player != null) player.EndDiscovery(this);
    }

    private void LateUpdate()
    {
        RefreshIndicator(ratsHud, false);
        RefreshIndicator(crowsHud, true);
        if (showBroomHint && player != null)
        {
            var tools = player.GetComponent<PlayerCombatTools>();
            if (tools != null && tools.Equipped == PlayerCombatTools.Tool.Broom) showBroomHint = false;
        }
        bool feeding = encounterPests.Any(p => p != null && p.IsCrow == crowLesson && p.IsFeeding);
        bool visible = (introRunning || showBroomHint) && feeding;
        GameplayCanvas.Instance?.SetWarning(WarningType.RatsAttack, this, visible && !crowLesson);
        GameplayCanvas.Instance?.SetWarning(WarningType.CrowsAttack, this, visible && crowLesson);
    }

    private readonly System.Collections.Generic.HashSet<GrainContainer> markedBags = new System.Collections.Generic.HashSet<GrainContainer>();
    private readonly System.Collections.Generic.List<FarmChicken> markedChickens = new System.Collections.Generic.List<FarmChicken>();
    private readonly System.Collections.Generic.List<Vector3> markerPositions = new System.Collections.Generic.List<Vector3>();
    private float nextMarkerRefresh;

    private void OnGUI()
    {
        // IMGUI also invokes Layout/input events; these dots only need Repaint.
        if (Event.current.type != EventType.Repaint) return;
        if (playerCamera == null) return;
        if (markerTexture == null) return;
        if (Time.unscaledTime >= nextMarkerRefresh)
        {
            nextMarkerRefresh = Time.unscaledTime + .2f;
            markedBags.Clear();
            markedChickens.Clear();
            foreach (var pest in FindObjectsByType<GrainPest>(FindObjectsSortMode.None))
                if (pest.IsFeeding) markedBags.Add(pest.Target);
            foreach (var chicken in FindObjectsByType<FarmChicken>(FindObjectsSortMode.None))
                if (chicken.IsGrainThreat) markedChickens.Add(chicken);
        }
        // Retain live positions so moving targets and camera turns stay smooth.
        markerPositions.Clear();
        foreach (var bag in markedBags)
        {
            if (bag == null || !bag.isActiveAndEnabled) continue;
            var bounds = bag.WorldBounds;
            markerPositions.Add(bounds.center + Vector3.up * (bounds.extents.y + 1f));
        }
        foreach (var chicken in markedChickens)
            if (chicken != null && chicken.IsGrainThreat)
                markerPositions.Add(chicken.transform.position + Vector3.up);
        foreach (var position in markerPositions)
        {
            Vector3 p = playerCamera.WorldToViewportPoint(position);
            Vector2 direction = new Vector2(p.x - .5f, p.y - .5f);
            if (p.z < 0f) direction = -direction;
            if (direction.sqrMagnitude < .0001f && p.z < 0f) direction = Vector2.down;
            float edge = Mathf.Max(Mathf.Abs(direction.x) / .46f, Mathf.Abs(direction.y) / .44f);
            if (p.z < 0f || edge > 1f) direction /= Mathf.Max(edge, .0001f);
            Vector2 screen = new Vector2((.5f + direction.x) * Screen.width, (.5f - direction.y) * Screen.height);
            float size = 20f + 7f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5f));
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .3f);
            GUI.DrawTexture(new Rect(screen.x-size*.5f, screen.y-size*.5f, size, size), markerTexture);
            GUI.color = previous;
        }
    }
}
