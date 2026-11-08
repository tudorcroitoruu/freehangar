using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-50)]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
[RequireComponent(typeof(Unity.AI.Navigation.NavMeshModifier))]
public sealed class GrainPile : MonoBehaviour
{
    [Header("Spatial grain regions (percent of starting mass)")]
    [SerializeField] private bool useSpatialLayout;
    [SerializeField] private GrainSpatialField.Arrangement arrangement;
    [SerializeField, Range(0,.5f)] private float regionWaviness = .12f;
    [Tooltip("Curved Sections junction in normalized footprint coordinates (0..1).")]
    [SerializeField] private Vector2 sectionCenter = new Vector2(.55f, .48f);
    [SerializeField, Range(-180,180)] private float sectionRotation;
    [SerializeField] private int grainLayoutSeed = 731;
    [SerializeField] private GrainRegion[] regions = { new GrainRegion(100, GrainType.Corn) };
    [SerializeField, Range(0,100), Tooltip("Edit Mode only. Clear this percentage from the front of the region sequence to inspect later crops. Play Mode always starts intact.")]
    private float previewClearedPercent;
    private GrainSpatialField spatial;
    public bool HasSpatialLayout => useSpatialLayout;
    public double GrainKilograms(GrainType grain) => spatial == null ? 0 : spatial.VolumeOf(grain) * kilogramsPerCubicMetre * VolumeScale;
    public void ConfigureRegions(GrainRegion[] value, GrainSpatialField.Arrangement pattern = GrainSpatialField.Arrangement.FrontToBack, float waviness = .12f)
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Author spatial stock outside Play Mode; changing it would replace stored grain.");
        useSpatialLayout = true; regions = value; arrangement = pattern; regionWaviness = waviness;
        previewClearedPercent = 0; RebuildPreview();
    }
    public void ApplySpatialAppearance(MaterialPropertyBlock block)
    {
        if (useSpatialLayout && spatial != null) spatial.Bind(block, transform);
        else block.SetFloat("_UseSpatialGrain", 0);
    }
    public void SampleSurfaceGrain(Vector3 worldPoint, GrainCargo output)
    {
        Initialize();
        if (spatial != null)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            spatial.Sample(local.x, local.z, output);
        }
        else { output.Clear(); CollectRecipe(Application.isPlaying ? harvestedKg : previewHarvestedKg, 1, output); }
    }
    [Header("Grain recipe â€” top to bottom")]
    [SerializeField] private GrainLayer[] grainLayers = { new GrainLayer(52, GrainType.Corn) };
    [SerializeField, Min(0), Tooltip("Editor only: preview the recipe after collecting this many kg. Does not excavate or change gameplay mass.")]
    private float previewHarvestedKg;
    private double harvestedKg;
    private readonly GrainCargo surfaceGrain = new GrainCargo();
    private readonly GrainAppearance appearance = new GrainAppearance();
    public double HarvestedKg => harvestedKg;
    private double initialKilograms;
    private PlayerData clearanceProfile;
    private float reportedClearance = -1f;
    public double InitialKilograms => initialKilograms;
    public float ClearedPercent => initialKilograms > 0
        ? Mathf.Clamp01((float)(1 - Kilograms / initialKilograms)) * 100f : 0f;
    public GrainCargo SurfaceGrain { get { RefreshGrainAppearance(); return surfaceGrain; } }
    private double lastAppearanceKg = -1;
    private MeshRenderer surfaceRenderer;

    public void SetRecipe(GrainLayer[] layers)
    {
        if (layers == null || layers.Length == 0) return;
        grainLayers = layers;
        harvestedKg = 0;
        lastAppearanceKg = -1;
        SurfaceRevision++;
        RefreshGrainAppearance();
    }

    public void CollectRecipe(double offsetKg, float amountKg, GrainCargo destination)
    {
        if (destination == null || !float.IsFinite(amountKg) || amountKg <= 0) return;
        if (grainLayers == null || grainLayers.Length == 0) { destination.Add(GrainType.Corn, amountKg); return; }
        double offset = System.Math.Max(0, offsetKg);
        float remaining = amountKg;
        for (int i = 0; i < grainLayers.Length && remaining > 0; i++)
        {
            var layer = grainLayers[i];
            float limit = layer != null && float.IsFinite(layer.kilograms) ? Mathf.Max(.01f, layer.kilograms) : 52;
            bool last = i == grainLayers.Length - 1;
            if (!last && offset >= limit) { offset -= limit; continue; }
            float part = last ? remaining : Mathf.Min(remaining, (float)(limit - offset));
            if (layer?.mixture != null) layer.mixture.AddTo(destination, part); else destination.Add(GrainType.Corn, part);
            remaining -= part;
            offset = 0;
        }
    }

    private void RefreshGrainAppearance()
    {
        double offset = Application.isPlaying ? harvestedKg : previewHarvestedKg;
        if (lastAppearanceKg == offset && spatial == null) return;
        lastAppearanceKg = offset;
        surfaceGrain.Clear();
        // Show a one-kilogram transition at layer boundaries, also in the substrate shader.
        if (spatial != null) spatial.Sample(0, 0, surfaceGrain); else CollectRecipe(offset, 1, surfaceGrain);
        appearance.Set(surfaceGrain);
        appearance.SetSurfaceDetail();
        ApplySpatialAppearance(appearance.Block);
        if (surfaceRenderer == null) surfaceRenderer = GetComponent<MeshRenderer>();
        if (surfaceRenderer != null) surfaceRenderer.SetPropertyBlock(appearance.Block);
    }
    [SerializeField, Range(9, 65)] private int resolution = 33;
    [SerializeField, Min(1f)] private float size = 6f;
    [SerializeField, Min(0f)] private float initialHeight = 1.8f;
    public enum PileShape { Cone, UnevenMounds, WallBank }
    [System.Serializable]
    public struct Mound
    {
        public Vector2 center;
        public Vector2 radius;
        [Min(0)] public float height;
        public Mound(Vector2 center, Vector2 radius, float height) { this.center = center; this.radius = radius; this.height = height; }
    }
    [SerializeField] private PileShape shape;
    [SerializeField, Range(0, .5f)] private float unevenness = .16f;
    [SerializeField] private int shapeSeed = 731;
    [Tooltip("Mound positions/radii use normalized coordinates: -1 to 1 spans the pile footprint. Heights multiply Initial Height.")]
    [SerializeField] private Mound[] mounds = {
        new Mound(new Vector2(-.4f, .18f), new Vector2(.57f, .7f), .85f),
        new Mound(new Vector2(.27f, .36f), new Vector2(.64f, .59f), 1f),
        new Mound(new Vector2(.18f, -.36f), new Vector2(.6f, .53f), .62f)
    };
    [SerializeField, Min(1f)] private float kilogramsPerCubicMetre = 720f;
    [SerializeField, Tooltip("Shared conversion between visible pile volume and collectible cargo. Dropped piles inherit it so pouring preserves volume and cargo.")]
    private GrainEconomySettings economySettings;
    public float GrainConsumeMultiplier => economySettings != null ? economySettings.ConsumeMultiplier : 1f;
    public bool IsDroppedPile { get; private set; }
    public double CollectibleKilograms => Kilograms / GrainConsumeMultiplier;
    [SerializeField, Range(5f, 60f)] private float reposeAngle = 32f;
    [SerializeField, Min(0f)] private float settlingMargin = .45f;
    [SerializeField, Range(1f, 30f)] private float simulationRate = 15f;
    [SerializeField, Range(1f, 8f), Tooltip("How quickly disturbed grain flows into the excavated area.")]
    private float settlingSpeed = 4f;
    [SerializeField, Range(0f, .5f), Tooltip("Visual settling response in seconds. Smooths scooping and pouring without delaying grain transfer.")]
    private float surfaceSmoothTime = .12f;
    [SerializeField, Range(1f, 15f)] private float colliderRate = 5f;
    [Header("Wall fitting")]
    [SerializeField] private bool fitToWalls = true;
    [SerializeField] private LayerMask wallMask = ~0;
    [SerializeField, Range(.002f, .1f)] private float wallClearance = .01f;
    [Tooltip("Grain-free rectangle in pile-local X/Z coordinates. Zero width or height disables it. Excludes surface, collision and collectible mass.")]
    [SerializeField] private Rect excludedFootprint;
    private bool HasExcludedFootprint => excludedFootprint.width > 0 && excludedFootprint.height > 0;
    private bool HasClippedSurface => DetectedWallCount > 0 || HasExcludedFootprint;
    private GrainWallBoundary boundary;
    private readonly List<Plane> detectedWalls = new List<Plane>(12);
    private readonly RaycastHit[] wallHits = new RaycastHit[128];
    private Matrix4x4 lastPlacement;
    public int DetectedWallCount => boundary == null ? 0 : boundary.Walls.Count;
    public bool IsInsideBoundary(Vector3 worldPoint, float radius = 0)
    {
        if (HasExcludedFootprint)
        {
            Vector3 p = transform.InverseTransformPoint(worldPoint);
            float xMargin = radius / WorldScale.x, zMargin = radius / WorldScale.z;
            if (p.x >= excludedFootprint.xMin - xMargin && p.x <= excludedFootprint.xMax + xMargin &&
                p.z >= excludedFootprint.yMin - zMargin && p.z <= excludedFootprint.yMax + zMargin) return false;
        }
        return boundary == null || boundary.Contains(worldPoint, wallClearance + radius);
    }
    private GrainHeightField field;
    internal GrainHeightField HeightField => field;
    private GrainHeightField visualField;
    private GrainHeightField groundField;
    internal float GroundOffset(int index) => groundField == null ? 0 : groundField.Heights[index];
    internal float GroundOffset(float x, float z) => groundField == null ? 0 : groundField.Sample(x, z);
    internal float VisualSurfaceHeight(float x, float z) => VisualHeightField.Sample(x, z) + GroundOffset(x, z);

    private void SampleDropGround()
    {
        surfaceDetails.Clear();
        groundField = null;
        if (!IsDroppedPile) return;
        // Ground is geometry, never collectible grain. Re-sample on restoration too.
        groundField = new GrainHeightField(field.Resolution, field.Size, 0);
        Physics.SyncTransforms();
        for (int i = 0; i < field.Heights.Length; i++)
        {
            Vector3 p = transform.TransformPoint(new Vector3(i % field.Resolution * field.Spacing - size * .5f,
                0, i / field.Resolution * field.Spacing - size * .5f));
            float reach = Mathf.Max(2f, size);
            int count = Physics.RaycastNonAlloc(p + Vector3.up * reach, Vector3.down, wallHits,
                reach * 2, wallMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int h = 0; h < count; h++)
            {
                var hit = wallHits[h];
                if (hit.normal.y < .65f || hit.collider.attachedRigidbody != null ||
                    hit.collider.GetComponentInParent<GrainPile>() != null ||
                    hit.collider.GetComponentInParent<GrainContainer>() != null) continue;
                float distance = Mathf.Abs(hit.point.y - p.y);
                if (distance >= nearest) continue;
                nearest = distance;
                groundField.Heights[i] = transform.InverseTransformPoint(hit.point).y;
            }
        }
    }
    internal GrainHeightField VisualHeightField => visualField ?? field;
    private bool visualSettling;
    private Mesh mesh;
    private Mesh collisionMesh;
    private MeshCollider meshCollider;
    private Vector3[] vertices;
    private Vector2[] surfaceUV;
    private List<int> activeTriangles;
    private bool settling;
    private bool colliderDirty;
    private float settlingSteps, nextCollider;
    private bool rebuildRequested;
    private Vector3 lastScale;
    public PileShape Shape => shape;
    public Vector3 WorldScale => transform.lossyScale;
    public int SurfaceRevision { get; private set; }
    public float Size => size;
    public float SampleLocalHeight(float x, float z, bool respectWalls = true)
    {
        Initialize();
        if (respectWalls && !IsInsideBoundary(transform.TransformPoint(new Vector3(x, 0, z)))) return 0;
        return field == null ? 0 : field.Sample(x, z);
    }

    private float VolumeScale => Mathf.Abs(transform.localToWorldMatrix.determinant);
    [SerializeField, HideInInspector, Tooltip("Calculated remaining mass in kg; updated from the pile volume.")]
    private double kilograms;
    public double Kilograms => kilograms = field == null ? 0 : field.Volume * kilogramsPerCubicMetre * VolumeScale;
    public float SurfaceHeight(Vector3 worldPoint)
    {
        Initialize();
        if (field == null) return worldPoint.y;
        if (!IsInsideBoundary(worldPoint)) return transform.position.y;
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        return transform.TransformPoint(new Vector3(p.x, field.Sample(p.x, p.z) + GroundOffset(p.x, p.z), p.z)).y;
    }

    /// <summary>Find a real grain surface near the blade, rather than the distant aim point.</summary>
    public bool TryNearestSurface(Vector3 worldPoint, float maximumDistance, out Vector3 surface)
        => TryNearestSurface(worldPoint, maximumDistance, out surface, null);

    public bool TryNearestSurface(Vector3 worldPoint, float maximumDistance, out Vector3 surface, GrainCargo preferred)
    {
        Initialize();
        surface = default;
        if (field == null || !isActiveAndEnabled) return false;
        float best = maximumDistance * maximumDistance;
        bool found = false;
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        float heightScale = WorldScale.y;
        var recipe = preferred != null && spatial == null ? new GrainCargo() : null;
        // Include the point directly below the blade, then the height-field vertices.
        for (int i = -1; i < field.Heights.Length; i++)
        {
            float x = i < 0 ? local.x : (i % field.Resolution) * field.Spacing - field.Size * .5f;
            float z = i < 0 ? local.z : (i / field.Resolution) * field.Spacing - field.Size * .5f;
            if (Mathf.Abs(x) > field.Size * .5f || Mathf.Abs(z) > field.Size * .5f) continue;
            float height = i < 0 ? field.Sample(x, z) : field.Heights[i];
            if (height * heightScale < .000001f) continue;
            if (preferred != null && spatial != null &&
                (i < 0 || spatial.MatchingSurfaceVolume(i, double.MaxValue, preferred) <= 0)) continue;
            Vector3 candidate = transform.TransformPoint(new Vector3(x, height + GroundOffset(x, z), z));
            if (recipe != null)
            {
                SampleSurfaceGrain(candidate, recipe);
                if (recipe[preferred.Dominant] <= 0) continue;
            }
            float distance = (candidate - worldPoint).sqrMagnitude;
            if (distance >= best || !IsInsideBoundary(candidate, .025f)) continue;
            surface = candidate; best = distance; found = true;
        }
        return found;
    }

    private void Awake() => Initialize();

    public PileSave CaptureSave()
    {
        Initialize();
        return new PileSave { size = field.Size, resolution = field.Resolution, initialKilograms = initialKilograms,
            heights = (float[])field.Heights.Clone(), columns = spatial?.CaptureSave() };
    }

    public void RestoreSave(PileSave saved)
    {
        if (saved == null || saved.heights == null) return;
        if (IsDroppedPile && (size != saved.size || field == null || field.Resolution != saved.resolution))
        {
            ReleaseMeshes(); field = null; spatial?.ReleaseTextures(); spatial = null;
            size = saved.size; resolution = saved.resolution;
        }
        Initialize();
        if (field.Heights.Length != saved.heights.Length) throw new System.InvalidOperationException("Saved grain grid does not match " + name);
        for (int i = 0; i < saved.heights.Length; i++) field.Heights[i] = float.IsFinite(saved.heights[i]) ? Mathf.Max(0, saved.heights[i]) : 0;
        spatial?.RestoreSave(saved.columns);
        SampleDropGround();
        initialKilograms = saved.initialKilograms;
        System.Array.Copy(field.Heights, visualField.Heights, field.Heights.Length);
        visualSettling = false;
        settling = true;
        RefreshSurface(); RefreshCollider(); RefreshGrainAppearance();
    }
    private void OnEnable()
    {
        // Deformable grain must never become permanent navigation geometry.
        var modifier = GetComponent<Unity.AI.Navigation.NavMeshModifier>();
        if (modifier == null) modifier = gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
        modifier.ignoreFromBuild = true;
        modifier.applyToChildren = true;
        Initialize();
    }
    private void OnDisable() => spatial?.ReleaseTextures();

    private float Profile(float x, float z)
    {
        float u = x / (size * .5f), v = z / (size * .5f);
        float h = 0;
        if (mounds != null) foreach (var mound in mounds)
        {
            float dx = (u - mound.center.x) / Mathf.Max(.05f, mound.radius.x);
            float dz = (v - mound.center.y) / Mathf.Max(.05f, mound.radius.y);
            float dome = Mathf.Max(0, 1f - dx * dx - dz * dz);
            h = Mathf.Max(h, mound.height * dome * dome);
        }
        if (shape == PileShape.WallBank)
        {
            float toe = -.62f + .18f * Mathf.Sin(u * 5f + shapeSeed * .13f);
            float bank = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(toe, .9f, v));
            h = Mathf.Max(h * .72f, bank * (.76f + .18f * Mathf.Sin(u * 4f + .6f)));
        }
        float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .16f, 1f - Mathf.Abs(u))) *
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .16f, shape == PileShape.WallBank ? v + 1f : 1f - Mathf.Abs(v)));
        float noise = Mathf.PerlinNoise(u * 2.7f + shapeSeed * .137f, v * 2.7f + shapeSeed * .219f);
        return initialHeight * h * edge * (1f + (noise * 2f - 1f) * unevenness);
    }

    public void Initialize()
    {
        if (field != null) return;
        Vector3 scale = WorldScale;
        // A height field can bank against a wall, but cannot hang vertically or represent overhangs.
        if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0 || Vector3.Dot(transform.up, Vector3.up) < .9999f)
        {
            return;
        }
        int grid = Mathf.Clamp(Mathf.Max(resolution, Mathf.CeilToInt(size * Mathf.Max(scale.x, scale.z) / .2f) + 1), 9, 129);
        field = new GrainHeightField(grid, size, initialHeight, shape == PileShape.Cone ? null : Profile);
        SampleDropGround();
        DetectWalls();
        boundary = new GrainWallBoundary();
        if (detectedWalls.Count > 0 || HasExcludedFootprint)
            boundary.Build(field, transform, detectedWalls, wallClearance, HasExcludedFootprint ? excludedFootprint : (Rect?)null);
        if (useSpatialLayout)
        {
            spatial = new GrainSpatialField(field, regions, arrangement, regionWaviness, grainLayoutSeed, sectionCenter, sectionRotation);
            if (!Application.isPlaying && previewClearedPercent > 0) spatial.PreviewClear(previewClearedPercent);
        }
        lastPlacement = transform.localToWorldMatrix;
        visualField = new GrainHeightField(field.Resolution, field.Size, 0);
        System.Array.Copy(field.Heights, visualField.Heights, field.Heights.Length);
        visualSettling = false;
        initialKilograms = Kilograms;
        lastScale = scale;
        activeTriangles = new List<int>(field.Triangles.Length);
        vertices = new Vector3[field.Heights.Length];
        var uv = new Vector2[vertices.Length];
        for (int z = 0; z < field.Resolution; z++)
        for (int x = 0; x < field.Resolution; x++)
        {
            int i = z * field.Resolution + x;
            vertices[i] = new Vector3(x * field.Spacing - field.Size * .5f, field.Heights[i], z * field.Spacing - field.Size * .5f);
            uv[i] = new Vector2(vertices[i].x, vertices[i].z);
        }
        mesh = new Mesh { name = "Grain surface (generated)", hideFlags = HideFlags.DontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        surfaceUV = uv;
        mesh.uv = uv;
        mesh.triangles = field.Triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        collisionMesh = Instantiate(mesh);
        collisionMesh.hideFlags = HideFlags.DontSave;
        collisionMesh.name = "Grain collision (runtime)";
        meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = collisionMesh;
        RefreshSurface();
        RefreshCollider();
        settling = false;
        rebuildRequested = false;
    }

    public float Scoop(Vector3 worldPoint, float radius, float depth, float capacityKg) => Scoop(worldPoint, radius, depth, capacityKg, null);

    /// <summary>Reject a batch introducing another crop before removing any mass.</summary>
    public float ScoopCompatibleWith(Vector3 worldPoint, float radius, float depth, float capacityKg,
        GrainCargo destination, GrainCargo existingCargo, out bool mixBlocked)
    {
        mixBlocked = false;
        if (!isActiveAndEnabled || !float.IsFinite(capacityKg) || capacityKg <= 0 || !IsInsideBoundary(worldPoint)) return 0;
        Initialize();
        if (field == null) return 0;
        float collectionRadius = Mathf.Max(radius, Mathf.Clamp(radius * 3f, 1.25f, 2.5f));
        var allowed = new GrainCargo();
        if (existingCargo != null && existingCargo.Total > 0)
            allowed.Add(existingCargo.Dominant, 1);
        else
        {
            if (spatial != null)
            {
                Vector3 local = transform.InverseTransformPoint(worldPoint);
                // Choose the majority crop at the actual hit, including soft region
                // transitions. Search nearby only when the contact has been cleared.
                spatial.Sample(local.x, local.z, allowed);
                if (allowed.Total <= .0001f)
                    spatial.SampleNearestAvailable(local.x, local.z, collectionRadius, WorldScale, allowed);
            }
            else SampleSurfaceGrain(worldPoint, allowed);
            if (allowed.Total > 0)
            {
                GrainType selected = allowed.Dominant;
                allowed.Clear();
                allowed.Add(selected, 1);
            }
        }
        if (spatial != null)
        {
            if (allowed.Total > 0)
            {
                Vector3 local = transform.InverseTransformPoint(worldPoint);
                double collectionDensity = kilogramsPerCubicMetre * VolumeScale / GrainConsumeMultiplier;
                collectionRadius = spatial.CollectionRadius(local.x, local.z, collectionRadius,
                    Mathf.Max(collectionRadius, 4.5f), WorldScale, allowed.Dominant, capacityKg / collectionDensity);
                if (spatial.BringCropToSurface(local.x, local.z, collectionRadius, WorldScale, allowed.Dominant))
                {
                    if (Application.isPlaying) surfaceRefreshPending = true;
                    else RefreshGrainAppearance();
                }
            }
            Vector3 contact = transform.InverseTransformPoint(worldPoint);
            float pickupDensity = kilogramsPerCubicMetre * VolumeScale / GrainConsumeMultiplier;
            double removed = spatial.CollectMatching(contact.x, contact.z, collectionRadius,
                WorldScale, allowed, capacityKg / (double)pickupDensity, destination, pickupDensity);
            float collected = (float)(removed * pickupDensity);
            if (removed > 0)
            {
                harvestedKg += collected;
                field.WakeNeighborhood(contact.x, contact.z, collectionRadius + settlingMargin, WorldScale);
                settling = true;
                if (Application.isPlaying) surfaceRefreshPending = true;
                else { RefreshGrainAppearance(); RefreshSurface(); RefreshCollider(); }
            }
            mixBlocked = removed <= 0 && existingCargo != null && existingCargo.Total > .0001f;
            return collected;
        }
        var previousCheck = field.CanRemove;
        bool blocked = false;
        double offset = harvestedKg;
        var recipe = spatial == null ? new GrainCargo() : null;
        float density = kilogramsPerCubicMetre * VolumeScale / GrainConsumeMultiplier;
        field.CanRemove = (cell, volume) =>
        {
            if (previousCheck != null && !previousCheck(cell, volume)) return false;
            bool compatible = true;
            if (spatial != null) compatible = spatial.CanCollectInto(cell, volume, existingCargo);
            else
            {
                recipe.Clear();
                float kg = (float)(volume * density);
                CollectRecipe(offset, kg, recipe);
                offset += kg;
                for (int t = 0; t < GrainCargo.TypeCount; t++)
                    if (recipe[(GrainType)t] > 0f && allowed[(GrainType)t] <= .0001f)
                        compatible = false;
            }
            if (!compatible) blocked = true;
            return compatible;
        };
        try { return Scoop(worldPoint, radius, depth, capacityKg, destination); }
        finally { field.CanRemove = previousCheck; mixBlocked = blocked; }
    }

    public float Scoop(Vector3 worldPoint, float radius, float depth, float capacityKg, GrainCargo destination)
    {
        if (!isActiveAndEnabled || !float.IsFinite(capacityKg) || !(capacityKg > 0f)) return 0;
        Initialize();
        if (field == null || !IsInsideBoundary(worldPoint)) return 0;
        Vector3 p = transform.InverseTransformPoint(worldPoint);
        // Reject a stale hit well above/below the actual surface.
        Vector3 scale = WorldScale;
        if (Mathf.Abs(p.y - field.Sample(p.x, p.z) - GroundOffset(p.x, p.z)) * scale.y > depth + .15f) return 0;
        float multiplier = GrainConsumeMultiplier;
        float density = kilogramsPerCubicMetre * VolumeScale / multiplier;
        // Keep the tool's contact footprint local. Scaling the radius by the economy
        // multiplier made a single shovel reach across an entire hangar pile.
        float excavationScale = Mathf.Pow(multiplier, 1f / 3f);
        if (spatial != null) spatial.BeginCollection(destination, density);
        float removed;
        try { removed = field.Scoop(p.x, p.z, radius, depth * excavationScale / scale.y, capacityKg / density, settlingMargin, scale); }
        finally { if (spatial != null) spatial.EndCollection(); }
        if (removed > 0)
        {
            float kg = removed * density;
            if (destination != null && spatial == null) CollectRecipe(harvestedKg, kg, destination);
            harvestedKg += kg;
            settling = true;
            if (Application.isPlaying) surfaceRefreshPending = true;
            else { RefreshGrainAppearance(); RefreshSurface(); RefreshCollider(); }
        }
        return removed * density;
    }

    /// <summary>Adds typed material locally without replacing the existing stock.</summary>
    public bool Deposit(Vector3 worldPoint, GrainCargo cargo, float spreadRadius = 0)
    {
        Initialize();
        if (field == null || cargo == null || cargo.Total <= 0 || !IsInsideBoundary(worldPoint)) return false;
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        if (Mathf.Abs(local.x) > size * .5f || Mathf.Abs(local.z) > size * .5f) return false;
        var weights = new float[field.Heights.Length];
        double sum = 0;
        // Match the volume expansion used by Scoop: mass scales cubically, radius linearly.
        float depositScale = Mathf.Pow(GrainConsumeMultiplier, 1f / 3f);
        float radius = Mathf.Max(spreadRadius * depositScale, Mathf.Max(.25f * depositScale,
            field.Spacing * Mathf.Max(WorldScale.x, WorldScale.z) * 3f));
        for (int i = 0; i < weights.Length; i++)
        {
            Vector3 point = new Vector3(i % field.Resolution * field.Spacing - size * .5f, 0,
                i / field.Resolution * field.Spacing - size * .5f);
            if (field.AreaAt(i) <= 0 || !IsInsideBoundary(transform.TransformPoint(point))) continue;
            float distance = Vector3.Scale(point - new Vector3(local.x, 0, local.z), WorldScale).magnitude;
            float falloff = Mathf.Clamp01(1 - distance * distance / (radius * radius));
            weights[i] = falloff * falloff;
            sum += weights[i] * field.AreaAt(i);
        }
        if (sum <= 0) return false;
        if (spatial == null)
        {
            // Convert the remaining legacy recipe into local strata before covering it.
            var layers = new List<GrainStratum>();
            double skip = harvestedKg, remaining = CollectibleKilograms;
            foreach (var layer in grainLayers)
            {
                double amount = layer == grainLayers[grainLayers.Length - 1] ? remaining : System.Math.Min(remaining, System.Math.Max(0, layer.kilograms - skip));
                skip = System.Math.Max(0, skip - layer.kilograms);
                if (amount <= 0) continue;
                layers.Add(new GrainStratum { percent = (float)amount, mixture = layer.mixture });
                remaining -= amount;
            }
            spatial = new GrainSpatialField(field, new[] { new GrainRegion { percent = 100, layers = layers.ToArray() } }, arrangement, 0, grainLayoutSeed);
            useSpatialLayout = true;
        }
        var mixture = new float[GrainCargo.TypeCount];
        for (int t = 0; t < mixture.Length; t++) mixture[t] = cargo.Weight(t);
        // Returning cargo restores the same collectible mass, including on an authored pile.
        double volume = cargo.Total * (double)GrainConsumeMultiplier / (kilogramsPerCubicMetre * VolumeScale);
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0) continue;
            float height = (float)(volume * weights[i] / sum);
            field.Heights[i] += height;
            spatial.Deposit(i, height * (double)field.AreaAt(i), mixture);
        }
        cargo.Clear();
        field.WakeNeighborhood(local.x, local.z, radius + settlingMargin, WorldScale);
        settling = true;
        // Coalesce streamed portions with settling, just as excavation does. The
        // kernel renderer observes SurfaceRevision and budgets changed-cell updates;
        // it already rebuilds its samples when the footprint expands onto bare ground.
        // Forcing Rebuild here resampled the entire hangar for every poured portion.
        if (Application.isPlaying) surfaceRefreshPending = true;
        else { RefreshGrainAppearance(); RefreshSurface(); RefreshCollider(); }
        return true;
    }

    public static float DropRadius(float kilograms) => Mathf.Max(.3f,
        Mathf.Pow(3f * kilograms / (720f * Mathf.PI * Mathf.Tan(32f * Mathf.Deg2Rad)), 1f / 3f));

    public static GrainPile CreateDroppedPile(Vector3 position, GrainPile appearanceSource, float expectedKg = 0)
    {
        var go = new GameObject("Dropped grain pile");
        go.SetActive(false);
        go.transform.position = position;
        var pile = go.AddComponent<GrainPile>();
        pile.IsDroppedPile = true;
        if (appearanceSource != null)
        {
            pile.economySettings = appearanceSource.economySettings;
            pile.kilogramsPerCubicMetre = appearanceSource.kilogramsPerCubicMetre;
            pile.surfaceSmoothTime = appearanceSource.surfaceSmoothTime;
            pile.settlingSpeed = appearanceSource.settlingSpeed;
        }
        float expectedVolume = Mathf.Max(0, expectedKg) * pile.GrainConsumeMultiplier / pile.kilogramsPerCubicMetre;
        float footprintRadius = Mathf.Pow(3f * expectedVolume / (Mathf.PI * Mathf.Tan(pile.reposeAngle * Mathf.Deg2Rad)), 1f / 3f);
        float visualScale = Mathf.Pow(pile.GrainConsumeMultiplier, 1f / 3f);
        // Small spilled heaps need a fraction of the grid used by an entire hangar.
        // Leave bare ground around the deposit brush as well as the settled cone.
        pile.initialHeight = 0; pile.size = Mathf.Max(1.5f, Mathf.Max(footprintRadius, DropRadius(expectedKg) * visualScale) * 2f + 1f); pile.resolution = 33;
        pile.fitToWalls = true; pile.useSpatialLayout = true;
        if (appearanceSource != null)
        {
            go.GetComponent<MeshRenderer>().sharedMaterials = appearanceSource.GetComponent<MeshRenderer>().sharedMaterials;
            var sourceKernels = appearanceSource.GetComponent<CornSurfaceKernels>();
            if (sourceKernels != null) go.AddComponent<CornSurfaceKernels>().CopyAppearanceFrom(sourceKernels);
        }
        go.SetActive(true);
        return pile;
    }

    private static readonly Unity.Profiling.ProfilerMarker SurfaceRefreshMarker = new Unity.Profiling.ProfilerMarker("GrainPile.RefreshSurface");
    private static readonly Unity.Profiling.ProfilerMarker ColliderRefreshMarker = new Unity.Profiling.ProfilerMarker("GrainPile.RefreshCollider");
    private static readonly Unity.Profiling.ProfilerMarker SmoothSurfaceMarker = new Unity.Profiling.ProfilerMarker("GrainPile.SmoothSurface");
    private bool surfaceRefreshPending;
    private float surfaceRefreshElapsed;
    // GPU interpolation advances every rendered frame between these snapshots.
    // Rebuilding the full skin at 30 Hz consumed several ms even after a scoop ended.
    private const float SurfaceRefreshInterval = 1f / 10f;
    private static readonly int SurfaceBlendId = Shader.PropertyToID("_SurfaceBlend");
    private float surfaceBlend = 1f, surfaceBlendStarted;
    private readonly List<Vector3> blendFromPositions = new List<Vector3>();
    private readonly List<Vector3> blendFromNormals = new List<Vector3>();
    private readonly List<Vector3> blendToPositions = new List<Vector3>();
    private readonly List<Vector3> blendToNormals = new List<Vector3>();

    private void UpdateSurfaceBlend()
    {
        if (surfaceBlend >= 1f) return;
        surfaceBlend = Mathf.Clamp01((Time.time - surfaceBlendStarted) / SurfaceRefreshInterval);
        if (surfaceBlend >= 1f)
        {
            previousSurfaceBounds = targetSurfaceBounds;
            mesh.bounds = targetSurfaceBounds;
        }
        appearance.Block.SetFloat(SurfaceBlendId, surfaceBlend);
        if (surfaceRenderer != null) surfaceRenderer.SetPropertyBlock(appearance.Block);
    }

    private void PrepareSurfaceBlend()
    {
        // Match the displayed position, not the previous target, if a new update
        // arrives before the last transition finishes. Reuse all backing buffers.
        bool interpolate = Application.isPlaying && blendToPositions.Count > 0;
        int oldCount = blendToPositions.Count;
        Bounds bounds = mesh.bounds;
        if (interpolate) bounds.Encapsulate(previousSurfaceBounds);
        for (int i = 0; i < closedSurfaceVertices.Count; i++)
        {
            Vector3 target = closedSurfaceVertices[i], normal = naturalSurfaceNormals[i];
            Vector3 from = target, fromNormal = normal;
            if (interpolate && i < oldCount &&
                Mathf.Abs(blendToPositions[i].x - target.x) < .00001f &&
                Mathf.Abs(blendToPositions[i].z - target.z) < .00001f)
            {
                if (surfaceBlend >= 1f)
                { from = blendToPositions[i]; fromNormal = blendToNormals[i]; }
                else
                {
                    from = Vector3.Lerp(blendFromPositions[i], blendToPositions[i], surfaceBlend);
                    fromNormal = Vector3.Lerp(blendFromNormals[i], blendToNormals[i], surfaceBlend).normalized;
                }
            }
            if (i < blendFromPositions.Count)
            { blendFromPositions[i] = from; blendFromNormals[i] = fromNormal; }
            else { blendFromPositions.Add(from); blendFromNormals.Add(fromNormal); }
        }
        int excess = blendFromPositions.Count - closedSurfaceVertices.Count;
        if (excess > 0)
        {
            blendFromPositions.RemoveRange(closedSurfaceVertices.Count, excess);
            blendFromNormals.RemoveRange(closedSurfaceVertices.Count, excess);
        }
        blendToPositions.Clear(); blendToPositions.AddRange(closedSurfaceVertices);
        blendToNormals.Clear(); blendToNormals.AddRange(naturalSurfaceNormals);
        mesh.SetUVs(3, blendFromPositions);
        mesh.SetUVs(4, blendFromNormals);
        targetSurfaceBounds = mesh.bounds;
        previousSurfaceBounds = bounds;
        mesh.bounds = bounds;
        surfaceBlend = interpolate ? 0f : 1f;
        surfaceBlendStarted = Time.time;
        appearance.Block.SetFloat(SurfaceBlendId, surfaceBlend);
        if (surfaceRenderer == null) surfaceRenderer = GetComponent<MeshRenderer>();
        if (surfaceRenderer != null) surfaceRenderer.SetPropertyBlock(appearance.Block);
    }

    private bool ClearCompletedRemainder()
    {
        if (IsDroppedPile || field == null || initialKilograms <= 0) return false;
        double remaining = Kilograms;
        if (remaining <= 0 || remaining > initialKilograms * .02) return false;
        // Completion cleanup is not harvested grain: don't grant cargo, sales or collection rewards.
        spatial?.RestoreSave(new List<GrainColumnSave>());
        System.Array.Clear(field.Heights, 0, field.Heights.Length);
        if (visualField != null) System.Array.Clear(visualField.Heights, 0, visualField.Heights.Length);
        settling = visualSettling = surfaceRefreshPending = false;
        RefreshSurface();
        RefreshCollider();
        RefreshGrainAppearance();
        return true;
    }

    private void LateUpdate()
    {
        if (field == null || !Application.isPlaying) return;
        UpdateSurfaceBlend();
        surfaceRefreshElapsed += Time.deltaTime;
        // Batch stock texture uploads and full mesh work independently of display FPS.
        if ((surfaceRefreshPending || visualSettling) && surfaceRefreshElapsed < SurfaceRefreshInterval)
        { TryRefreshSettledCollider(Time.time); return; }
        if (surfaceRefreshPending)
        {
            spatial?.ReconcileHeights();
            spatial?.ConsolidateThinRemnants(WorldScale);
            surfaceRefreshPending = false;
            visualSettling = true;
            colliderDirty = true;
            RefreshGrainAppearance();
        }
        if (visualSettling && Time.deltaTime > 0)
        {
            visualSettling = SmoothVisualSurface(surfaceRefreshElapsed);
            RefreshSurface();
        }
        surfaceRefreshElapsed = 0;
        TryRefreshSettledCollider(Time.time);
    }

    private bool SmoothVisualSurface(float deltaTime)
    {
        using var measurement = SmoothSurfaceMarker.Auto();
        float blend = surfaceSmoothTime <= 0 ? 1f : 1f - Mathf.Exp(-deltaTime / surfaceSmoothTime);
        float tolerance = .0001f / Mathf.Max(.0001f, WorldScale.y);
        bool pending = false;
        for (int i = 0; i < field.Heights.Length; i++)
        {
            float height = Mathf.Lerp(visualField.Heights[i], field.Heights[i], blend);
            if (Mathf.Abs(height - field.Heights[i]) <= tolerance) height = field.Heights[i];
            else pending = true;
            visualField.Heights[i] = height;
        }
        return pending;
    }

    private void Update()
    {
        if (!Application.isPlaying) RefreshGrainAppearance();
        if (!Application.isPlaying)
        {
            DetectWalls();
            if (rebuildRequested || lastScale != WorldScale || lastPlacement != transform.localToWorldMatrix || boundary == null || !boundary.Matches(detectedWalls)) RebuildPreview();
            return;
        }
        if (field == null || Time.deltaTime <= 0) return;
        if (!IsDroppedPile && !WorldSaveRuntime.IsRestoring && SaveSystem.savesLoaded && SaveSystem.Data != null)
        {
            ClearCompletedRemainder();
            float cleared = ClearedPercent;
            if (clearanceProfile != SaveSystem.Data || cleared > reportedClearance)
            {
                clearanceProfile = SaveSystem.Data;
                reportedClearance = cleared;
                GoalContext.Resolve(this, out int hangar, out _);
                SaveSystem.TryRecordHangarClearance(hangar, cleared);
            }
        }
        AdvanceSettling(Time.deltaTime);
    }

    private void TryRefreshSettledCollider(float now)
    {
        // Follow the displayed skin even while simulation is still moving. The
        // bake rate and movement threshold coalesce insignificant animation steps.
        if (colliderDirty && !surfaceRefreshPending && now >= nextCollider)
            RefreshCollider();
    }

    private void AdvanceSettling(float deltaTime)
    {
        if (settling)
        {
            // Accumulate time instead of scheduling from the current frame: the
            // old timer lost fractional steps and visibly slowed at lower FPS.
            settlingSteps = Mathf.Min(8f, settlingSteps + deltaTime * simulationRate * settlingSpeed);
            while (settling && settlingSteps >= 1f)
            {
                settlingSteps -= 1f;
                settling = field.Settle(reposeAngle, .45f, WorldScale);
                if (settling) surfaceRefreshPending = true;
            }
        }
        else settlingSteps = 0;
    }

    private void RefreshSurface()
    {
        using var measurement = SurfaceRefreshMarker.Auto();
        kilograms = Kilograms;
        SurfaceRevision++;
        colliderDirty = true;
        var surface = VisualHeightField;
        if (!Application.isPlaying) System.Array.Copy(field.Heights, surface.Heights, field.Heights.Length);
        if (TryRefreshLocalSurface(surface)) return;
        if (HasClippedSurface)
        {
            boundary.UpdateHeights(surface, .045f / WorldScale.y);
            OffsetBoundaryGround();
            CloseClippedEdges();
            FinishNaturalSurface();
            if (!Application.isPlaying) colliderDirty = true;
            return;
        }
        // The volume skin sits beneath the visible kernels. Bury its zero-height apron
        // below the ground instead of exposing large orange triangles around the pile.
        float surfaceRecess = .045f / WorldScale.y;
        for (int i = 0; i < vertices.Length; i++) vertices[i].y = GrainHeightField.RecessedSurfaceHeight(surface.Heights[i], surfaceRecess) + GroundOffset(i);
        // Remove the previous closure before assigning the top grid again.
        if (surfaceUV == null || surfaceUV.Length != vertices.Length)
        {
            surfaceUV = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) surfaceUV[i] = new Vector2(vertices[i].x, vertices[i].z);
        }
        activeTriangles.Clear();
        for (int t = 0; t < field.Triangles.Length; t += 3)
        {
            int a = field.Triangles[t], b = field.Triangles[t + 1], c = field.Triangles[t + 2];
            if (surface.Heights[a] <= .00001f && surface.Heights[b] <= .00001f && surface.Heights[c] <= .00001f) continue;
            activeTriangles.Add(a); activeTriangles.Add(b); activeTriangles.Add(c);
        }
        // Bare ground must not retain a golden rectangle or a phantom grain collider.
        // Authored stock can also rise at its footprint edge after a deposit.
        CloseDroppedEdges(surface);
        FinishNaturalSurface();
        if (!Application.isPlaying) colliderDirty = true;
    }

    private void OnValidate() { rebuildRequested = true; lastAppearanceKg = -1; SurfaceRevision++; }

    public void RebuildPreview()
    {
        if (Application.isPlaying) return; // Never reset excavated material during gameplay.
        ReleaseMeshes(); field = null;
        Initialize();
    }

    private readonly List<Vector3> closedSurfaceVertices = new List<Vector3>();
    private readonly List<Vector2> closedSurfaceUV = new List<Vector2>();
    private readonly List<int> closedSurfaceTriangles = new List<int>();
    private int sideTriangleStart;
    internal int SideFaceCount => (closedSurfaceTriangles.Count - sideTriangleStart) / 6;
    internal void SideFace(int index, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d)
    {
        int t = sideTriangleStart + index * 6;
        a = transform.TransformPoint(closedSurfaceVertices[closedSurfaceTriangles[t]]);
        b = transform.TransformPoint(closedSurfaceVertices[closedSurfaceTriangles[t + 2]]);
        c = transform.TransformPoint(closedSurfaceVertices[closedSurfaceTriangles[t + 1]]);
        d = transform.TransformPoint(closedSurfaceVertices[closedSurfaceTriangles[t + 5]]);
    }
    private GrainWallBoundary closedBoundary;
    private readonly Dictionary<Vector3Int, int> surfaceNormalGroups = new Dictionary<Vector3Int, int>();
    private readonly List<int> surfaceNormalIndices = new List<int>();
    private readonly List<Vector3> surfaceNormalSums = new List<Vector3>();
    private readonly List<Vector3> naturalSurfaceNormals = new List<Vector3>();
    private readonly List<Vector3Int> cachedNormalKeys = new List<Vector3Int>();
    private int topSurfaceVertexCount;
    private struct HeightSample
    {
        public int a, b, c;
        public Vector3 weights;
        public float Read(float[] heights) => heights[a] * weights.x + heights[b] * weights.y + heights[c] * weights.z;
        public static HeightSample At(GrainHeightField field, float x, float z)
        {
            float gx = (x + field.Size * .5f) / field.Spacing, gz = (z + field.Size * .5f) / field.Spacing;
            if (gx < 0 || gz < 0 || gx > field.Resolution - 1 || gz > field.Resolution - 1) return default;
            int ix = Mathf.Min((int)gx, field.Resolution - 2), iz = Mathf.Min((int)gz, field.Resolution - 2);
            int i = iz * field.Resolution + ix;
            float u = gx - ix, v = gz - iz;
            return u + v <= 1f
                ? new HeightSample { a = i, b = i + 1, c = i + field.Resolution, weights = new Vector3(1-u-v, u, v) }
                : new HeightSample { a = i + field.Resolution + 1, b = i + field.Resolution, c = i + 1, weights = new Vector3(u+v-1, 1-u, 1-v) };
        }
    }
    private struct SurfaceDetail
    {
        public Vector2 position;
        public float ground, noise;
        public HeightSample center, left, right, back, front;
    }
    private readonly List<SurfaceDetail> surfaceDetails = new List<SurfaceDetail>();
    private Vector3 surfaceDetailScale;
    private Bounds previousSurfaceBounds, targetSurfaceBounds;
    private float[] renderedHeights;
    private bool[] changedHeights, changedVertices, changedGroups;
    private int[] renderedTriangles, groupFaceOffsets, groupFaces;
    private int renderedTriangleCount;
    private int[] normalFaceCursor;
    private Vector3[] faceNormals;
    private int[] detailOffsets, detailVertices, renderHeightSources;
    private bool[] dirtyFaces;
    private readonly List<int> dirtySurfaceVertices = new List<int>();
    private readonly List<int> dirtySurfaceFaces = new List<int>();
    private readonly List<int> dirtySurfaceGroups = new List<int>();

    private static void ReserveSurfaceBuffer<T>(ref T[] buffer, int count)
    {
        if (buffer == null || buffer.Length < count) buffer = new T[Mathf.NextPowerOfTwo(Mathf.Max(1, count))];
    }

    private void BuildSurfaceDependencies()
    {
        if (HasClippedSurface) { detailOffsets = null; return; }
        int count = VisualHeightField.Heights.Length;
        detailOffsets = new int[count + 1];
        renderHeightSources = new int[closedSurfaceVertices.Count];
        void Visit(int vertex, System.Action<int, int> add)
        {
            var d = surfaceDetails[vertex];
            void Sample(HeightSample s) { add(s.a, vertex); add(s.b, vertex); add(s.c, vertex); }
            Sample(d.center); Sample(d.left); Sample(d.right); Sample(d.back); Sample(d.front);
            if (renderHeightSources[vertex] >= 0) add(renderHeightSources[vertex], vertex);
        }
        System.Action<int, int> countDependency = (sample, vertex) => detailOffsets[sample + 1]++;
        for (int i = 0; i < renderHeightSources.Length; i++)
        {
            var p = closedSurfaceVertices[i];
            bool top = i < topSurfaceVertexCount || (i - topSurfaceVertexCount) % 4 < 2;
            int x = Mathf.RoundToInt((p.x + field.Size * .5f) / field.Spacing);
            int z = Mathf.RoundToInt((p.z + field.Size * .5f) / field.Spacing);
            renderHeightSources[i] = top ? z * field.Resolution + x : -1;
            Visit(i, countDependency);
        }
        for (int i = 1; i < detailOffsets.Length; i++) detailOffsets[i] += detailOffsets[i - 1];
        detailVertices = new int[detailOffsets[count]];
        var cursor = (int[])detailOffsets.Clone();
        System.Action<int, int> storeDependency = (sample, vertex) => detailVertices[cursor[sample]++] = vertex;
        for (int i = 0; i < renderHeightSources.Length; i++)
            Visit(i, storeDependency);
        dirtyFaces = new bool[faceNormals.Length];
    }

    private bool TryRefreshLocalSurface(GrainHeightField surface)
    {
        if (HasClippedSurface || detailOffsets == null || surfaceDetailScale != WorldScale ||
            renderedHeights == null || renderedHeights.Length != surface.Heights.Length ||
            surfaceDetails.Count != blendToPositions.Count || cachedNormalKeys.Count != blendToPositions.Count) return false;
        float inset = .045f / surfaceDetailScale.y;
        // Near the ground, triangles and skirt edges can appear/disappear. Use the
        // full topology path there. Interior deformation retains the exact indices.
        for (int i = 0; i < renderedHeights.Length; i++)
            if (renderedHeights[i] != surface.Heights[i] && Mathf.Min(renderedHeights[i], surface.Heights[i]) <= inset * 2f)
                return false;
        System.Array.Clear(changedVertices, 0, changedVertices.Length);
        System.Array.Clear(changedGroups, 0, changedGroups.Length);
        dirtySurfaceVertices.Clear(); dirtySurfaceFaces.Clear(); dirtySurfaceGroups.Clear();
        for (int i = 0; i < renderedHeights.Length; i++)
        {
            if (renderedHeights[i] == surface.Heights[i]) continue;
            renderedHeights[i] = surface.Heights[i];
            for (int j = detailOffsets[i]; j < detailOffsets[i + 1]; j++)
            {
                int vertex = detailVertices[j];
                if (changedVertices[vertex]) continue;
                changedVertices[vertex] = true; dirtySurfaceVertices.Add(vertex);
            }
        }
        if (dirtySurfaceVertices.Count == 0) return true;
        float scaleY = Mathf.Max(.001f, surfaceDetailScale.y);
        foreach (int vertex in dirtySurfaceVertices)
        {
            var d = surfaceDetails[vertex];
            var p = blendToPositions[vertex];
            int source = renderHeightSources[vertex];
            p.y = source >= 0 ? GrainHeightField.RecessedSurfaceHeight(surface.Heights[source], inset) + d.ground : d.ground - inset;
            float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((p.y - d.ground) * scaleY / .18f));
            if (fade > 0)
            {
                float average = (d.left.Read(surface.Heights) + d.right.Read(surface.Heights) +
                    d.back.Read(surface.Heights) + d.front.Read(surface.Heights)) * .25f;
                float rounding = Mathf.Clamp((average - d.center.Read(surface.Heights)) * .5f * scaleY, -.06f, 0);
                p.y += (rounding + d.noise) * fade / scaleY;
            }
            closedSurfaceVertices[vertex] = p;
            int group = surfaceNormalIndices[vertex];
            for (int j = groupFaceOffsets[group]; j < groupFaceOffsets[group + 1]; j++)
            {
                int face = groupFaces[j];
                if (dirtyFaces[face]) continue;
                dirtyFaces[face] = true; dirtySurfaceFaces.Add(face);
            }
        }
        foreach (int face in dirtySurfaceFaces)
        {
            int t = face * 3;
            var a = closedSurfaceVertices[renderedTriangles[t]];
            var b = closedSurfaceVertices[renderedTriangles[t + 1]];
            var c = closedSurfaceVertices[renderedTriangles[t + 2]];
            faceNormals[face] = Vector3.Cross(b - a, c - a);
            dirtyFaces[face] = false;
            for (int j = 0; j < 3; j++)
            {
                int group = surfaceNormalIndices[renderedTriangles[t + j]];
                if (changedGroups[group]) continue;
                changedGroups[group] = true; dirtySurfaceGroups.Add(group);
            }
        }
        foreach (int group in dirtySurfaceGroups)
        {
            Vector3 normal = Vector3.zero;
            for (int j = groupFaceOffsets[group]; j < groupFaceOffsets[group + 1]; j++) normal += faceNormals[groupFaces[j]];
            normal = normal.sqrMagnitude > .00000001f ? normal.normalized : Vector3.up;
            surfaceNormalSums[group] = normal;
            for (int j = groupFaceOffsets[group]; j < groupFaceOffsets[group + 1]; j++)
            for (int corner = 0; corner < 3; corner++)
            {
                int vertex = renderedTriangles[groupFaces[j] * 3 + corner];
                if (surfaceNormalIndices[vertex] == group) naturalSurfaceNormals[vertex] = normal;
            }
        }
        mesh.SetVertices(closedSurfaceVertices); mesh.SetNormals(naturalSurfaceNormals);
        mesh.RecalculateBounds(); PrepareSurfaceBlend();
        if (!Application.isPlaying) colliderDirty = true;
        return true;
    }

    private bool SampleChanged(HeightSample sample) =>
        changedHeights[sample.a] || changedHeights[sample.b] || changedHeights[sample.c];

    private Vector3Int SurfaceVertexKey(int index)
    {
        Vector3 p = closedSurfaceVertices[index];
        bool top = index < topSurfaceVertexCount || (index - topSurfaceVertexCount) % 4 < 2;
        return new Vector3Int(Mathf.RoundToInt(p.x * 10000), top ? 1 : 0, Mathf.RoundToInt(p.z * 10000));
    }

    private void FinishNaturalSurface()
    {
        // Only the render skin changes: stock, collision and scooping keep their
        // original heights. Fade at the ground so cleared areas stay cleared.
        Vector3 metric = WorldScale;
        float scaleY = Mathf.Max(.001f, metric.y);
        float radius = Mathf.Min(VisualHeightField.Spacing * .5f, .15f / Mathf.Max(.001f, metric.x));
        var surface = VisualHeightField;
        if (surfaceDetailScale != metric) { surfaceDetails.Clear(); surfaceDetailScale = metric; }
        bool layoutChanged = blendToPositions.Count != closedSurfaceVertices.Count || cachedNormalKeys.Count != closedSurfaceVertices.Count;
        for (int i = 0; i < closedSurfaceVertices.Count && !layoutChanged; i++)
            layoutChanged = cachedNormalKeys[i] != SurfaceVertexKey(i);
        bool topologyChanged = renderedTriangles == null || renderedTriangleCount != closedSurfaceTriangles.Count || layoutChanged;
        for (int i = 0; i < closedSurfaceTriangles.Count && !topologyChanged; i++)
            topologyChanged = renderedTriangles[i] != closedSurfaceTriangles[i];
        bool rebuildDependencies = layoutChanged || surfaceDetails.Count != closedSurfaceVertices.Count || detailOffsets == null;
        bool refreshAll = topologyChanged || surfaceDetails.Count != closedSurfaceVertices.Count ||
            renderedHeights == null || renderedHeights.Length != surface.Heights.Length;
        if (renderedHeights == null || renderedHeights.Length != surface.Heights.Length)
        {
            renderedHeights = new float[surface.Heights.Length];
            changedHeights = new bool[surface.Heights.Length];
        }
        for (int i = 0; i < renderedHeights.Length; i++)
        {
            changedHeights[i] = refreshAll || renderedHeights[i] != surface.Heights[i];
            renderedHeights[i] = surface.Heights[i];
        }
        if (changedVertices == null || changedVertices.Length != closedSurfaceVertices.Count)
            changedVertices = new bool[closedSurfaceVertices.Count];
        for (int i = 0; i < closedSurfaceVertices.Count; i++)
        {
            Vector3 p = closedSurfaceVertices[i];
            Vector2 xz = new Vector2(p.x, p.z);
            SurfaceDetail detail;
            if (i < surfaceDetails.Count && surfaceDetails[i].position == xz) detail = surfaceDetails[i];
            else
            {
                detail = new SurfaceDetail { position = xz, ground = GroundOffset(p.x, p.z),
                    noise = (Mathf.PerlinNoise(p.x * metric.x * 4.3f + 17.1f, p.z * metric.z * 4.3f + 31.7f) - .5f) * .05f,
                    center = HeightSample.At(surface, p.x, p.z), left = HeightSample.At(surface, p.x-radius, p.z),
                    right = HeightSample.At(surface, p.x+radius, p.z), back = HeightSample.At(surface, p.x, p.z-radius),
                    front = HeightSample.At(surface, p.x, p.z+radius) };
                if (i < surfaceDetails.Count) surfaceDetails[i] = detail; else surfaceDetails.Add(detail);
            }
            // A local scoop changes only a small part of the field. Retain the
            // exact previous skin elsewhere, including its noise and peak rounding.
            bool changed = refreshAll || SampleChanged(detail.center) || SampleChanged(detail.left) ||
                SampleChanged(detail.right) || SampleChanged(detail.back) || SampleChanged(detail.front);
            changedVertices[i] = changed;
            if (!changed) { closedSurfaceVertices[i] = blendToPositions[i]; continue; }
            float aboveGround = (p.y - detail.ground) * scaleY;
            float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01(aboveGround / .18f));
            if (fade <= 0) continue;
            float center = detail.center.Read(surface.Heights);
            float average = (detail.left.Read(surface.Heights) + detail.right.Read(surface.Heights) +
                detail.back.Read(surface.Heights) + detail.front.Read(surface.Heights)) * .25f;
            // Round peaks by at most six centimetres, without filling empty holes.
            float rounding = Mathf.Clamp((average - center) * .5f * scaleY, -.06f, 0);
            p.y += (rounding + detail.noise) * fade / scaleY;
            closedSurfaceVertices[i] = p;
        }
        if (surfaceDetails.Count > closedSurfaceVertices.Count)
            surfaceDetails.RemoveRange(closedSurfaceVertices.Count, surfaceDetails.Count - closedSurfaceVertices.Count);
        // Wall clipping duplicates triangle vertices. Average their normals by
        // position, otherwise every clipped triangle produces a hard lighting seam.
        bool rebuildGroups = topologyChanged;
        if (rebuildGroups)
        {
            surfaceNormalGroups.Clear(); surfaceNormalIndices.Clear(); surfaceNormalSums.Clear();
            cachedNormalKeys.Clear();
            for (int i = 0; i < closedSurfaceVertices.Count; i++)
            {
                var key = SurfaceVertexKey(i);
                cachedNormalKeys.Add(key);
                if (!surfaceNormalGroups.TryGetValue(key, out int group))
                {
                    group = surfaceNormalSums.Count;
                    surfaceNormalGroups.Add(key, group);
                    surfaceNormalSums.Add(Vector3.zero);
                }
                surfaceNormalIndices.Add(group);
            }
        }
        if (topologyChanged)
        {
            renderedTriangleCount = closedSurfaceTriangles.Count;
            ReserveSurfaceBuffer(ref renderedTriangles, renderedTriangleCount);
            closedSurfaceTriangles.CopyTo(renderedTriangles);
            ReserveSurfaceBuffer(ref faceNormals, renderedTriangleCount / 3);
            ReserveSurfaceBuffer(ref changedGroups, surfaceNormalSums.Count);
            ReserveSurfaceBuffer(ref dirtyFaces, renderedTriangleCount / 3);
            // CSR adjacency retains the original triangle accumulation order,
            // so updating a subset gives the same normals as a complete rebuild.
            ReserveSurfaceBuffer(ref groupFaceOffsets, surfaceNormalSums.Count + 1);
            System.Array.Clear(groupFaceOffsets, 0, surfaceNormalSums.Count + 1);
            for (int i = 0; i < renderedTriangleCount; i++) groupFaceOffsets[surfaceNormalIndices[renderedTriangles[i]] + 1]++;
            for (int i = 1; i <= surfaceNormalSums.Count; i++) groupFaceOffsets[i] += groupFaceOffsets[i - 1];
            ReserveSurfaceBuffer(ref normalFaceCursor, surfaceNormalSums.Count + 1);
            System.Array.Copy(groupFaceOffsets, normalFaceCursor, surfaceNormalSums.Count + 1);
            ReserveSurfaceBuffer(ref groupFaces, renderedTriangleCount);
            for (int i = 0; i < renderedTriangleCount; i++)
                groupFaces[normalFaceCursor[surfaceNormalIndices[renderedTriangles[i]]]++] = i / 3;
            naturalSurfaceNormals.Clear();
            for (int i = 0; i < closedSurfaceVertices.Count; i++) naturalSurfaceNormals.Add(Vector3.up);
        }
        System.Array.Clear(changedGroups, 0, changedGroups.Length);
        for (int t = 0; t < closedSurfaceTriangles.Count; t += 3)
        {
            if (!changedVertices[closedSurfaceTriangles[t]] && !changedVertices[closedSurfaceTriangles[t + 1]] &&
                !changedVertices[closedSurfaceTriangles[t + 2]]) continue;
            Vector3 a = closedSurfaceVertices[closedSurfaceTriangles[t]];
            Vector3 b = closedSurfaceVertices[closedSurfaceTriangles[t + 1]];
            Vector3 c = closedSurfaceVertices[closedSurfaceTriangles[t + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a);
            faceNormals[t / 3] = normal;
            for (int j = 0; j < 3; j++)
            {
                int group = surfaceNormalIndices[closedSurfaceTriangles[t + j]];
                changedGroups[group] = true;
            }
        }
        for (int i = 0; i < surfaceNormalSums.Count; i++)
        {
            if (!changedGroups[i]) continue;
            Vector3 normal = Vector3.zero;
            for (int f = groupFaceOffsets[i]; f < groupFaceOffsets[i + 1]; f++) normal += faceNormals[groupFaces[f]];
            surfaceNormalSums[i] = normal.sqrMagnitude > .00000001f ? normal.normalized : Vector3.up;
        }
        for (int i = 0; i < surfaceNormalIndices.Count; i++)
            if (changedGroups[surfaceNormalIndices[i]]) naturalSurfaceNormals[i] = surfaceNormalSums[surfaceNormalIndices[i]];
        if (topologyChanged) mesh.Clear();
        mesh.SetVertices(closedSurfaceVertices);
        if (topologyChanged)
        {
            mesh.SetUVs(0, closedSurfaceUV);
            mesh.SetTriangles(closedSurfaceTriangles, 0, false);
        }
        mesh.SetNormals(naturalSurfaceNormals);
        // Grain uses world-space triplanar normals; mesh tangents are not consumed.
        mesh.RecalculateBounds();
        PrepareSurfaceBlend();
        if (rebuildDependencies) BuildSurfaceDependencies();
    }
    private readonly List<Vector2Int> clippedEdges = new List<Vector2Int>();
    private int[] clippedRenderMap;
    private readonly List<int> clippedRenderSources = new List<int>();

    private void CloseClippedEdges()
    {
        if (closedBoundary != boundary || clippedRenderMap == null || clippedRenderMap.Length != boundary.Positions.Count)
        {
            closedBoundary = boundary;
            clippedEdges.Clear();
            clippedRenderMap = new int[boundary.Positions.Count];
            clippedRenderSources.Clear();
            // Clipping splits vertices per triangle. Weld X/Z keys only for finding
            // perimeter edges; do not alter the top surface or its volume weights.
            var pointIds = new Dictionary<Vector2Int, int>();
            var edges = new Dictionary<Vector2Int, Vector2Int>();
            var counts = new Dictionary<Vector2Int, int>();
            int PointId(int index)
            {
                Vector3 p = boundary.Positions[index];
                var key = new Vector2Int(Mathf.RoundToInt(p.x * 100000), Mathf.RoundToInt(p.z * 100000));
                if (!pointIds.TryGetValue(key, out int id))
                {
                    id = pointIds.Count; pointIds.Add(key, id);
                    clippedRenderSources.Add(index);
                }
                clippedRenderMap[index] = id;
                return id;
            }
            for (int i = 0; i < boundary.Positions.Count; i += 3)
            for (int side = 0; side < 3; side++)
            {
                int a = i + side, b = i + (side + 1) % 3;
                int x = PointId(a), y = PointId(b);
                if (x == y) continue;
                var key = new Vector2Int(Mathf.Min(x, y), Mathf.Max(x, y));
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
                edges[key] = new Vector2Int(a, b);
            }
            foreach (var entry in counts)
                if (entry.Value == 1) clippedEdges.Add(edges[entry.Key]);
        }
        // Clipping emits separate vertices for every triangle. Share their X/Z
        // samples so deformation, normals and blend uploads run once per point.
        // Triangle coverage and the authoritative collision/stock field stay intact.
        closedSurfaceVertices.Clear(); closedSurfaceUV.Clear();
        foreach (int source in clippedRenderSources)
        {
            closedSurfaceVertices.Add(boundary.Positions[source]);
            closedSurfaceUV.Add(boundary.UV[source]);
        }
        closedSurfaceTriangles.Clear();
        foreach (int index in boundary.Triangles) closedSurfaceTriangles.Add(clippedRenderMap[index]);
        sideTriangleStart = closedSurfaceTriangles.Count;
        topSurfaceVertexCount = closedSurfaceVertices.Count;
        float inset = .045f / WorldScale.y;
        foreach (var edge in clippedEdges) AddGroundEdge(clippedRenderMap[edge.x], clippedRenderMap[edge.y], inset);
    }
    private void CloseDroppedEdges(GrainHeightField surface)
    {
        closedSurfaceVertices.Clear(); closedSurfaceVertices.AddRange(vertices);
        closedSurfaceUV.Clear(); closedSurfaceUV.AddRange(surfaceUV);
        closedSurfaceTriangles.Clear(); closedSurfaceTriangles.AddRange(activeTriangles);
        sideTriangleStart = closedSurfaceTriangles.Count;
        topSurfaceVertexCount = closedSurfaceVertices.Count;
        int n = field.Resolution;
        float inset = .045f / WorldScale.y;
        for (int i = 0; i < n - 1; i++)
        {
            AddGroundEdge(i + 1, i, inset);
            AddGroundEdge(i * n, (i + 1) * n, inset);
            AddGroundEdge((n - 1) * n + i, (n - 1) * n + i + 1, inset);
            AddGroundEdge((i + 1) * n + n - 1, i * n + n - 1, inset);
        }
    }
    private void AddGroundEdge(int a, int b, float inset)
    {
        Vector3 topA = closedSurfaceVertices[a], topB = closedSurfaceVertices[b];
        Vector3 bottomA = topA, bottomB = topB;
        bottomA.y = GroundOffset(topA.x, topA.z) - inset;
        bottomB.y = GroundOffset(topB.x, topB.z) - inset;
        if (Mathf.Max(topA.y - bottomA.y, topB.y - bottomB.y) <= inset) return;
        // Shared corners must coincide to keep the perimeter closed.
        int start = closedSurfaceVertices.Count;
        closedSurfaceVertices.Add(topA); closedSurfaceVertices.Add(topB);
        closedSurfaceVertices.Add(bottomA); closedSurfaceVertices.Add(bottomB);
        closedSurfaceUV.Add(closedSurfaceUV[a]); closedSurfaceUV.Add(closedSurfaceUV[b]);
        closedSurfaceUV.Add(closedSurfaceUV[a]); closedSurfaceUV.Add(closedSurfaceUV[b]);
        closedSurfaceTriangles.Add(start); closedSurfaceTriangles.Add(start + 2); closedSurfaceTriangles.Add(start + 1);
        closedSurfaceTriangles.Add(start + 1); closedSurfaceTriangles.Add(start + 2); closedSurfaceTriangles.Add(start + 3);
    }
    private void OffsetBoundaryGround()
    {
        if (groundField == null) return;
        for (int i = 0; i < boundary.Positions.Count; i++)
        {
            Vector3 p = boundary.Positions[i];
            p.y += GroundOffset(p.x, p.z);
            boundary.Positions[i] = p;
        }
    }
    private readonly List<Vector3> collisionVertices = new List<Vector3>();
    private readonly List<int> collisionIndices = new List<int>();
    private readonly List<Vector3> cookedVertices = new List<Vector3>();
    private readonly List<int> cookedIndices = new List<int>();

    private void RefreshCollider()
    {
        using var measurement = ColliderRefreshMarker.Auto();
        nextCollider = Time.time + 1f / colliderRate;
        bool animating = settling || visualSettling || surfaceBlend < 1f;
        colliderDirty = animating;
        collisionIndices.Clear(); collisionVertices.Clear();
        // Use the renderer's positions and interpolation. Normal groups weld
        // duplicate skirt/clipped vertices without joining top and bottom.
        for (int i = 0; i < surfaceNormalSums.Count; i++) collisionVertices.Add(Vector3.zero);
        for (int i = 0; i < closedSurfaceVertices.Count; i++)
        {
            Vector3 point = surfaceBlend < 1f
                ? Vector3.Lerp(blendFromPositions[i], blendToPositions[i], surfaceBlend)
                : closedSurfaceVertices[i];
            collisionVertices[surfaceNormalIndices[i]] = point;
        }
        foreach (int vertex in closedSurfaceTriangles) collisionIndices.Add(surfaceNormalIndices[vertex]);
        bool changed = cookedVertices.Count != collisionVertices.Count || cookedIndices.Count != collisionIndices.Count;
        // Thin visible remnants are only millimetres high; a 2.5 cm tolerance
        // leaves their collider underground after the render surface has risen.
        float tolerance = .001f / Mathf.Max(.0001f, WorldScale.y);
        for (int i = 0; i < collisionVertices.Count && !changed; i++)
            changed = Mathf.Abs(collisionVertices[i].y - cookedVertices[i].y) >= tolerance ||
                collisionVertices[i].x != cookedVertices[i].x || collisionVertices[i].z != cookedVertices[i].z;
        for (int i = 0; i < collisionIndices.Count && !changed; i++) changed = collisionIndices[i] != cookedIndices[i];
        if (!changed) return;
        meshCollider.sharedMesh = null;
        collisionMesh.Clear();
        collisionMesh.SetVertices(collisionVertices); collisionMesh.SetTriangles(collisionIndices, 0);
        collisionMesh.RecalculateBounds();
        meshCollider.enabled = collisionIndices.Count > 0;
        if (meshCollider.enabled) meshCollider.sharedMesh = collisionMesh;
        cookedVertices.Clear(); cookedVertices.AddRange(collisionVertices);
        cookedIndices.Clear(); cookedIndices.AddRange(collisionIndices);
    }

    private void OnDestroy() => ReleaseMeshes();

    private void DetectWalls()
    {
        if (fitToWalls) GrainWallBoundary.Detect(transform, size, initialHeight, wallMask, detectedWalls, wallHits);
        else detectedWalls.Clear();
    }

    private void ReleaseMeshes()
    {
        spatial?.Dispose(); spatial = null;
        if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        if (collisionMesh != null) { if (Application.isPlaying) Destroy(collisionMesh); else DestroyImmediate(collisionMesh); }
        mesh = collisionMesh = null;
        surfaceDetails.Clear(); cachedNormalKeys.Clear();
        renderedHeights = null; renderedTriangles = null;
        detailOffsets = null;
        blendFromPositions.Clear(); blendFromNormals.Clear();
        blendToPositions.Clear(); blendToNormals.Clear();
        surfaceBlend = 1f;
        appearance.Block.SetFloat(SurfaceBlendId, 1f);
        visualField = null; visualSettling = false;
        cookedVertices.Clear(); cookedIndices.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .7f, .1f, .6f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * initialHeight * .5f, new Vector3(size, initialHeight, size));
    }
}
