using System;
using UnityEngine;

/// <summary>A buried wall becomes solid when uncovered and can then be broken with the right tool.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class BreakableGrainBarrier : MonoBehaviour, IInteractable
{
    public enum Material { Wood, Metal }

    [SerializeField] private string barrierId;
    [SerializeField] private Material material;
    [SerializeField, Min(1)] private int hitsRequired = 3;
    [SerializeField] private GrainPile coveringPile;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .5f;

    [SerializeField] private bool alwaysExposed;
    // Legacy scene copies used the same IDs for the wood and metal enclosures.
    // Keep their discoveries independent, including in scenes authored with the old IDs.
    private string PersistentId => !string.IsNullOrEmpty(barrierId) && barrierId.StartsWith("farm.pistol.box.panel.", StringComparison.Ordinal)
        ? barrierId.Replace(".box.panel.", material == Material.Metal ? ".box.metal.panel." : ".box.wood.panel.")
        : barrierId;
    public bool IsBroken => hits >= hitsRequired || BuriedFind.WasFound(SaveSystem.Data, PersistentId);
    private BoxCollider blockingCollider;
    private Renderer[] visuals;
    private int hits;
    private bool exposed;
    private float nextRevealCheck;
    private float topHeight;

    public bool IsExposed => exposed;
    public int HitsRemaining => Mathf.Max(0, hitsRequired - hits);
    public int SavedHits => hits;
    public bool RestoreSave(int savedHits, bool wasExposed, bool wasActive = true)
    {
        hits = Mathf.Clamp(savedHits, 0, hitsRequired);
        SetExposed(alwaysExposed || wasExposed);
        // Per-object hit counts preserve genuine destruction when legacy shared
        // discovery IDs cannot identify which enclosure was actually broken.
        return alwaysExposed ? !IsBroken : wasActive;
    }

    private void Awake()
    {
        blockingCollider = GetComponent<BoxCollider>();
        visuals = GetComponentsInChildren<Renderer>(true);
        topHeight = VisualBounds().max.y;
        nextRevealCheck = Time.time + UnityEngine.Random.Range(0f, .3f);
        SetExposed(alwaysExposed);
    }

    private void Update()
    {
        if (IsBroken) { gameObject.SetActive(false); return; }
        if (exposed || Time.time < nextRevealCheck || coveringPile == null || !coveringPile.isActiveAndEnabled) return;
        nextRevealCheck = Time.time + .3f;
        if (AnyFaceUncovered(topHeight + .08f))
            SetExposed(true);
    }

    private bool AnyFaceUncovered(float top)
    {
        Vector3 center = transform.TransformPoint(blockingCollider.center);
        Vector3 side = transform.right * (blockingCollider.size.x * transform.lossyScale.x * .45f);
        Vector3 face = transform.forward * (blockingCollider.size.z * transform.lossyScale.z * .55f);
        return IsUncovered(center + face) || IsUncovered(center - face) ||
            IsUncovered(center + side) || IsUncovered(center - side);

        bool IsUncovered(Vector3 point) => coveringPile.IsInsideBoundary(point) &&
            coveringPile.SurfaceHeight(point) <= top;
    }

    private Bounds VisualBounds()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer visual in visuals ?? GetComponentsInChildren<Renderer>(true))
        {
            if (visual == null) continue;
            if (first) { bounds = visual.bounds; first = false; }
            else bounds.Encapsulate(visual.bounds);
        }
        return bounds;
    }

    private void SetExposed(bool value)
    {
        exposed = value;
        blockingCollider.enabled = value;
        foreach (Renderer visual in visuals) if (visual != null) visual.enabled = value;
    }

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || !exposed || IsBroken) return false;
        string label = material == Material.Wood ? "Wooden Wall" : "Metal Wall";
        Vector3 origin = promptAnchor != null ? promptAnchor.position : transform.position;
        prompt = new InteractionPromptData(origin + promptOffset, label, false);
        prompt.RequiredTool = material == Material.Wood ? PlayerCombatTools.Tool.Axe : PlayerCombatTools.Tool.Hammer;
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor) => false;

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = promptAnchor != null ? promptAnchor.position : transform.position;
        Vector3 position = origin + promptOffset;
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, position);
        Gizmos.DrawWireSphere(position, .08f);
        Gizmos.color = previousColor;
#if UNITY_EDITOR
        UnityEditor.Handles.Label(position + Vector3.up * .12f, "Barrier prompt (promptOffset)");
#endif
    }

    public bool AcceptsTool(PlayerCombatTools.Tool tool) => material == Material.Wood ? tool == PlayerCombatTools.Tool.Axe : tool == PlayerCombatTools.Tool.Hammer;

    public bool TryHit(PlayerCombatTools.Tool tool)
    {
        if (!isActiveAndEnabled || !exposed || SaveSystem.Data == null || string.IsNullOrEmpty(barrierId) || IsBroken) return false;
        if (!AcceptsTool(tool))
            return false;
        GameplayAudio.Instance?.BarrierHit(material == Material.Metal);
        hits = Mathf.Min(hitsRequired, hits + CombatToolUpgrades.WallDamage(CombatToolUpgrades.Level(SaveSystem.Data, tool), hitsRequired));
        if (hits < hitsRequired) return true;

        PlayerData data = SaveSystem.Data;
        if (data.foundSecrets == null) data.foundSecrets = Array.Empty<string>();
        int index = data.foundSecrets.Length;
        Array.Resize(ref data.foundSecrets, index + 1);
        data.foundSecrets[index] = PersistentId;
        // Keep destruction in the live profile; normal saves capture the world.
        // Capturing every grain height field synchronously here stalls the finishing strike.
        SpawnBreakParticles();
        gameObject.SetActive(false);
        return true;
    }
    private void SpawnBreakParticles()
    {
        if (!Application.isPlaying || WorldSaveRuntime.IsRestoring || WorldSaveRuntime.IsSeeking) return;
        // Detached effects survive disabling the wall. Reuse meshes/materials without allocations
        // of geometry or rigidbodies; debris is visual only and cannot block the player.
        int sources = 0;
        foreach (var visual in visuals)
        {
            if (!visual || !visual.enabled || !visual.TryGetComponent<MeshFilter>(out var filter) ||
                !filter.sharedMesh || !visual.sharedMaterial) continue;
            if (++sources > 8) break;
            var debris = new GameObject("Wall mesh debris");
            debris.transform.position = visual.bounds.center;
            var particles = debris.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.65f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.8f, 2.4f);
            main.gravityModifier = .6f;
            Vector3 meshSize = filter.sharedMesh.bounds.size;
            float longest = Mathf.Max(.001f, Mathf.Max(meshSize.x, Mathf.Max(meshSize.y, meshSize.z)));
            main.startSize = new ParticleSystem.MinMaxCurve(.06f / longest, .18f / longest);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = visual.bounds.size;
            shape.randomDirectionAmount = 1f;
            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-5f, 5f);
            spin.y = new ParticleSystem.MinMaxCurve(-5f, 5f);
            spin.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.65f, 1f), new Keyframe(1f, 0f)));
            var renderer = debris.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = filter.sharedMesh;
            renderer.sharedMaterials = visual.sharedMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            Destroy(debris, 2f);
        }
    }
}
