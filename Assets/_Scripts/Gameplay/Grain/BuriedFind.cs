using System;
using System.Collections;
using UnityEngine;

/// <summary>A key or cash chest becomes interactive when the grain above it has been removed.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
public sealed class BuriedFind : MonoBehaviour, IInteractable
{
    public static event Action<BuriedFind, PlayerData> Collected;
    // Explicit values preserve existing scene, prefab and save assignments when renaming tools.
    public enum Kind { Key = 0, CashChest = 1, UpgradeVoucher = 2, InfiniteStamina = 3, CashCache = 4, Axe = 5, Hammer = 6, HangarKey = 7, Broom = 8, VehicleKey = 9, PerkBottle = 10, Pistol = 11 }

    [SerializeField] private Kind kind;
    [SerializeField] private string findId;
    [SerializeField] private string vehicleKeyName;
    [SerializeField] private string chestKeyLabel;
    public string RequiredChestKeyId => requiredKeyId;
    public string ChestKeyHint => string.IsNullOrEmpty(matchingKeyColor)
        ? "Requires a key for this chest."
        : $"Requires any {matchingKeyColor.ToLowerInvariant()} key.";
    private static readonly string[] ChestKeyFamilies = { "farm.key.iron", "farm.key.copper" };
    public static bool HasChestKey(PlayerData data, string requiredId) => FindChestKey(data, requiredId) != null;
    private static string FindChestKey(PlayerData data, string requiredId)
    {
        if (data?.foundSecrets == null || string.IsNullOrEmpty(requiredId)) return null;
        if (WasFound(data, requiredId) && !WasFound(data, "consumed.chest.key:" + requiredId)) return requiredId;
        foreach (string family in ChestKeyFamilies)
        {
            if (requiredId != family && !requiredId.StartsWith(family + ".", StringComparison.Ordinal)) continue;
            foreach (string ownedId in data.foundSecrets)
                if ((ownedId == family || (ownedId != null && ownedId.StartsWith(family + ".", StringComparison.Ordinal))) &&
                    !WasFound(data, "consumed.chest.key:" + ownedId))
                    return ownedId;
        }
        return null;
    }
    public static bool TryConsumeChestKey(PlayerData data, string requiredId)
    {
        string key = FindChestKey(data, requiredId);
        if (key == null) return false;
        // Keep collection history so a spent key does not respawn when loading the save.
        int index = data.foundSecrets.Length;
        Array.Resize(ref data.foundSecrets, index + 1);
        data.foundSecrets[index] = "consumed.chest.key:" + key;
        return true;
    }
    public void ConfigureChestKey(string id,string label) { kind=Kind.Key;findId=id;chestKeyLabel=label; }
    public void ConfigureMatchingChest(string id,string keyId,string label,GrainPile pile,Material keyMaterial)
    {
        kind=Kind.CashChest;findId=id;requiredKeyId=keyId;matchingKeyColor=label;
        coveringPile=pile;matchingHangarKeyId="";matchingKeyMaterial=keyMaterial;settleOnReveal=false;
        variedChestRewards=true;chestPerkSeconds=PlayerPerks.DefaultDurationSeconds;
    }
    public void ConfigureVehicleKey(string id, string displayName, GrainPile pile)
    {
        kind = Kind.VehicleKey; findId = id; vehicleKeyName = displayName; coveringPile = pile;
    }
    public void ConfigurePerkBottle(string id, ChestReward perk, GrainPile pile)
    {
        kind=Kind.PerkBottle;findId=id;bottlePerk=perk;coveringPile=pile;
        staminaDurationSeconds=PlayerPerks.DefaultDurationSeconds;settleOnReveal=false;
    }
    [SerializeField] private ChestReward bottlePerk = ChestReward.InfiniteStamina;
    private bool IsBottle => kind==Kind.InfiniteStamina || kind==Kind.PerkBottle;
    private ChestReward BottleReward => kind==Kind.InfiniteStamina ? ChestReward.InfiniteStamina : bottlePerk;
    private string BottleName => BottleReward==ChestReward.Mobility ? "mobility" : BottleReward==ChestReward.NoAttacks ? "protection" : BottleReward==ChestReward.DoubleSellCash ? "double sell cash" : "stamina";
    [SerializeField] private string requiredKeyId;
    [SerializeField] private string matchingHangarKeyId;
    [SerializeField] private string matchingKeyColor;
    [SerializeField] private Material matchingKeyMaterial;
    [SerializeField, Min(1)] private int cashReward = 500;
    [SerializeField] private bool variedChestRewards = true;
    [SerializeField, Min(1)] private int chestPerkSeconds = PlayerPerks.DefaultDurationSeconds;
    private bool chestRewardRolled;
    private ChestReward rolledChestReward;
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
    [SerializeField] private BreakableGrainBarrier[] enclosingPanels;
    private BoxCollider interactionCollider;
    private Renderer[] visuals;
    private Renderer hangarKeyVisual;
    private float nextRevealCheck;
    private readonly RaycastHit[] supportHits = new RaycastHit[24];
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
    private bool drinkingCamera;
    private Vector3 drinkCameraPosition;
    private Quaternion drinkCameraRotation;
    private AudioSource drinkingAudio;
    private static bool profileLoadRequested;

