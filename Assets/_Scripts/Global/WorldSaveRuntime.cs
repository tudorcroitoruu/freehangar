using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Scene snapshot adapter owned by SaveSystem; no separate save store.</summary>
[DefaultExecutionOrder(-250)]
public sealed class WorldSaveRuntime : MonoBehaviour
{
    public static WorldSaveRuntime Instance { get; private set; }
    public static bool IsRestoring => Instance != null && Instance.restoring;
    public static bool IsSeeking => Instance != null && ((Instance.intro != null && Instance.intro.IsSeeking) ||
        (Instance.launch != null && Instance.launch.IsSeeking));
    private bool ready, restoring;
    public string DestinationScene { get; set; }
    private PlayerData profile;
    private PlayerController player;
    private GameIntroduction intro;
    private HangarLaunchSequence launch;
    private RocketEndingCutscene ending;
    private readonly Dictionary<string, GameObject> objects = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, (string productId, int limit)> inventoryCatalog = new Dictionary<string, (string, int)>();
    private readonly List<string> authoredContainers = new List<string>();
    private readonly List<WorldObjectSave> unavailableObjects = new List<WorldObjectSave>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        Instance = null;
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode)
        => EnsureScene(scene);

    public static void EnsureScene(Scene scene)
    {
        if (scene.name != "Gameplay" && scene.name != "Last Scene") return;
        if (Instance != null && Instance.gameObject.scene == scene) return;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<WorldSaveRuntime>() is WorldSaveRuntime existing) { Instance = existing; return; }
        var go = new GameObject("SaveSystem world state"); SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<WorldSaveRuntime>();
    }
    private T[] Find<T>() where T : Component => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(x => x.gameObject.scene == gameObject.scene).ToArray();
    private void Awake()
    {
        Instance = this;
        restoring = true;
        player = Find<PlayerController>().FirstOrDefault(); intro = Find<GameIntroduction>().FirstOrDefault();
        launch = Find<HangarLaunchSequence>().FirstOrDefault(); ending = Find<RocketEndingCutscene>().FirstOrDefault();
        foreach (var shop in Find<ShopProduct>())
            if (shop.ProductPrefab != null)
            {
                templates[shop.SaveProductId] = shop.ProductPrefab;
                inventoryCatalog[shop.SaveProductId] = (shop.SaveProductId, shop.MaxActiveInScene);
                var container = shop.ProductPrefab.GetComponent<GrainContainer>();
                if (container != null)
                {
                    templates[container.CargoTypeId] = shop.ProductPrefab;
                    inventoryCatalog[container.CargoTypeId] = (shop.SaveProductId, shop.MaxActiveInScene);
                }
            }
        foreach (var product in Find<InventoryProduct>())
            if (product.SavePrefab != null)
            {
                templates[product.ProductID] = product.SavePrefab;
                inventoryCatalog[product.ProductID] = (product.ProductID, product.MaxActiveInScene);
                var container = product.SavePrefab.GetComponent<GrainContainer>();
                if (container != null)
                {
                    templates[container.CargoTypeId] = product.SavePrefab;
                    inventoryCatalog[container.CargoTypeId] = (product.ProductID, product.MaxActiveInScene);
                }
            }
        foreach (var supply in Find<GrainBagSupply>()) if (supply.SavePrefab != null) templates[supply.SavePrefab.CargoTypeId] = supply.SavePrefab.gameObject;
        foreach (var pests in Find<GrainPestDirector>()) if (pests.tutorialBagTemplate != null && !templates.ContainsKey(pests.tutorialBagTemplate.CargoTypeId))
            templates[pests.tutorialBagTemplate.CargoTypeId] = pests.tutorialBagTemplate.gameObject;
        foreach (var component in Entities())
        {
            var go = component.gameObject;
            string id = "scene:" + Path(go.transform, null);
            id = Identity(go, id); objects[id] = go;
            if (component is GrainContainer || component is CarryableItem || component is ConsumableFood) authoredContainers.Add(id);
        }
    }
    private IEnumerable<Component> Entities() => Find<BicycleController>().Cast<Component>()
        .Concat(Find<WheelbarrowCargo>()).Concat(Find<GrainContainer>()).Concat(Find<GrainPile>()).Concat(Find<CarryableItem>()).Concat(Find<GrainBagSupply>())
        .Concat(Find<BreakableGrainBarrier>()).Concat(Find<ConsumableFood>());
    public static string Identity(GameObject go, string id = null)
    {
        if (go == null) return null;
        var identity = go.GetComponent<WorldSaveIdentity>() ?? go.AddComponent<WorldSaveIdentity>();
        if (string.IsNullOrEmpty(identity.Id)) identity.Id = id ?? "runtime:" + Guid.NewGuid().ToString("N");
        return identity.Id;
    }
    // Names plus same-name sibling ordinals remain stable when unrelated objects are inserted.
    public static string Path(Transform node, Transform root)
    {
        if (node == null || node == root) return "";
        int ordinal = 0;
        if (node.parent != null)
            for (int i = 0; i < node.GetSiblingIndex(); i++) if (node.parent.GetChild(i).name == node.name) ordinal++;
        return Path(node.parent, root) + "/" + Uri.EscapeDataString(node.name) + "#" + ordinal;
    }
    private static Transform Resolve(Transform root, string path, bool create = false)
    {
        foreach (string part in (path ?? "").Split('/'))
        {
            if (part.Length == 0) continue;
            int split = part.LastIndexOf('#');
            if (split <= 0 || !int.TryParse(part.Substring(split + 1), out int ordinal) || ordinal < 0) return null;
            string name = Uri.UnescapeDataString(part.Substring(0, split)); Transform next = null;
            foreach (Transform child in root) if (child.name == name && ordinal-- == 0) { next = child; break; }
            if (next == null && create) { next = new GameObject(name).transform; next.SetParent(root, false); }
            if (next == null) return null; root = next;
        }
        return root;
    }
    private IEnumerator Start()
    {
        while (!SaveSystem.savesLoaded) yield return null;
        profile = SaveSystem.Data;
        restoring = true;
        // VehicleUpgrade.Start resolves the saved model first; it transfers its stable identity.
        yield return null;
        yield return null;
        try
        {
            var saved = profile.world;
            if (saved != null && saved.scene == gameObject.scene.name) Restore(saved);
            foreach (var hangar in Find<HangarGrainVisibility>())
            {
                if (!hangar.isActiveAndEnabled || hangar.HangarOpen) continue;
                hangar.PrepareClosedPile();
                yield return null;
            }
            ready = true;
        }
        finally { restoring = false; }
    }
    public static void CaptureIntoPlayerData()
    {
        var instance = Instance;
        if (instance == null || !instance.ready || instance.restoring || instance.profile != SaveSystem.Data) return;
        SaveSystem.Data.world = instance.Capture();
    }
    private void OnApplicationQuit() { if (SaveSystem.savesLoaded) SaveSystem.SaveData(); }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public WorldSaveData Capture()
    {
        var result = new WorldSaveData { scene = gameObject.scene.name, authoredContainers = new List<string>(authoredContainers),
            introPlaying = intro != null && intro.IsPlaying, introTime = intro != null ? intro.SavedTime : 0,
            launchPlaying = launch != null && launch.Running, launchTime = launch != null ? launch.SavedTime : 0,
            launchDoorOpen = launch != null && launch.DoorOpen,
            endingPlaying = ending != null && ending.IsPlaying, endingTime = ending != null ? ending.SavedTime : 0 };
        var bags = player != null ? player.GetComponent<BagInteraction>() : null;
        var items = player != null ? player.GetComponent<PlayerItemCarry>() : null;
        if (player != null)
        {
            result.stamina = player.Stamina != null ? player.Stamina.CurrentStamina : -1;
            result.hunger = player.Stamina != null ? player.Stamina.SavedHunger : 0;
            result.equippedTool = (int)(player.GetComponent<PlayerCombatTools>()?.Equipped ?? PlayerCombatTools.Tool.Shovel);
            result.playerPosition = result.introPlaying ? intro.SavedPlayerPosition : player.transform.position;
            result.playerRotation = result.introPlaying ? intro.SavedPlayerRotation : player.transform.rotation;
            result.lookRotation = result.introPlaying ? intro.SavedLookRotation : player.characterCamera.transform.localRotation;
            if (player.Shovel != null) result.shovelCargo = Enumerable.Range(0, GrainCargo.TypeCount).Select(i => player.Shovel.Cargo[(GrainType)i]).ToArray();
            result.carriedBag = bags != null && bags.CarriedBag != null ? Identity(bags.CarriedBag.gameObject) : null;
            result.carriedItem = items != null ? Identity(items.CarriedObject) : null;
            var pushed = player.GetComponent<PlayerInteractor>()?.PushedWheelbarrow;
            if (pushed != null) result.pushedWheelbarrow = Identity(pushed.gameObject);
            var rider = player.GetComponent<PlayerBicycleInteraction>();
            if (rider != null && rider.IsRiding)
            {
                result.riddenVehicle = Identity(rider.bicycle.gameObject);
                result.ridingLook = rider.SavedLook;
                result.playerPosition = rider.bicycle.transform.position;
            }
        }
        foreach (var component in Entities().GroupBy(x => x.gameObject).Select(g => g.First()))
        {
            var go = component.gameObject;
            if (go.GetComponentInParent<SoldCargoAnimation>() != null || (go.GetComponent<VehicleUpgrade>()?.Replacement != null)) continue;
            var container = go.GetComponent<GrainContainer>(); var pile = go.GetComponent<GrainPile>();
            WorldObjectSave record = container != null ? new BagSave() : component is WheelbarrowCargo ? new WheelbarrowSave() :
                component is BicycleController ? new VehicleSave() : new WorldObjectSave();
            record.id = Identity(go); record.active = go.activeSelf;
            record.kind = container != null ? "container" : pile != null ? (pile.IsDroppedPile ? "spill" : "pile") : component is CarryableItem ? "item" : component is GrainBagSupply ? "supply" : component is BreakableGrainBarrier ? "barrier" : component is ConsumableFood ? "food" : "vehicle";
            record.inventoryLimit = go.GetComponent<InventoryItemInstance>()?.MaxActiveInScene ?? 0;
            record.prefab = go.GetComponent<InventoryItemInstance>()?.ProductID ?? container?.CargoTypeId;
            record.position = go.transform.position; record.rotation = go.transform.rotation; record.scale = go.transform.lossyScale;
            record.container = container?.CaptureSave(); record.pile = pile?.CaptureSave();
            if (record.id == result.carriedBag) record.scale = bags.SavedWorldScale;
            if (record.id == result.carriedItem) record.scale = items.SavedWorldScale;
            go.GetComponent<VehicleCargoPlacement>()?.CaptureSave(record);
            if (go.TryGetComponent<BreakableGrainBarrier>(out var barrier)) { record.barrierHits = barrier.SavedHits; record.barrierExposed = barrier.IsExposed; }
            var parent = go.transform.parent != null ? go.transform.parent.GetComponentInParent<WorldSaveIdentity>() : null;
            if (parent != null && parent.gameObject != go)
            {
                record.parent = parent.Id; record.slot = Path(go.transform.parent, parent.transform);
                record.localPosition = go.transform.localPosition; record.localRotation = go.transform.localRotation;
                record.slotPosition = go.transform.parent.localPosition; record.slotRotation = go.transform.parent.localRotation;
                record.slotScale = go.transform.parent.localScale;
            }
            if (go.TryGetComponent<Rigidbody>(out var body))
            {
                record.hasBody = true; record.gravity = body.useGravity; record.mass = body.mass;
                record.linearDamping = body.linearDamping; record.angularDamping = body.angularDamping;
                record.kinematic = body.isKinematic; record.velocity = body.linearVelocity; record.angularVelocity = body.angularVelocity;
            }
            if (record.kind == "vehicle") foreach (var child in go.GetComponentsInChildren<Rigidbody>(true))
                if (child.gameObject != go && child.GetComponent<GrainContainer>() == null)
                    record.bodies.Add(new BodySave { path = Path(child.transform, go.transform), position = child.position,
                        rotation = child.rotation, velocity = child.linearVelocity, angularVelocity = child.angularVelocity });
            if (result.introPlaying && intro.SavedBicycle != null && go == intro.SavedBicycle.gameObject)
            { record.position = intro.SavedBikePosition; record.rotation = intro.SavedBikeRotation; }
            if (record is BagSave bagRecord) result.bags.Add(bagRecord);
            else if (record is WheelbarrowSave barrowRecord) result.wheelbarrows.Add(barrowRecord);
            else if (record is VehicleSave vehicleRecord) result.vehicles.Add(vehicleRecord);
            else result.items.Add(record);
        }
        // Keep unavailable content in the save so a later scene/prefab repair can recover it.
        foreach (var record in unavailableObjects)
        {
            string json = JsonUtility.ToJson(record);
            if (record is WheelbarrowSave)
            {
                var copy = JsonUtility.FromJson<WheelbarrowSave>(json); copy.bags.Clear(); result.wheelbarrows.Add(copy);
            }
            else if (record is VehicleSave)
            {
                var copy = JsonUtility.FromJson<VehicleSave>(json); copy.bags.Clear(); result.vehicles.Add(copy);
            }
            else if (record is BagSave) result.bags.Add(JsonUtility.FromJson<BagSave>(json));
            else result.items.Add(JsonUtility.FromJson<WorldObjectSave>(json));
        }
        result.NestCargo();
        if (!string.IsNullOrEmpty(DestinationScene))
        { result.scene = DestinationScene; result.launchPlaying = false; result.endingPlaying = true; result.endingTime = 0; }
        return result;
    }

    private void Restore(WorldSaveData saved)
    {
        if (saved.version != 2) throw new InvalidOperationException("Unsupported world save version " + saved.version);
        if (ending != null && player == null) { ending.ResumeSaved(saved.endingTime); return; }
        foreach (var identity in Find<WorldSaveIdentity>()) if (!string.IsNullOrEmpty(identity.Id)) objects[identity.Id] = identity.gameObject;
        ReconnectSceneObjects(saved);
        unavailableObjects.Clear();
        var present = new HashSet<string>(saved.Records().Select(x => x.id));
        foreach (string id in saved.authoredContainers)
            if (!present.Contains(id) && objects.TryGetValue(id, out var removed) && removed != null)
            { removed.SetActive(false); if (Application.isPlaying) Destroy(removed); else DestroyImmediate(removed); objects.Remove(id); }
        var inventory = Find<InventoryManager>().FirstOrDefault();
        foreach (var record in saved.Records())
        {
            if (!objects.TryGetValue(record.id, out var go) || go == null)
            {
                if (record.kind == "spill") go = GrainPile.CreateDroppedPile(record.position, Find<GrainPile>().FirstOrDefault(p => !p.IsDroppedPile)).gameObject;
                else if (!string.IsNullOrEmpty(record.prefab) && templates.TryGetValue(record.prefab, out var prefab)) go = Instantiate(prefab);
                else
                {
                    unavailableObjects.Add(record);
                    Debug.LogWarning("Saved object is unavailable: " + record.id + ". Continuing load; its record will be retained.");
                    continue;
                }
                Identity(go, record.id); objects[record.id] = go;
                SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            }
            if (record.kind == "container" || record.kind == "item") go.transform.SetParent(null, true);
            SetPose(go.transform, record.position, record.rotation, record.scale);
            go.GetComponent<GrainContainer>()?.RestoreSave(record.container);
            go.GetComponent<GrainPile>()?.RestoreSave(record.pile);
            var barrier = go.GetComponent<BreakableGrainBarrier>();
            go.SetActive(barrier != null ? barrier.RestoreSave(record.barrierHits, record.barrierExposed, record.active) : record.active);
            if (inventory != null && (record is BagSave || record.kind == "container" || record.kind == "item"))
            {
                string productId = !string.IsNullOrWhiteSpace(record.prefab) ? record.prefab : go.GetComponent<GrainContainer>()?.CargoTypeId;
                int limit = record.inventoryLimit;
                if (productId != null && inventoryCatalog.TryGetValue(productId, out var product) && productId != product.productId)
                {
                    productId = product.productId;
                    limit = product.limit;
                }
                if (inventory.RegisterRestoredInstance(go, productId, limit) == null)
                    Debug.LogWarning("Restored object has no inventory product: " + record.id + ". Its world state was restored.");
            }
            if (record.hasBody && go.GetComponent<Rigidbody>() == null) go.AddComponent<Rigidbody>();
            if (go.TryGetComponent<Rigidbody>(out var body))
            {
                body.isKinematic = record.kinematic;
                if (record.hasBody) { body.useGravity = record.gravity; body.mass = Mathf.Max(.001f,record.mass); body.linearDamping = record.linearDamping; body.angularDamping = record.angularDamping; }
                if (!body.isKinematic) { body.linearVelocity = record.velocity; body.angularVelocity = record.angularVelocity; }
            }
            foreach (var child in record.bodies)
            {
                var target = Resolve(go.transform, child.path); if (target == null) continue;
                target.SetPositionAndRotation(child.position, child.rotation);
                if (target.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic) { rb.linearVelocity = child.velocity; rb.angularVelocity = child.angularVelocity; }
            }
            go.GetComponent<VehicleReset>()?.SetHomePose();
        }
        foreach (var record in saved.Records())
        {
            if (string.IsNullOrEmpty(record.parent) || !objects.TryGetValue(record.id, out var go) || go == null) continue;
            objects.TryGetValue(record.parent, out var parent);
            var slot = parent != null ? Resolve(parent.transform, record.slot, parent.GetComponent<WheelbarrowCargo>() != null) : null;
            if (slot == null)
            {
                go.transform.SetParent(null, true);
                SetPose(go.transform, record.position, record.rotation, record.scale);
                if (go.TryGetComponent<GrainContainer>(out _) && go.TryGetComponent<Rigidbody>(out var looseBody))
                { looseBody.isKinematic = false; looseBody.useGravity = true; }
                Debug.LogWarning("Saved cargo support is unavailable for " + record.id + ". Restored at its saved world position.");
                continue;
            }
            if (parent.GetComponent<WheelbarrowCargo>() != null)
            { slot.localPosition = record.slotPosition; slot.localRotation = record.slotRotation; slot.localScale = record.slotScale; }
            go.transform.SetParent(slot, true); go.transform.localPosition = record.localPosition; go.transform.localRotation = record.localRotation;
            if (go.TryGetComponent<GrainContainer>(out var cargo)) parent.GetComponent<WheelbarrowCargo>()?.RestoreLoadedBag(cargo, slot);
            VehicleCargoPlacement.RestoreSave(go.transform, record);
            if (cargo != null) parent.GetComponent<ForkliftController>()?.RestoreLoadedBag(cargo);
            if (cargo != null) parent.GetComponent<GrainBagSupply>()?.RestoreCurrentBag(cargo);
        }
        if (player != null)
        {
            bool controllerEnabled = player.characterController.enabled; player.characterController.enabled = false;
            player.transform.SetPositionAndRotation(saved.playerPosition, saved.playerRotation);
            player.characterCamera.transform.localRotation = saved.lookRotation; player.ResetLook();
            player.characterController.enabled = controllerEnabled;
            player.Stamina?.RestoreSave(saved.stamina, saved.hunger);
            player.GetComponent<PlayerCombatTools>()?.TryEquip((PlayerCombatTools.Tool)saved.equippedTool);
            if (player.Shovel != null && saved.shovelCargo != null)
            { player.Shovel.Cargo.Clear(); for (int i = 0; i < saved.shovelCargo.Length && i < GrainCargo.TypeCount; i++) player.Shovel.Cargo.Add((GrainType)i, saved.shovelCargo[i]); }
            if (!string.IsNullOrEmpty(saved.carriedBag) && objects.TryGetValue(saved.carriedBag, out var bag)) player.GetComponent<BagInteraction>()?.RestoreCarried(bag.GetComponent<GrainContainer>());
            if (!string.IsNullOrEmpty(saved.carriedItem) && objects.TryGetValue(saved.carriedItem, out var item)) player.GetComponent<PlayerItemCarry>()?.TryTake(item);
            if (!string.IsNullOrEmpty(saved.riddenVehicle) && objects.TryGetValue(saved.riddenVehicle, out var vehicle))
            { var rider = player.GetComponent<PlayerBicycleInteraction>(); if (rider != null) { rider.bicycle = vehicle.GetComponent<BicycleController>(); if (rider.TryMount()) rider.RestoreSavedLook(saved.ridingLook); } }
            if (!string.IsNullOrEmpty(saved.pushedWheelbarrow) && objects.TryGetValue(saved.pushedWheelbarrow, out var barrow))
                barrow.GetComponent<WheelbarrowCargo>()?.TryInteract(player.GetComponent<PlayerInteractor>());
        }
        Physics.SyncTransforms();
        if (saved.endingPlaying && ending != null) ending.ResumeSaved(saved.endingTime);
        launch?.RestoreDoor(saved.launchDoorOpen);
        if (saved.launchPlaying && launch != null && player != null) launch.ResumeSaved(player.GetComponent<PlayerInteractor>(), saved.launchTime);
    }
    private void ReconnectSceneObjects(WorldSaveData saved)
    {
        var ids = saved.Records().Select(x => x.id).Concat(saved.authoredContainers)
            .Where(id => !string.IsNullOrEmpty(id) && id.StartsWith("scene:", StringComparison.Ordinal)).Distinct().ToArray();
        var claimed = new HashSet<GameObject>(ids.Where(id => objects.TryGetValue(id, out var go) && go != null).Select(id => objects[id]));
        foreach (string id in ids)
        {
            if (objects.TryGetValue(id, out var existing) && existing != null) continue;
            // Compare from the object upward: adding/removing a scene wrapper must not invalidate saves.
            var matches = objects.Where(pair => pair.Value != null && !claimed.Contains(pair.Value))
                .Select(pair => new { pair.Value, Score = MatchingPathSuffix(id, pair.Key) })
                .Where(match => match.Score > 0).OrderByDescending(match => match.Score).ToArray();
            if (matches.Length == 0 || (matches.Length > 1 && matches[0].Score == matches[1].Score)) continue;
            var match = matches[0];
            string candidateId = match.Value.GetComponent<WorldSaveIdentity>().Id;
            if (ids.Any(other => other != id && (!objects.TryGetValue(other, out var otherObject) || otherObject == null) &&
                MatchingPathSuffix(other, candidateId) >= match.Score)) continue;
            objects[id] = match.Value;
            match.Value.GetComponent<WorldSaveIdentity>().Id = id;
            claimed.Add(match.Value);
        }
        // Capture removals using the same identities as the migrated objects.
        for (int i = 0; i < authoredContainers.Count; i++)
            if (objects.TryGetValue(authoredContainers[i], out var go) && go != null)
                authoredContainers[i] = go.GetComponent<WorldSaveIdentity>().Id;
    }
    private static int MatchingPathSuffix(string first, string second)
    {
        if (!second.StartsWith("scene:", StringComparison.Ordinal)) return 0;
        var a = first.Substring(6).Split('/'); var b = second.Substring(6).Split('/');
        int count = 0;
        while (count < a.Length && count < b.Length && a[a.Length - 1 - count] == b[b.Length - 1 - count]) count++;
        return count;
    }
    private static void SetPose(Transform target, Vector3 position, Quaternion rotation, Vector3 worldScale)
    {
        target.SetPositionAndRotation(position, rotation);
        var parent = target.parent != null ? target.parent.lossyScale : Vector3.one;
        target.localScale = new Vector3(worldScale.x / parent.x, worldScale.y / parent.y, worldScale.z / parent.z);
    }
    public bool Ready => ready;
}
