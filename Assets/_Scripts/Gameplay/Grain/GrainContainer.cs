using SickscoreGames.HUDNavigationSystem;
using UnityEngine;

/// <summary>Single-species receiver for sacks, boxes and barrels. Unassigned receivers bind on first fill.</summary>
[DisallowMultipleComponent]
public sealed class GrainContainer : MonoBehaviour, IInteractable
{
    [SerializeField, Min(1), Tooltip("Minimum vehicle cargo level required to transport this container.")] private int transportLevel = 1;
    [SerializeField] private string cargoTypeId;
    [SerializeField, Tooltip("Model rotation when upright. Independent of physics rotation and vehicle cargo poses.")]
    private Vector3 uprightEulerAngles = new Vector3(-90f, 0f, 0f);
    public Quaternion UprightRotation => Quaternion.Euler(uprightEulerAngles);
    // Early scene sacks predate cargo IDs. Resolve them through the same levels as shop prefabs.
    public string CargoTypeId => !string.IsNullOrWhiteSpace(cargoTypeId) ? cargoTypeId : TransportLevel switch
    {
        1 => "FreeBag", 2 => "SmallBag", 3 => "LargeBag", 4 => "Crate", 5 => "Barrel", 6 => "BigBag", _ => string.Empty
    };
    [SerializeField, Range(1,2), Tooltip("Pickup bay space: 1 for a small container, 2 for a full Big Bag bay.")] private int pickupSpaceUnits = 1;
    public int PickupSpaceUnits => Mathf.Clamp(pickupSpaceUnits,1,2);
    public int TransportLevel => Mathf.Max(1, transportLevel);
    public string DisplayName => CargoTypeId switch
    {
        "FreeBag" => "Free bag",
        "SmallBag" => "Small bag",
        "LargeBag" => "Large bag",
        "Crate" => "Crate",
        "Barrel" => "Barrel",
        "BigBag" => "Big bag",
        _ => "Container"
    };
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
    [Header("Interaction prompt")]
    [SerializeField, Tooltip("Optional custom prompt origin. Without an anchor, the top of the container bounds is used.")]
    private Transform promptAnchor;
    [SerializeField, Tooltip("World-space offset from the anchor or container top. Negative Y lowers the prompt.")]
    private Vector3 promptOffset = Vector3.up * .12f;
    private BoxCollider fillCollider;
    [SerializeField, Tooltip("Optional pallet carried and seated with this container.")] private BoxCollider palletCollider;

    [Space, Header("HUD Element")] 
    [SerializeField] private HUDNavigationElement hudElement;
    public Bounds LocalBounds
        => GetLocalBounds(false);
    // Vehicle placement must not move the pallet as the sack fills or closes.
    public Bounds VehiclePlacementBounds => GetLocalBounds(IsBigBag && fitFillCollider);
    private Bounds GetLocalBounds(bool useClosedShape)
    {
        var box = GetComponent<BoxCollider>();
        var bounds = useClosedShape ? closedBounds : box != null ? new Bounds(box.center, box.size) : new Bounds(Vector3.zero, Vector3.one * .5f);
        if (palletCollider != null)
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = palletCollider.center + Vector3.Scale(palletCollider.size * .5f,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                bounds.Encapsulate(transform.InverseTransformPoint(palletCollider.transform.TransformPoint(corner)));
            }
        return bounds;
    }
    public Vector3 PromptPosition
    {
        get
        {
            Bounds bounds = WorldBounds;
            if (IsCarried) return bounds.center;
            Vector3 origin = promptAnchor != null ? promptAnchor.position : new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            return origin + promptOffset;
        }
    }
    [System.NonSerialized] private string achievementIdentity;
    public string AchievementIdentity => achievementIdentity ?? (achievementIdentity=System.Guid.NewGuid().ToString("N"));
    public float Kilograms => kilograms;
    public float CapacityKg => capacityKg;
    public bool HasAssignedGrain => restrictGrain || kilograms > .000001f;
    public GrainType AcceptedGrain => acceptedGrain;
    public bool Accepts(GrainType grain) => !HasAssignedGrain || grain == acceptedGrain;
    public bool IsOpen => sack == null || sack.Close < .05f;
    public bool IsFull => kilograms >= capacityKg - .001f;
    public bool IsFinished => sack != null && sack.Close >= .999f;
    public bool IsEmpty => kilograms <= .001f;
    public bool CanLoadOnVehicle => !IsClosing && (IsEmpty || IsBigBag ||
        string.Equals(CargoTypeId, "Crate", System.StringComparison.OrdinalIgnoreCase) ||
        string.Equals(CargoTypeId, "Barrel", System.StringComparison.OrdinalIgnoreCase) ||
        (IsFull && (sack == null || IsFinished)));
    public bool IsClosing { get; private set; }
    public bool IsCarried { get; private set; }
    public bool IsBigBag => string.Equals(CargoTypeId,"BigBag",System.StringComparison.OrdinalIgnoreCase);
    public bool RequiresForklift => IsBigBag && kilograms > .001f;
    public bool CanBeCarried => canBeCarried && !RequiresForklift;
    public bool IsUnderAttack => GrainPest.IsAttacking(this);
    public bool CanCarry => CanBeCarried && isActiveAndEnabled && !IsCarried && !IsClosing && !IsUnderAttack;
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
            Bounds localBounds = LocalBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = localBounds.center + Vector3.Scale(localBounds.extents,
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

    private void Awake() => RefreshFillPresentation();

    private void OnEnable() => RefreshHudVisibility();

    private void OnTransformParentChanged() => RefreshHudVisibility();

    private void RefreshHudVisibility()
    {
        if (hudElement != null) hudElement.enabled = VehicleCargoOwner.Resolve(this) == null;
    }

    /// <summary>Refresh visuals and contact bounds together without depositing grain or awarding progress.</summary>
    public void RefreshFillPresentation()
    {
        if (sack == null) sack = GetComponent<SackController>();
        fillCollider = GetComponent<BoxCollider>();
        // The barrel is rigid: its contact shape follows the wooden shell, not the fill level.
        if (string.Equals(CargoTypeId, "Barrel", System.StringComparison.OrdinalIgnoreCase))
        {
            if (fillCollider == null) fillCollider = gameObject.AddComponent<BoxCollider>();
            fitFillCollider = false;
            bool found = false;
            Bounds shell = default;
            foreach (var mesh in GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.sharedMesh == null) continue;
                Bounds bounds = mesh.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = transform.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                    if (!found) { shell = new Bounds(point, Vector3.zero); found = true; }
                    else shell.Encapsulate(point);
                }
            }
            if (found) { fillCollider.center = shell.center; fillCollider.size = shell.size; }
        }
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
        => SetDemonstrationFill(acceptedGrain, amount);

