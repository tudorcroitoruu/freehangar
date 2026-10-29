using UnityEngine;

/// <summary>Single-species receiver for sacks, boxes and barrels. Unassigned receivers bind on first fill.</summary>
[DisallowMultipleComponent]
public sealed class GrainContainer : MonoBehaviour, IInteractable
{
    [SerializeField, Min(.01f)] private float capacityKg = 50f;
    [SerializeField, Min(0f)] private float kilograms;
    [SerializeField, Tooltip("Whether the player can carry this container, regardless of its fill level. Disable for Big Bag.")]
    private bool canBeCarried = true;
    [SerializeField, Tooltip("A designated receiver accepts only this crop. Otherwise the first pour assigns it.")] private bool restrictGrain;
    [SerializeField] private GrainType acceptedGrain;
    [SerializeField] private SackController sack;
    [SerializeField] private bool fitFillCollider;
    [SerializeField] private Bounds emptyBounds;
    [SerializeField] private Bounds fullBounds;
    [SerializeField] private Bounds closedBounds;
    [SerializeField, Min(.1f)] private float closeDuration = .65f;
    private BoxCollider fillCollider;
    public float Kilograms => kilograms;
    public float CapacityKg => capacityKg;
    public bool HasAssignedGrain => restrictGrain || kilograms > .000001f;
    public GrainType AcceptedGrain => acceptedGrain;
    public bool Accepts(GrainType grain) => !HasAssignedGrain || grain == acceptedGrain;
    public bool IsOpen => sack == null || sack.Close < .05f;
    public bool IsFull => kilograms >= capacityKg - .001f;
    public bool IsFinished => sack != null && sack.Close >= .999f;
    public bool IsClosing { get; private set; }
    public bool IsCarried { get; private set; }
    public bool CanBeCarried => canBeCarried;
    public bool CanCarry => canBeCarried && isActiveAndEnabled && !IsCarried && !IsClosing;
    public bool CanHandle => isActiveAndEnabled && sack != null && IsFull && !IsCarried;

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        return interactor != null && interactor.Bags != null && interactor.Bags.TryGetPrompt(this, out prompt);
    }

    public bool TryInteract(PlayerInteractor interactor) => interactor != null && interactor.Bags != null && interactor.Bags.TryInteract(this);

    public Bounds WorldBounds
    {
        get
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
            {
                Bounds bounds = new Bounds(transform.position, Vector3.one * .5f);
                bool found = false;
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                {
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                return bounds;
            }
            Bounds result = default;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = box.center + Vector3.Scale(box.size * .5f,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = transform.TransformPoint(corner);
                if (i == 0) result = new Bounds(point, Vector3.zero); else result.Encapsulate(point);
            }
            return result;
        }
    }
    public Vector3 PourPoint
    {
        get
        {
            var target = GetComponent<Collider>();
            return target != null ? new Vector3(target.bounds.center.x, target.bounds.max.y - .04f, target.bounds.center.z) : transform.position;
        }
    }

    private void Awake()
    {
        if (sack == null) sack = GetComponent<SackController>();
        fillCollider = GetComponent<BoxCollider>();
        kilograms = Mathf.Clamp(kilograms, 0, capacityKg);
        ApplyFill();
    }

    public float Add(float amountKg) => Add(GrainType.Corn, amountKg);

    public float Add(GrainType grain, float amountKg)
    {
        if (!isActiveAndEnabled || IsClosing || IsCarried || !IsOpen || !Accepts(grain) ||
            (int)grain < 0 || (int)grain >= GrainCargo.TypeCount || !(amountKg > 0) || !float.IsFinite(amountKg)) return 0;
        float accepted = Mathf.Min(amountKg, Mathf.Max(0, capacityKg - kilograms));
        bool wasFull = IsFull;
        if (accepted > 0) acceptedGrain = grain;
        kilograms += accepted;
        ApplyFill();
        if (accepted > 0)
        {
            GoalContext.Resolve(this, out int hangar, out _);
            GoalChecker.ReportProgress(GoalType.GrainMoved, accepted, hangar, grain);
            GoalChecker.ReportValue(GoalType.SackFilled, IsFull ? capacityKg : kilograms, hangar, grain);
            GrainDeposited?.Invoke(accepted);
            if (!wasFull && IsFull) GoalChecker.ReportProgress(GoalType.BagsFilled, 1, hangar, grain);
        }
        return accepted;
    }

    public static event System.Action<float> GrainDeposited;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents() => GrainDeposited = null;

    public bool Finish()
    {
        if (!isActiveAndEnabled || !CanHandle || IsFinished || IsClosing) return false;
        IsClosing = true;
        return true;
    }

    // Initialise newly spawned demonstration cargo without reporting player work.
    public void SetDemonstrationFill(float amount)
    {
        if (!isActiveAndEnabled || !float.IsFinite(amount)) return;
        kilograms = Mathf.Clamp(amount, 0f, capacityKg);
        ApplyFill();
    }

    public float ConsumeByPest(float amount)
    {
        if (!isActiveAndEnabled || !float.IsFinite(amount) || amount <= 0f) return 0f;
        float removed = Mathf.Min(kilograms, amount);
        kilograms -= removed;
        if (removed > 0f && sack != null)
        {
            // Pest damage opens a closed sack so the missing grain can be replaced.
            IsClosing = false;
            sack.Close = 0f;
        }
        ApplyFill();
        return removed;
    }

    public void SetCarried(bool carried) => IsCarried = carried;

    private void Update()
    {
        if (!IsClosing || sack == null || Time.deltaTime <= 0) return;
        sack.Close = Mathf.MoveTowards(sack.Close, 1f, Time.deltaTime / closeDuration);
        ApplyFill();
        if (IsFinished)
        {
            IsClosing = false;
            GoalContext.Resolve(this, out int hangar, out _);
            GoalChecker.ReportProgress(GoalType.SackClosed, 1, hangar, acceptedGrain);
        }
    }

    private void ApplyFill()
    {
        if (sack != null) sack.Fill = kilograms / capacityKg;
        if (fitFillCollider && fillCollider != null)
        {
            float fill = kilograms / capacityKg;
            Bounds bounds = new Bounds(Vector3.Lerp(emptyBounds.center, fullBounds.center, fill),
                Vector3.Lerp(emptyBounds.size, fullBounds.size, fill));
            float closure = sack != null && closedBounds.size.sqrMagnitude > .0001f ? sack.Close : 0;
            fillCollider.center = Vector3.Lerp(bounds.center, closedBounds.center, closure);
            fillCollider.size = Vector3.Lerp(bounds.size, closedBounds.size, closure);
        }
    }
}
