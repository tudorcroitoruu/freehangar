using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Equips the buried hand tools and drives short first-person barrier strikes.</summary>
[DisallowMultipleComponent]
public sealed class PlayerCombatTools : MonoBehaviour
{
    public enum Tool { Shovel = 1, Axe = 2, Hammer = 3, Broom = 4, Pistol = 5 }

    public const string PistolFindId = "farm.tool.pistol";
    public const string BroomFindId = "farm.tool.broom";
    public const string LegacyAxeFindId = "farm.tool.hammer"; // Keep the old axe discovery ID for existing saves.
    public const string AxeFindId = "farm.tool.axe";
    public const string HammerFindId = "farm.tool.sledgehammer";

    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform shovelHands;
    [SerializeField] private ShovelTool shovelTool;
    [SerializeField] private BagInteraction bags;
    [SerializeField] private Transform heldBroom;
    [SerializeField] private GameObject dustEffect;
    [FormerlySerializedAs("heldHammer"), SerializeField] private Transform heldAxe;
    [SerializeField, InspectorName("Held Hammer")] private Transform heldSledgehammer;
    [SerializeField, Min(.5f)] private float strikeReach = 3f;
    [SerializeField, Min(.1f)] private float strikeDuration = .55f;

    [SerializeField] private Transform heldPistol;
    [SerializeField] private GameObject pistolSmoke;
    [SerializeField] private AudioClip pistolShot, pistolReload;
    private const float ReloadDuration = 1.2f;
    private float lastShot = float.NegativeInfinity;
    private bool reloadSoundPending;
    private AudioSource pistolAudio;
    private Vector3 pistolRestPosition;
    private Quaternion pistolRestRotation;
    private PlayerInteractor interactor;
    private PlayerController player;
    private PlayerBicycleInteraction bicycle;
    private Tool equipped = Tool.Shovel;
    private float strikeStart = -10f;
    private bool impactPending;
    private readonly RaycastHit[] strikeHits = new RaycastHit[32];
    private float ImpactPhase => equipped == Tool.Broom ? .45f : equipped == Tool.Axe ? .42f : .5f;
    private float StrikeDuration => equipped == Tool.Broom ? .56f : strikeDuration;
    private float broomImpactTime = -10f;
    public Quaternion BroomCameraKick
    {
        get
        {
            float t = (Time.time - broomImpactTime) / .16f;
            if (t < 0f || t >= 1f) return Quaternion.identity;
            float fade = (1f-t)*(1f-t);
            return Quaternion.Euler((1.1f + Mathf.Sin(t*20f)*.25f)*fade,
                Mathf.Sin(t*27f)*.35f*fade, Mathf.Sin(t*18f)*.2f*fade);
        }
    }
    private Vector3 broomRestPosition, hammerRestPosition, sledgeRestPosition;
    private Quaternion broomRestRotation, hammerRestRotation, sledgeRestRotation;