    public string FindId => findId;
    public bool IsRevealed => revealed;
    public Kind FindKind => kind;
    public int TargetHangar => targetHangar;
    public bool IsAnimating => opening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProfileRequest()
    {
        profileLoadRequested = false;
        Collected = null;
    }

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
        FitHangarKeyCollider();
        // A moved map pickup must not wait for an unrelated, closed hangar.
        if (coveringPile != null)
        {
            Vector3 position = hangarKeyVisual != null ? hangarKeyVisual.bounds.center : transform.position;
            Vector3 local = coveringPile.transform.InverseTransformPoint(position);
            if (Mathf.Abs(local.x) > coveringPile.Size * .5f || Mathf.Abs(local.z) > coveringPile.Size * .5f)
                coveringPile = null;
        }
        interactionCollider.isTrigger = false;
        SetRevealed(false);
    }

    private void FitHangarKeyCollider()
    {
        if (kind != Kind.HangarKey) return;
        foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name != "Combined key mesh" || filter.sharedMesh == null) continue;
            hangarKeyVisual = filter.GetComponent<Renderer>();
            Bounds meshBounds = filter.sharedMesh.bounds;
            Bounds bounds = default;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = meshBounds.center + Vector3.Scale(meshBounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 local = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (i == 0) bounds = new Bounds(local, Vector3.zero); else bounds.Encapsulate(local);
            }
            Vector3 scale = transform.lossyScale;
            bounds.Expand(new Vector3(.04f / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                .04f / Mathf.Max(.001f, Mathf.Abs(scale.y)), .04f / Mathf.Max(.001f, Mathf.Abs(scale.z))));
            interactionCollider.center = bounds.center;
            interactionCollider.size = bounds.size;
            break;
        }
    }

    private void Update()
    {
        if (opening) return;
        if (WasFound(SaveSystem.Data, findId)) { gameObject.SetActive(false); return; }
        if (Time.time < nextRevealCheck) return;
        nextRevealCheck = Time.time + .3f;
        if (revealed)
        {
            if (kind == Kind.CashChest) CheckChestSupport();
            return;
        }
        if (kind == Kind.Pistol && (!WasFound(SaveSystem.Data, PlayerCombatTools.HammerFindId) ||
            enclosingPanels == null || !Array.Exists(enclosingPanels, panel => panel && panel.IsBroken))) return;
        if (coveringPile == null) { SetRevealed(true); return; }
        // Closed hangars keep their grain disabled until opened. That is not an excavation.
        if (!coveringPile.isActiveAndEnabled) return;
        Bounds bounds = VisualBounds();
        float surface = coveringPile.SurfaceHeight(bounds.center);
        if (surface <= bounds.max.y + .08f) SetRevealed(true);
    }

    private void CheckChestSupport()
    {
        if (coveringPile != null && !coveringPile.isActiveAndEnabled) return;
        Bounds bounds = interactionCollider.bounds;
        // Check the footprint, not just its center: a partly excavated chest can
        // still rest on the remaining edge of the pile.
        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
        {
            Vector3 point = new Vector3(bounds.center.x + bounds.extents.x * .85f * x,
                bounds.min.y, bounds.center.z + bounds.extents.z * .85f * z);
            if (coveringPile != null && coveringPile.IsInsideBoundary(point) &&
                coveringPile.SurfaceHeight(point) >= bounds.min.y - .08f) return;
        }
        int count = Physics.BoxCastNonAlloc(new Vector3(bounds.center.x, bounds.min.y + .04f, bounds.center.z),
            new Vector3(bounds.extents.x * .85f, .025f, bounds.extents.z * .85f),
            Vector3.down, supportHits, Quaternion.identity, .15f, ~0, QueryTriggerInteraction.Ignore);
        if (count == supportHits.Length) return;
        for (int i = 0; i < count; i++)
        {
            var hit = supportHits[i];
            if (hit.transform.IsChildOf(transform)) continue;
            var pile = hit.collider.GetComponentInParent<GrainPile>();
            // Grain colliders refresh less frequently than excavation. Do not
            // keep a chest floating on an old collider after its stock is removed.
            if (pile != null && pile.SurfaceHeight(hit.point) < bounds.min.y - .08f) continue;
            return;
        }
        if (body == null) body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.useGravity = true;
        body.isKinematic = false;
        body.WakeUp();
    }

    private Bounds VisualBounds()
    {
        if (hangarKeyVisual != null) return hangarKeyVisual.bounds;
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
        bool hasKey = kind != Kind.CashChest || HasChestKey(SaveSystem.Data, requiredKeyId) || HasChestKey(SaveSystem.Data, matchingHangarKeyId);
        string action = !hasProfile ? "Load save to collect" : !hasKey ? "" :
            kind == Kind.VehicleKey ? $"Take {vehicleKeyName} key" :
            kind == Kind.Key ? $"Take {(string.IsNullOrEmpty(chestKeyLabel) ? "chest" : chestKeyLabel)} key" :
            kind == Kind.HangarKey ? $"Take HANGAR {targetHangar} key" :
            kind == Kind.Pistol ? $"Press {FarmInput.KeyLabel(KeyCode.E)} to pick up the pistol" :
            kind == Kind.Broom ? $"Take broom (equip with {FarmInput.KeyLabel(KeyCode.Alpha4)})" :
            kind == Kind.Axe ? $"Take axe (equip with {FarmInput.KeyLabel(KeyCode.Alpha2)})" :
            kind == Kind.Hammer ? $"Take hammer (equip with {FarmInput.KeyLabel(KeyCode.Alpha3)})" :
            kind == Kind.UpgradeVoucher ? "Pick up free upgrade" :
            IsBottle ? $"Drink {BottleName} tonic ({staminaDurationSeconds / 60} min)" :
            kind == Kind.CashCache ? $"Take cash cache (+${cashReward})" :
            kind == Kind.CashChest ? (string.IsNullOrEmpty(matchingKeyColor) ? "Open chest" : $"Open {matchingKeyColor.ToLowerInvariant()} chest") : "Pick up perk";
        Bounds bounds = VisualBounds();
        prompt = new InteractionPromptData(new Vector3(bounds.center.x, bounds.max.y + .15f, bounds.center.z),
            action, hasProfile && hasKey);
        prompt.InteractionKey = KeyCode.E;
        prompt.NeedKeyForChest = hasProfile && !hasKey;
        if (interactor != null && (kind == Kind.CashChest || IsBottle))
            prompt.RequiresFreeHands = (interactor.Bags != null && interactor.Bags.IsCarrying) ||
                (interactor.Items != null && interactor.Items.IsCarrying) || interactor.PushedWheelbarrow != null;
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out var prompt) || !prompt.ShowKey || prompt.RequiresFreeHands || Time.timeScale <= 0) return false;
        if (kind == Kind.CashChest || IsBottle)
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
        GameplayAudio.Instance?.PerkFound();
        DiscoveryBurst.Play(pickupEffect, VisualBounds().center, Receipt(), interactor != null ? interactor.GetComponentInChildren<Camera>() : null);
        gameObject.SetActive(false);
        return true;
    }

    private bool CommitReward()
    {
        PlayerData data = SaveSystem.Data;
        if (data == null || WasFound(data, findId)) return false;
        string chestKey = null;
        if (kind == Kind.CashChest)
        {
            chestKey = FindChestKey(data, requiredKeyId) ?? FindChestKey(data, matchingHangarKeyId);
            if (chestKey == null) return false;
        }
        if (kind == Kind.CashChest && variedChestRewards)
        {
            if(!chestRewardRolled) { rolledChestReward=PlayerPerks.Roll(UnityEngine.Random.value);chestRewardRolled=true; }
            if(!PlayerPerks.Grant(data,rolledChestReward,cashReward,chestPerkSeconds,PlayerPerks.Now))return false;
        }
        else if (kind == Kind.CashChest || kind == Kind.CashCache)
        {
            if (cashReward <= 0 || data.cash > int.MaxValue - cashReward) return false;
            data.cash += cashReward;
        }
        else if (kind == Kind.UpgradeVoucher)
        {
            if (data.freeUpgradeVouchers == int.MaxValue) return false;
            data.freeUpgradeVouchers++;
        }
        else if (IsBottle)
        {
            if(!PlayerPerks.Grant(data,BottleReward,0,staminaDurationSeconds,PlayerPerks.Now))return false;
        }
        if (kind == Kind.CashChest) TryConsumeChestKey(data, chestKey);
        if (data.foundSecrets == null) data.foundSecrets = Array.Empty<string>();
        int index = data.foundSecrets.Length;
        Array.Resize(ref data.foundSecrets, index + 1);
        data.foundSecrets[index] = findId;
        SaveReward();
        Collected?.Invoke(this, data);
        if (kind == Kind.HangarKey) GoalChecker.ReportValue(GoalType.HangarKeyCollected, 1, targetHangar);
        return true;
    }

    private async void SaveReward()
    {
        // Boxes and bottles share the goal-reward save path: capture Unity state
        // now, then serialize/compress its isolated snapshot off the gameplay thread.
        // The receipt, audio and perk must not wait for a large grain-world save.
        try { await SaveSystem.SaveGoalCompletionAsync(); }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            // Preserve the earned reward if background serialization fails.
            SaveSystem.SaveData();
        }
    }

    private string Receipt() => kind == Kind.CashChest && variedChestRewards && chestRewardRolled ? PlayerPerks.Receipt(rolledChestReward,cashReward,chestPerkSeconds) : kind == Kind.VehicleKey ? $"{vehicleKeyName.ToUpperInvariant()} KEY FOUND" : kind == Kind.HangarKey ? $"HANGAR {targetHangar} KEY\nUse at Hangar {targetHangar}" :
        kind == Kind.Pistol ? $"PISTOL FOUND\nPress {FarmInput.KeyLabel(KeyCode.Alpha4)} to switch" :
        kind == Kind.Broom ? $"BROOM FOUND\nPress {FarmInput.KeyLabel(KeyCode.Alpha4)} to equip" : kind == Kind.Key ? $"{(string.IsNullOrEmpty(chestKeyLabel) ? "CHEST" : chestKeyLabel.ToUpperInvariant())} KEY FOUND" : IsBottle ? PlayerPerks.Receipt(BottleReward,0,staminaDurationSeconds) :
        kind == Kind.CashChest || kind == Kind.CashCache ? $"+${cashReward}" :
        kind == Kind.UpgradeVoucher ? "FREE UPGRADE!" : kind == Kind.Axe ? $"AXE FOUND\nPress {FarmInput.KeyLabel(KeyCode.Alpha2)} to equip" : $"HAMMER FOUND\nPress {FarmInput.KeyLabel(KeyCode.Alpha3)} to equip";

    private bool PresentationValid => actor != null && actor.isActiveAndEnabled && view != null &&
        view.isActiveAndEnabled && SaveSystem.Data == animationProfile;

    private IEnumerator OpenChest()
    {
        // Capture the closed chest's center before the lid changes its visual bounds.
        Vector3 effectOrigin = VisualBounds().center;
        if (keySocket != null && insertionKeyPrefab != null)
        {
            Quaternion facing = Quaternion.LookRotation(-keySocket.forward, keySocket.up);
            insertionKey = Instantiate(insertionKeyPrefab, keySocket.position + keySocket.forward * .45f, facing);
            if (matchingKeyMaterial != null)
                foreach (var renderer in insertionKey.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = matchingKeyMaterial;
            float keyTime = 0;
            while (keyTime < 1.05f)
            {
                if (!PresentationValid) { CancelPresentation(); yield break; }
                keyTime += Time.deltaTime;
                // A small unlocking rattle settles before the lid lifts.
                float rattle = Mathf.Clamp01((keyTime - .65f) / .15f) * (1f - Mathf.Clamp01((keyTime - .9f) / .15f));
                transform.position = originalPosition + transform.right * (Mathf.Sin(keyTime * 65f) * .012f * rattle);
                float slide = Mathf.SmoothStep(0, 1, keyTime / .6f);
                insertionKey.transform.position = keySocket.position + keySocket.forward * Mathf.Lerp(.45f, .015f, slide);
                insertionKey.transform.rotation = facing * Quaternion.Euler(0, 0, Mathf.SmoothStep(0, 90, (keyTime - .65f) / .4f));
                yield return null;
            }
        }
        transform.position = originalPosition;
        float elapsed = 0f;
        while (elapsed < .85f)
        {
            if (!PresentationValid) { CancelPresentation(); yield break; }
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / .85f);
            if (lidPivot != null) lidPivot.localRotation = closedLid * Quaternion.Euler(lidOpenDegrees * t, 0, 0);
            if (!committed && elapsed >= .52f)
            {
                if (!CommitReward()) { CancelPresentation(); yield break; }
                committed = true;
                GameplayAudio.Instance?.PerkFound();
                DiscoveryBurst.Play(pickupEffect, effectOrigin, Receipt(), view);
            }
            yield return null;
        }
        ReleaseActor();
        yield return new WaitForSeconds(1.2f);
        gameObject.SetActive(false);
    }

    private IEnumerator Drink()
    {
        drinkingCamera = true;
        drinkCameraPosition = view.transform.localPosition;
        drinkCameraRotation = view.transform.localRotation;
        // Bottle models are upright along local Y; place their neck at the mouth.
        Bounds bottleBounds = new Bounds();
        bool first = true;
        foreach (var mesh in GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null) continue;
            var b = mesh.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var point = transform.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                if(first) { bottleBounds = new Bounds(point,Vector3.zero); first=false; } else bottleBounds.Encapsulate(point);
            }
        }
        Vector3 neck = new Vector3(bottleBounds.center.x,bottleBounds.max.y,bottleBounds.center.z);
        float finish = .65f + (GameplayAudio.Instance != null ? GameplayAudio.Instance.BottleDrinkDuration : 1.9f);
        bool soundStarted = false;
        float elapsed = 0;
        while (elapsed < finish + .45f)
        {
            if (!PresentationValid) { CancelPresentation(); yield break; }
            elapsed += Time.deltaTime;
            float lift = Mathf.SmoothStep(0, 1, elapsed / .55f);
            float tilt = Mathf.SmoothStep(0, 1, (elapsed - .5f) / .45f);
            float lower = Mathf.SmoothStep(0, 1, (elapsed - finish) / .45f);
            float sip = tilt * (1-lower);
            view.transform.localPosition = drinkCameraPosition + Vector3.up * (.025f*sip);
            view.transform.localRotation = drinkCameraRotation * Quaternion.Euler(-8f*sip,0,0);
            transform.localScale = originalScale * (1 - lower);
            transform.rotation = Quaternion.Slerp(originalRotation, view.transform.rotation * Quaternion.Euler(-110f*tilt,0,0),lift);
            Vector3 mouth = view.transform.TransformPoint(new Vector3(0,-.14f,.18f));
            Vector3 hold = Vector3.Lerp(view.transform.TransformPoint(new Vector3(.04f,-.24f,.55f)), mouth-transform.TransformVector(neck),tilt);
            transform.position = Vector3.Lerp(originalPosition,hold,lift)-view.transform.up*lower*.35f;
            if (!soundStarted && elapsed >= .65f)
            {
                drinkingAudio = GameplayAudio.Instance?.StartBottleDrink();
                soundStarted = true;
            }
            if (!committed && elapsed >= finish)
            {
                if (!CommitReward()) { CancelPresentation(); yield break; }
                committed = true;
                GameplayAudio.Instance?.PerkFound();
                DiscoveryBurst.Play(pickupEffect, view.transform.position + view.transform.forward * 1.1f, Receipt(), view);
            }
            yield return null;
        }
        gameObject.SetActive(false);
    }

    private void ReleaseActor()
    {
        if (drinkingCamera && view != null)
        {
            view.transform.localPosition = drinkCameraPosition;
            view.transform.localRotation = drinkCameraRotation;
        }
        drinkingCamera = false;
        if (drinkingAudio != null) { drinkingAudio.Stop(); Destroy(drinkingAudio); }
        drinkingAudio = null;
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
