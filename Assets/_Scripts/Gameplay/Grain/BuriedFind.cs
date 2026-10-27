using System;
using System.Collections;
using UnityEngine;

/// <summary>A key or cash chest becomes interactive when the grain above it has been removed.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class BuriedFind : MonoBehaviour, IInteractable
{
    public enum Kind { Key, CashChest }

    [SerializeField] private Kind kind;
    [SerializeField] private string findId;
    [SerializeField] private string requiredKeyId;
    [SerializeField, Min(1)] private int cashReward = 500;
    [SerializeField] private GrainPile coveringPile;
    [SerializeField] private Transform lidPivot;
    [SerializeField] private float lidOpenDegrees = -110f;
    private BoxCollider interactionCollider;
    private Renderer[] visuals;
    private float nextRevealCheck;
    private bool revealed;
    private bool opening;
    private static bool profileLoadRequested;

    public string FindId => findId;
    public bool IsRevealed => revealed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProfileRequest() => profileLoadRequested = false;

    private async void Start()
    {
        // Gameplay is often played directly in the Editor without the boot scene.
        if (GlobalManager.Instance != null || SaveSystem.savesLoaded || profileLoadRequested) return;
        profileLoadRequested = true;
        await SaveSystem.LoadData();
    }

    private void Awake()
    {
        interactionCollider = GetComponent<BoxCollider>();
        visuals = GetComponentsInChildren<Renderer>(true);
        interactionCollider.isTrigger = false;
        SetRevealed(false);
    }

    private void Update()
    {
        if (opening) return;
        if (WasFound(SaveSystem.Data, findId)) { gameObject.SetActive(false); return; }
        if (revealed || Time.time < nextRevealCheck) return;
        nextRevealCheck = Time.time + .3f;
        if (coveringPile == null) { SetRevealed(true); return; }
        // Closed hangars keep their grain disabled until opened. That is not an excavation.
        if (!coveringPile.isActiveAndEnabled) return;
        Bounds bounds = VisualBounds();
        float surface = coveringPile.SurfaceHeight(bounds.center);
        if (surface <= bounds.max.y + .08f) SetRevealed(true);
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

    private void SetRevealed(bool value)
    {
        revealed = value;
        if (interactionCollider != null) interactionCollider.enabled = value;
        if (visuals != null) foreach (Renderer visual in visuals) if (visual != null) visual.enabled = value;
    }

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || !revealed || string.IsNullOrEmpty(findId) || WasFound(SaveSystem.Data, findId)) return false;
        bool hasProfile = SaveSystem.savesLoaded && SaveSystem.Data != null;
        bool hasKey = kind != Kind.CashChest || WasFound(SaveSystem.Data, requiredKeyId);
        string action = !hasProfile ? "Load save to collect" : !hasKey ? "Requires hidden key" :
            kind == Kind.Key ? "Take hidden key" : $"Open mystery chest (+${cashReward})";
        Bounds bounds = VisualBounds();
        prompt = new InteractionPromptData(new Vector3(bounds.center.x, bounds.max.y + .15f, bounds.center.z),
            action, hasProfile && hasKey);
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out var prompt) || !prompt.ShowKey) return false;
        PlayerData data = SaveSystem.Data;
        if (kind == Kind.CashChest)
        {
            if (cashReward <= 0 || data.cash > int.MaxValue - cashReward) return false;
            data.cash += cashReward;
        }
        if (data.foundSecrets == null) data.foundSecrets = Array.Empty<string>();
        int index = data.foundSecrets.Length;
        Array.Resize(ref data.foundSecrets, index + 1);
        data.foundSecrets[index] = findId;
        SaveSystem.SaveData();
        if (kind == Kind.CashChest && lidPivot != null)
        {
            opening = true;
            interactionCollider.enabled = false;
            StartCoroutine(OpenAndHide());
        }
        else gameObject.SetActive(false);
        return true;
    }

    private IEnumerator OpenAndHide()
    {
        Quaternion closed = lidPivot.localRotation;
        Quaternion open = closed * Quaternion.Euler(lidOpenDegrees, 0f, 0f);
        float elapsed = 0f;
        while (elapsed < 1.1f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 1.1f));
            lidPivot.localRotation = Quaternion.Slerp(closed, open, t);
            yield return null;
        }
        yield return new WaitForSeconds(1.5f);
        gameObject.SetActive(false);
    }

    public static bool WasFound(PlayerData data, string id)
    {
        if (data == null || string.IsNullOrEmpty(id) || data.foundSecrets == null) return false;
        foreach (string found in data.foundSecrets) if (found == id) return true;
        return false;
    }
}