    public Tool Equipped => equipped;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        player = GetComponent<PlayerController>();
        bicycle = GetComponent<PlayerBicycleInteraction>();
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (bags == null) bags = GetComponent<BagInteraction>();
        if (viewCamera != null)
            foreach(var node in viewCamera.GetComponentsInChildren<Transform>(true))
            {
                if(heldAxe==null && node.name=="Axe") heldAxe=node;
                if(heldBroom==null && node.name=="Broom grip pivot") heldBroom=node;
                if(heldSledgehammer==null && node.name=="Held sledgehammer") heldSledgehammer=node;
            }
        if (heldBroom != null) { broomRestPosition = heldBroom.localPosition; broomRestRotation = heldBroom.localRotation; }
        if (heldAxe != null) { hammerRestPosition = heldAxe.localPosition; hammerRestRotation = heldAxe.localRotation; }
        if (heldSledgehammer != null) { sledgeRestPosition = heldSledgehammer.localPosition; sledgeRestRotation = heldSledgehammer.localRotation; }
        if (heldPistol != null)
        {
            pistolRestPosition = heldPistol.localPosition;
            pistolRestRotation = heldPistol.localRotation;
            pistolAudio = gameObject.AddComponent<AudioSource>();
            pistolAudio.playOnAwake = false;
        }
        ApplyVisuals();
    }

    public void Tick(bool acceptInput, bool wasLocked)
    {
        if(!IsUnlocked(equipped)) { equipped=Tool.Shovel;CancelStrike();ApplyVisuals(); }
        RestoreSelectedVisuals();
        if (!acceptInput || (interactor != null && interactor.DiscoveryBusy)) { CancelStrike(); return; }
        if (equipped == Tool.Pistol && reloadSoundPending && Time.time >= lastShot + .18f)
        {
            reloadSoundPending = false;
            if (pistolReload) pistolAudio.PlayOneShot(pistolReload);
        }
        AdvanceStrike();
        if (Input.GetKeyDown(KeyCode.Alpha1)) TryEquip(Tool.Shovel);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) TryEquip(Tool.Axe);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) TryEquip(Tool.Hammer);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) TryEquip(IsUnlocked(Tool.Pistol) && (equipped == Tool.Broom || !IsUnlocked(Tool.Broom)) ? Tool.Pistol : Tool.Broom);

        if (wasLocked && Input.GetMouseButtonDown(0)) TryStartStrike();
    }

    private void AdvanceStrike()
    {
        if (impactPending && Time.time >= strikeStart + StrikeDuration * ImpactPhase)
        {
            impactPending = false;
            if (equipped == Tool.Broom) broomImpactTime = Time.time;
            PerformStrike();
        }
    }

    public bool IsUnlocked(Tool tool) => tool == Tool.Shovel ||
        (tool == Tool.Pistol && BuriedFind.WasFound(SaveSystem.Data,PistolFindId)) ||
        (tool == Tool.Broom && BuriedFind.WasFound(SaveSystem.Data,BroomFindId)) ||
        (tool == Tool.Axe && (BuriedFind.WasFound(SaveSystem.Data,AxeFindId) || BuriedFind.WasFound(SaveSystem.Data,LegacyAxeFindId))) ||
        (tool == Tool.Hammer && BuriedFind.WasFound(SaveSystem.Data,HammerFindId));

    public bool TryStartStrike()
    {
        if (!isActiveAndEnabled || !IsUnlocked(equipped) || equipped == Tool.Shovel || viewCamera == null ||
            Time.timeScale<=0 || Time.time < strikeStart + StrikeDuration ||
            (bags!=null && bags.IsCarrying) || (interactor!=null && (interactor.HandsOccupied || interactor.DiscoveryBusy))) return false;
        if (equipped == Tool.Pistol)
        {
            if (!heldPistol || Time.time < lastShot + ReloadDuration) return false;
            lastShot = Time.time;
            reloadSoundPending = true;
            if (pistolShot) pistolAudio.PlayOneShot(pistolShot);
            FirePistol();
            return true;
        }
        strikeStart=Time.time;impactPending=true;
        GameplayAudio.Instance?.ToolSwing();
        return true;
    }
    private void FirePistol()
    {
        var ray = PlayerAim.CenterRay(viewCamera);
        var muzzle = heldPistol.TransformPoint(new Vector3(0f, -.25f, .145f));
        SpawnPistolSmoke(muzzle, ray.direction);
        RaycastHit nearest = default;
        foreach (var hit in Physics.RaycastAll(ray, 150f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (nearest.collider && hit.distance >= nearest.distance)) continue;
            nearest = hit;
        }
        if (!nearest.collider) return;
        SpawnPistolSmoke(nearest.point + nearest.normal * .02f, nearest.normal);
        var chicken = nearest.collider.GetComponentInParent<FarmChicken>();
        if (chicken) chicken.Hit(chicken.health, transform.position, transform);
        var pest = nearest.collider.GetComponentInParent<GrainPest>();
        if (pest)
        {
            GameplayAudio.Instance?.PestHit(pest.IsCrow, nearest.point);
            Destroy(pest.gameObject);
        }
    }

    private void SpawnPistolSmoke(Vector3 position, Vector3 direction)
    {
        if (!pistolSmoke) return;
        var effect = Instantiate(pistolSmoke, position, Quaternion.LookRotation(direction));
        Destroy(effect, 3f);
    }

    private void CancelStrike() { impactPending=false;strikeStart=-10f;reloadSoundPending=false; }
    private void OnDisable() => CancelStrike();

    private void PerformStrike()
    {
        var ray = PlayerAim.CenterRay(viewCamera);
        int count=equipped==Tool.Broom
            ? Physics.SphereCastNonAlloc(ray,.22f,strikeHits,strikeReach,~0,QueryTriggerInteraction.Ignore)
            : Physics.RaycastNonAlloc(ray,strikeHits,strikeReach,~0,QueryTriggerInteraction.Ignore);
        if(count==strikeHits.Length) return;
        RaycastHit hit=default;bool found=false;float nearest=float.PositiveInfinity;
        for(int i=0;i<count;i++)
        {
            var candidate=strikeHits[i];
            if(candidate.transform.IsChildOf(transform) || candidate.distance>=nearest)continue;
            nearest=candidate.distance;hit=candidate;found=true;
        }
        if (!found) return;
        var chicken = hit.collider.GetComponentInParent<FarmChicken>();
        if (chicken != null && (equipped == Tool.Axe || equipped == Tool.Broom))
        {
            chicken.Hit(equipped == Tool.Axe ? 28f : 3f, transform.position, transform);
            return;
        }
        if (equipped == Tool.Broom)
        {
            var pest = hit.collider.GetComponentInParent<GrainPest>();
            if (pest != null && pest.ScaredAway(transform.position))
            {
                GameplayAudio.Instance?.PestHit(pest.IsCrow, hit.point);
                GameAchievements.BroomPestScared(pest.IsCrow);
            }
            GameplayAudio.Instance?.BroomHit();
            if (dustEffect != null) { var fx = Instantiate(dustEffect, hit.point, Quaternion.LookRotation(hit.normal)); Destroy(fx, 4f); }
            return;
        }
        BreakableGrainBarrier wall = hit.collider.GetComponentInParent<BreakableGrainBarrier>();
        if (wall != null) wall.TryHit(equipped);
    }

    // Carrying temporarily hides the shovel without changing the selected tool.
    public static bool SetShovelEquipped(PlayerController player, bool equip)
    {
        if (player == null || !player.isActiveAndEnabled || player.Shovel == null || Time.timeScale <= 0f ||
            UIPopups.BasePopupUI.HasActivePopup || UpgradeManager.BlocksGameplayInput ||
            (player.characterController != null && !player.characterController.enabled)) return false;
        var bicycle = player.GetComponent<PlayerBicycleInteraction>();
        var interactor = player.GetComponent<PlayerInteractor>();
        if ((bicycle != null && bicycle.IsRiding) || (interactor != null && interactor.PushedWheelbarrow != null)) return false;
        if (equip)
        {
            var bags = player.GetComponent<BagInteraction>();
            if (bags != null && bags.IsCarrying && !bags.TryInteract()) return false;
            if (interactor != null && interactor.Items != null && interactor.Items.IsCarrying && !interactor.Items.TryPlace()) return false;
            var tools = player.GetComponent<PlayerCombatTools>();
            if (tools != null && !tools.TryEquip(Tool.Shovel)) return false;
        }
        else player.Shovel.CancelAction();
        if (player.HeldInstrument != null) player.HeldInstrument.gameObject.SetActive(equip);
        player.Shovel.enabled = equip;
        return true;
    }

    public bool TryEquip(Tool tool)
    {
        if(!IsUnlocked(tool) || (interactor!=null && interactor.DiscoveryBusy))return false;
        if (bags != null && bags.IsCarrying && !bags.TryInteract()) return false;
        if (interactor != null && interactor.Items != null && interactor.Items.IsCarrying && !interactor.Items.TryPlace()) return false;
        if (interactor != null && interactor.PushedWheelbarrow != null) return false;
        // Showing the already-selected tool is not a tool switch and must preserve its load.
        if(tool==equipped) { ApplyVisuals();return true; }
        if (shovelTool != null && (shovelTool.Kilograms > 0f || shovelTool.IsBusy)) return false;
        if (tool != Tool.Shovel && GetHeldModel(tool) == null) return false;
        if (shovelTool != null) shovelTool.CancelAction();
        reloadSoundPending = false;
        equipped = tool;
        strikeStart = -10f;
        impactPending = false;
        ApplyVisuals();
        return true;
    }

    public void HideForPushing(System.Collections.Generic.List<GameObject> hidden)
    {
        strikeStart = -10f;
        impactPending = false;
        foreach (var tool in new[] { heldBroom, heldAxe, heldSledgehammer, heldPistol })
            if (tool != null && tool.gameObject.activeSelf) { hidden.Add(tool.gameObject); tool.gameObject.SetActive(false); }
    }

    private Transform GetHeldModel(Tool tool) => tool switch
    {
        Tool.Shovel => shovelHands,
        Tool.Axe => heldAxe,
        Tool.Hammer => heldSledgehammer,
        Tool.Broom => heldBroom,
        Tool.Pistol => heldPistol,
        _ => null
    };

    private void RestoreSelectedVisuals()
    {
        // Temporary hide owners can overlap (cargo, menus, driving). Restore from the selected
        // tool once gameplay is available, rather than relying on every owner remembering to show it.
        if (player == null || !player.isActiveAndEnabled || Time.timeScale <= 0f || LoadingScreen.IsLoading ||
            UpgradeManager.BlocksGameplayInput || UIPopups.BasePopupUI.HasActivePopup ||
            (player.characterController != null && !player.characterController.enabled) || player.IsCarrying ||
            (bicycle != null && bicycle.IsRiding) || (bags != null && bags.IsCarrying) ||
            (interactor != null && (interactor.HandsOccupied || interactor.DiscoveryBusy))) return;
        var selected = GetHeldModel(equipped);
        if ((selected != null && !selected.gameObject.activeSelf) ||
            (shovelTool != null && shovelTool.enabled != (equipped == Tool.Shovel))) ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        // All held models use the same independent depth pass as the shovel.
        // Include inactive descendants so switching tools cannot reveal world-layer meshes.
        int layer = LayerMask.NameToLayer("HeldTools");
        if (layer >= 0)
        {
            SetHeldLayer(shovelHands, layer);
            SetHeldLayer(heldBroom, layer);
            SetHeldLayer(heldPistol, layer);
            SetHeldLayer(heldAxe, layer);
            SetHeldLayer(heldSledgehammer, layer);
        }
        if (shovelTool != null) shovelTool.enabled=equipped==Tool.Shovel;
        if (shovelHands != null) shovelHands.gameObject.SetActive(equipped == Tool.Shovel);
        if (heldPistol != null) heldPistol.gameObject.SetActive(equipped == Tool.Pistol);
        if (heldBroom != null) heldBroom.gameObject.SetActive(equipped == Tool.Broom);
        if (heldAxe != null) heldAxe.gameObject.SetActive(equipped == Tool.Axe);
        if (heldSledgehammer != null) heldSledgehammer.gameObject.SetActive(equipped == Tool.Hammer);
    }

    private static void SetHeldLayer(Transform root, int layer)
    {
        if (root == null) return;
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }

    private void LateUpdate()
    {
        if (equipped == Tool.Pistol && heldPistol)
        {
            float t = Time.time - lastShot;
            float recoil = Mathf.Clamp01(1f - t / .18f);
            float lower = Mathf.SmoothStep(0f, 1f, (t - .12f) / .30f)
                * (1f - Mathf.SmoothStep(0f, 1f, (t - .80f) / (ReloadDuration - .80f)));
            heldPistol.localPosition = pistolRestPosition + new Vector3(0f, .04f * recoil - .34f * lower, -.09f * recoil);
            heldPistol.localRotation = Quaternion.Euler(-14f * recoil + 20f * lower, 0f, -8f * lower) * pistolRestRotation;
            return;
        }
        Transform held = equipped == Tool.Broom ? heldBroom : equipped == Tool.Axe ? heldAxe : equipped == Tool.Hammer ? heldSledgehammer : null;
        if (held == null) return;
        float phase = Mathf.Clamp01((Time.time - strikeStart) / StrikeDuration);
        if (equipped == Tool.Broom)
        {
            Vector3 offset, angles;
            if (phase < .30f)
            {
                float t = Mathf.SmoothStep(0,1,phase/.30f);
                offset = Vector3.Lerp(Vector3.zero, new Vector3(.015f,.07f,-.06f),t);
                angles = Vector3.Lerp(Vector3.zero,new Vector3(-48,-4,2),t);
            }
            else if (phase < .48f)
            {
                float t = Mathf.SmoothStep(0,1,(phase-.30f)/.18f);
                offset = Vector3.Lerp(new Vector3(.015f,.07f,-.06f),new Vector3(-.015f,-.08f,.12f),t);
                angles = Vector3.Lerp(new Vector3(-48,-4,2),new Vector3(52,4,-3),t);
            }
            else
            {
                float t = Mathf.SmoothStep(0,1,(phase-.48f)/.52f);
                offset = Vector3.Lerp(new Vector3(-.015f,-.08f,.12f),Vector3.zero,t);
                angles = Vector3.Lerp(new Vector3(52,4,-3),Vector3.zero,t);
            }
            held.localPosition = broomRestPosition + offset;
            held.localRotation = broomRestRotation * Quaternion.Euler(angles);
            return;
        }
        float contact=ImpactPhase;
        Vector3 restPosition=equipped==Tool.Axe?hammerRestPosition:sledgeRestPosition;
        Quaternion restRotation=equipped==Tool.Axe?hammerRestRotation:sledgeRestRotation;
        float windupEnd=contact*.65f;
        float windup=phase<windupEnd?Mathf.SmoothStep(0,1,phase/windupEnd):1;
        float impact=phase<windupEnd?0:Mathf.SmoothStep(0,1,(phase-windupEnd)/(contact-windupEnd));
        float recovery=phase<=contact?0:Mathf.SmoothStep(0,1,(phase-contact)/(1-contact));
        Vector3 pull=new Vector3(.025f,.08f,-.07f);
        Vector3 hitOffset=new Vector3(-.035f,-.12f,.2f);
        Vector3 strikeOffset=phase<windupEnd?pull*windup:Vector3.Lerp(pull,hitOffset,impact);
        Vector3 pullAngles=equipped==Tool.Axe?new Vector3(-38,-8,10):new Vector3(-58,0,7);
        Vector3 hitAngles=equipped==Tool.Axe?new Vector3(58,8,-14):new Vector3(72,0,-8);
        Vector3 strikeAngles=phase<windupEnd?pullAngles*windup:Vector3.Lerp(pullAngles,hitAngles,impact);
        held.localPosition=restPosition+Vector3.Lerp(strikeOffset,Vector3.zero,recovery);
        held.localRotation=restRotation*Quaternion.Euler(Vector3.Lerp(strikeAngles,Vector3.zero,recovery));
    }
}
