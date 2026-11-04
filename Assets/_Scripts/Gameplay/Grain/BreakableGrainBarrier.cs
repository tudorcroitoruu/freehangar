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

    [SerializeField] private bool alwaysExposed;
    public bool IsBroken => BuriedFind.WasFound(SaveSystem.Data, barrierId);
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
        // Older saves used a different wall's discovery ID for all six box panels.
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
        if (BuriedFind.WasFound(SaveSystem.Data, barrierId)) { gameObject.SetActive(false); return; }
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
        foreach (Renderer visual in visuals)
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
        if (!isActiveAndEnabled || !exposed || BuriedFind.WasFound(SaveSystem.Data, barrierId)) return false;
        string tool = material == Material.Wood ? "axe (2)" : "hammer (3)";
        string label = $"{material} wall: {tool}, left click ({HitsRemaining} hits)";
        Bounds bounds = VisualBounds();
        prompt = new InteractionPromptData(new Vector3(bounds.center.x, bounds.max.y + .15f, bounds.center.z), label, false);
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor) => false;

    public bool AcceptsTool(PlayerCombatTools.Tool tool) => material == Material.Wood ? tool == PlayerCombatTools.Tool.Axe : tool == PlayerCombatTools.Tool.Hammer;

    public bool TryHit(PlayerCombatTools.Tool tool)
    {
        if (!isActiveAndEnabled || !exposed || SaveSystem.Data == null || string.IsNullOrEmpty(barrierId)) return false;
        if (!AcceptsTool(tool))
            return false;
        GameplayAudio.Instance?.BarrierHit(material == Material.Metal);
        if (++hits < hitsRequired) return true;

        PlayerData data = SaveSystem.Data;
        if (data.foundSecrets == null) data.foundSecrets = Array.Empty<string>();
        int index = data.foundSecrets.Length;
        Array.Resize(ref data.foundSecrets, index + 1);
        data.foundSecrets[index] = barrierId;
        SaveSystem.SaveData();
        gameObject.SetActive(false);
        return true;
    }
}
