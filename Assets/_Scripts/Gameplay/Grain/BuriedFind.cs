using System;
using System.Collections;
using UnityEngine;

/// <summary>A key or cash chest becomes interactive when the grain above it has been removed.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class BuriedFind : MonoBehaviour, IInteractable
{
    public enum Kind { Key, CashChest, UpgradeVoucher, InfiniteStamina, CashCache, Hammer, Sledgehammer, HangarKey, Broom }

    [SerializeField] private Kind kind;
    [SerializeField] private string findId;
    [SerializeField] private string requiredKeyId;
    [SerializeField] private string matchingHangarKeyId;
    [SerializeField] private string matchingKeyColor;
    [SerializeField] private Material matchingKeyMaterial;
    [SerializeField, Min(1)] private int cashReward = 500;
    [SerializeField, Min(1)] private int staminaDurationSeconds = 300;
    [SerializeField] private GrainPile coveringPile;
    [SerializeField] private bool settleOnReveal;
    [SerializeField] private Transform lidPivot;
    [SerializeField] private float lidOpenDegrees = -110f;
    [Header("Discovery presentation")]
    [SerializeField] private Transform keySocket;
    [SerializeField] private GameObject insertionKeyPrefab;
    [SerializeField] private DiscoveryBurst pickupEffect;
    [SerializeField, Range(2, 5)] private int targetHangar = 2;
    private BoxCollider interactionCollider;
    private Renderer[] visuals;
    private float nextRevealCheck;
    private bool revealed;
    private bool opening;
    private bool committed;
    private PlayerInteractor actor;
    private PlayerData animationProfile;
    private Camera view;
    private GameObject insertionKey;
    private Vector3 originalPosition, originalScale;
    private Quaternion originalRotation, closedLid;
    private Rigidbody body;
    private bool wasKinematic;
    private Renderer[] heldVisuals;
    private bool[] heldVisible;
    private static bool profileLoadRequested;

    public string FindId => findId;
    public bool IsRevealed => revealed;
    public Kind FindKind => kind;
    public int TargetHangar => targetHangar;
    public bool IsAnimating => opening;

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
        if (value && settleOnReveal && coveringPile != null)
        {
            Bounds bounds = VisualBounds();
            float surface = coveringPile.SurfaceHeight(bounds.center);
            transform.position += Vector3.up * (surface + .04f - bounds.min.y);
            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.useGravity = true;
            body.isKinematic = false;
        }
        if (interactionCollider != null) interactionCollider.enabled = value;
        if (visuals != null) foreach (Renderer visual in visuals) if (visual != null) visual.enabled = value;
    }

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || opening || !revealed || string.IsNullOrEmpty(findId) || WasFound(SaveSystem.Data, findId)) return false;
        bool hasProfile = SaveSystem.savesLoaded && SaveSystem.Data != null;
        bool hasKey = kind != Kind.CashChest || WasFound(SaveSystem.Data, requiredKeyId) || (!string.IsNullOrEmpty(matchingHangarKeyId) && WasFound(SaveSystem.Data, matchingHangarKeyId));
        string action = !hasProfile ? "Load save to collect" : !hasKey ? (string.IsNullOrEmpty(matchingKeyColor) ? "Requires chest key" : $"Requires {matchingKeyColor} key") :
            kind == Kind.Key ? "Take CHEST key" :
            kind == Kind.HangarKey ? $"Take HANGAR {targetHangar} key" :
            kind == Kind.Broom ? "Take broom (equip with 2)" :
            kind == Kind.Hammer ? "Take hammer (equip with 3)" :
            kind == Kind.Sledgehammer ? "Take sledgehammer (equip with 4)" :
            kind == Kind.UpgradeVoucher ? "Take free upgrade voucher" :
            kind == Kind.InfiniteStamina ? $"Drink stamina tonic ({staminaDurationSeconds / 60} min)" :
            kind == Kind.CashCache ? $"Take cash cache (+${cashReward})" :
            $"Open mystery chest (+${cashReward})";
        Bounds bounds = VisualBounds();
        prompt = new InteractionPromptData(new Vector3(bounds.center.x, bounds.max.y + .15f, bounds.center.z),
            action, hasProfile && hasKey);
        if (interactor != null && (kind == Kind.CashChest || kind == Kind.InfiniteStamina))
            prompt.RequiresFreeHands = (interactor.Bags != null && interactor.Bags.IsCarrying) ||
                (interactor.Items != null && interactor.Items.IsCarrying) || interactor.PushedWheelbarrow != null;
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out var prompt) || !prompt.ShowKey || prompt.RequiresFreeHands || Time.timeScale <= 0) return false;
        if (kind == Kind.CashChest || kind == Kind.InfiniteStamina)
        {
            if (interactor == null || interactor.DiscoveryBusy) return false;
            var player = interactor.GetComponent<PlayerController>();
            if (player != null && player.Shovel != null && player.Shovel.IsBusy) return false;
            view = player != null ? player.characterCamera : interactor.GetComponentInChildren<Camera>();
            if (view == null || !interactor.BeginDiscovery(this)) return false;
            actor = interactor; animationProfile = SaveSystem.Data; opening = true; committed = false;
            originalPosition = transform.position; originalRotation = transform.rotation; originalScale = transform.localScale;
            if (lidPivot != null) closedLid = lidPivot.localRotation;
            body = GetComponent<Rigidbody>();
            if (body != null) { wasKinematic = body.isKinematic; body.isKinematic = true; }
            interactionCollider.enabled = false;
            heldVisuals = view.GetComponentsInChildren<Renderer>(); heldVisible = new bool[heldVisuals.Length];
            for (int i = 0; i < heldVisuals.Length; i++) { heldVisible[i] = heldVisuals[i].enabled; heldVisuals[i].enabled = false; }
            StartCoroutine(kind == Kind.CashChest ? OpenChest() : Drink());
            return true;
        }
        if (!CommitReward()) return false;
        DiscoveryBurst.Play(pickupEffect, VisualBounds().center, Receipt(), interactor != null ? interactor.GetComponentInChildren<Camera>() : null);
        gameObject.SetActive(false);
        return true;
    }

    private bool CommitReward()
    {
        PlayerData data = SaveSystem.Data;
        if (data == null || WasFound(data, findId)) return false;
        if (kind == Kind.CashChest || kind == Kind.CashCache)
        {
            if (cashReward <= 0 || data.cash > int.MaxValue - cashReward) return false;
            data.cash += cashReward;
        }
        else if (kind == Kind.UpgradeVoucher)
        {
            if (data.freeUpgradeVouchers == int.MaxValue) return false;
            data.freeUpgradeVouchers++;
        }
        else if (kind == Kind.InfiniteStamina)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long start = Math.Max(now, data.infiniteStaminaUntilUtc);
            data.infiniteStaminaUntilUtc = Math.Min(long.MaxValue - staminaDurationSeconds, start) + staminaDurationSeconds;
        }
        if (data.foundSecrets == null) data.foundSecrets = Array.Empty<string>();
        int index = data.foundSecrets.Length;
        Array.Resize(ref data.foundSecrets, index + 1);
        data.foundSecrets[index] = findId;
        SaveSystem.SaveData();
        if (kind == Kind.HangarKey) GoalChecker.ReportProgress(GoalType.HangarKeyCollected, 1, targetHangar);
        if ((kind == Kind.CashChest || kind == Kind.CashCache) && DataHolderUI.Instance != null) DataHolderUI.Instance.RefreshDataPlayer();
        return true;
    }

    private string Receipt() => kind == Kind.HangarKey ? $"HANGAR {targetHangar} KEY\nUse at Hangar {targetHangar}" :
        kind == Kind.Broom ? "BROOM FOUND\nPress 2 to equip" : kind == Kind.Key ? "CHEST KEY FOUND" : kind == Kind.InfiniteStamina ? "INFINITE STAMINA!" :
        kind == Kind.CashChest || kind == Kind.CashCache ? $"+${cashReward}" :
        kind == Kind.UpgradeVoucher ? "FREE UPGRADE!" : kind == Kind.Hammer ? "HAMMER FOUND" : "SLEDGEHAMMER FOUND";

    private bool PresentationValid => actor != null && actor.isActiveAndEnabled && view != null &&
        view.isActiveAndEnabled && SaveSystem.Data == animationProfile;

    private IEnumerator OpenChest()
    {
        if (keySocket != null && insertionKeyPrefab != null)
        {
            Quaternion facing = Quaternion.LookRotation(-keySocket.forward, keySocket.up);
            insertionKey = Instantiate(insertionKeyPrefab, keySocket.position + keySocket.forward * .45f, facing);
            if (matchingKeyMaterial != null && WasFound(SaveSystem.Data, matchingHangarKeyId))
                foreach (var renderer in insertionKey.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = matchingKeyMaterial;
            float keyTime = 0;
            while (keyTime < 1.05f)
            {
                if (!PresentationValid) { CancelPresentation(); yield break; }
                keyTime += Time.deltaTime;
                float slide = Mathf.SmoothStep(0, 1, keyTime / .6f);
                insertionKey.transform.position = keySocket.position + keySocket.forward * Mathf.Lerp(.45f, .015f, slide);
                insertionKey.transform.rotation = facing * Quaternion.Euler(0, 0, Mathf.SmoothStep(0, 90, (keyTime - .65f) / .4f));
                yield return null;
            }
        }
        float elapsed = 0f;
        while (elapsed < .85f)
        {
            if (!PresentationValid) { CancelPresentation(); yield break; }
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / .85f);
            if (lidPivot != null) lidPivot.localRotation = closedLid * Quaternion.Euler(lidOpenDegrees * t, 0, 0);
            if (!committed && elapsed >= .28f)
            {
                if (!CommitReward()) { CancelPresentation(); yield break; }
                committed = true;
                DiscoveryBurst.Play(pickupEffect, VisualBounds().center + Vector3.up * .4f, Receipt(), view);
            }
            yield return null;
        }
        ReleaseActor();
        yield return new WaitForSeconds(1.2f);
        gameObject.SetActive(false);
    }

    private IEnumerator Drink()
    {
        float elapsed = 0;
        while (elapsed < 1.8f)
        {
            if (!PresentationValid) { CancelPresentation(); yield break; }
            elapsed += Time.deltaTime;
            float lift = Mathf.SmoothStep(0, 1, elapsed / .55f);
            float tilt = Mathf.SmoothStep(0, 1, (elapsed - .55f) / .45f);
            Vector3 hold = view.transform.TransformPoint(new Vector3(.13f, -.08f, .48f));
            float lower = Mathf.SmoothStep(0, 1, (elapsed - 1.4f) / .4f);
            transform.position = Vector3.Lerp(originalPosition, hold, lift) - view.transform.up * lower * .3f;
            transform.rotation = Quaternion.Slerp(originalRotation, view.transform.rotation * Quaternion.Euler(-68 * tilt, 0, -12), lift);
            transform.localScale = originalScale * (1 - lower);
            if (!committed && elapsed >= 1.2f)
            {
                if (!CommitReward()) { CancelPresentation(); yield break; }
                committed = true;
                DiscoveryBurst.Play(pickupEffect, view.transform.position + view.transform.forward * 1.1f, Receipt(), view);
            }
            yield return null;
        }
        gameObject.SetActive(false);
    }

    private void ReleaseActor()
    {
        if (actor != null) actor.EndDiscovery(this);
        if (heldVisuals != null) for (int i = 0; i < heldVisuals.Length; i++) if (heldVisuals[i] != null) heldVisuals[i].enabled = heldVisible[i];
        heldVisuals = null; heldVisible = null; actor = null;
        if (insertionKey != null) Destroy(insertionKey);
        insertionKey = null;
    }
    private void CancelPresentation()
    {
        if (!opening) return;
        ReleaseActor();
        if (!committed)
        {
            transform.SetPositionAndRotation(originalPosition, originalRotation); transform.localScale = originalScale;
            if (lidPivot != null) lidPivot.localRotation = closedLid;
            if (body != null) body.isKinematic = wasKinematic;
            if (interactionCollider != null) interactionCollider.enabled = revealed;
        }
        else gameObject.SetActive(false);
        opening = false;
    }
    private void OnDisable() { StopAllCoroutines(); CancelPresentation(); }

    public static bool WasFound(PlayerData data, string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
#if UNITY_EDITOR
        if (Application.isPlaying && GameManager.HasEditorTestItem(id)) return true;
#endif
        if (data == null || data.foundSecrets == null) return false;
        foreach (string found in data.foundSecrets) if (found == id) return true;
        return false;
    }
}