    public void SetDemonstrationFill(GrainType grain, float amount)
    {
        if (!isActiveAndEnabled || !float.IsFinite(amount) || (int)grain < 0 ||
            (int)grain >= GrainCargo.TypeCount) return;
        acceptedGrain = grain;
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

    public void SetCarried(bool carried)
    {
        IsCarried = carried;
        if (carried) return;
        // Cargo handling temporarily disables contacts. Never release a container without support.
        var box = GetComponent<BoxCollider>();
        if (box != null) { box.enabled = true; box.isTrigger = false; }
        if (palletCollider != null) { palletCollider.enabled = true; palletCollider.isTrigger = false; }
        if (TryGetComponent<Rigidbody>(out var body)) body.detectCollisions = true;
    }

    public void EmptyAfterPour()
    {
        kilograms = 0f;
        IsClosing = false;
        if (sack != null) sack.Close = 0f;
        ApplyFill();
    }

    public ContainerSave CaptureSave() => new ContainerSave { kilograms = kilograms, grain = (int)acceptedGrain,
        closure = sack != null ? sack.Close : 0, closing = IsClosing, achievementId = AchievementIdentity };

    public void RestoreSave(ContainerSave saved)
    {
        if (saved == null) return;
        kilograms = float.IsFinite(saved.kilograms) ? Mathf.Clamp(saved.kilograms, 0, capacityKg) : 0;
        acceptedGrain = (GrainType)Mathf.Clamp(saved.grain, 0, GrainCargo.TypeCount - 1);
        achievementIdentity = saved.achievementId;
        RefreshFillPresentation();
        if (sack != null) sack.Close = Mathf.Clamp01(saved.closure);
        IsClosing = saved.closing && sack != null && !IsFinished;
        IsCarried = false;
        ApplyFill();
    }

    private void Update()
    {
        if (!IsClosing || sack == null || Time.deltaTime <= 0) return;
        sack.Close = Mathf.MoveTowards(sack.Close, 1f, Time.deltaTime / closeDuration);
        ApplyFill();
        if (IsFinished)
        {
            IsClosing = false;
            GameplayAudio.Instance?.CloseContainer();
            GoalContext.Resolve(this, out int hangar, out _);
            GameAchievements.BagClosed(this);
            GoalChecker.ReportProgress(GoalType.SackClosed, 1, hangar, acceptedGrain);
        }
    }

    private void ApplyFill()
    {
        if (sack != null) sack.Fill = kilograms / capacityKg;
        if (TryGetComponent<BagCornSurface>(out var surface)) surface.RefreshGrainAppearance();
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
