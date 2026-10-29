using UnityEngine;

/// <summary>Equips the buried hand tools and drives short first-person barrier strikes.</summary>
[DisallowMultipleComponent]
public sealed class PlayerCombatTools : MonoBehaviour
{
    public enum Tool { Shovel = 1, Hammer = 2, Sledgehammer = 3, Broom = 4 }

    public const string BroomFindId = "farm.tool.broom";
    public const string HammerFindId = "farm.tool.hammer";
    public const string SledgehammerFindId = "farm.tool.sledgehammer";

    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform shovelHands;
    [SerializeField] private ShovelTool shovelTool;
    [SerializeField] private BagInteraction bags;
    [SerializeField] private Transform heldBroom;
    [SerializeField] private GameObject dustEffect;
    [SerializeField] private Transform heldHammer;
    [SerializeField] private Transform heldSledgehammer;
    [SerializeField, Min(.5f)] private float strikeReach = 3f;
    [SerializeField, Min(.1f)] private float strikeDuration = .55f;

    private PlayerInteractor interactor;
    private Tool equipped = Tool.Shovel;
    private float strikeStart = -10f;
    private bool broomImpactPending;
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
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (bags == null) bags = GetComponent<BagInteraction>();
        if (heldBroom != null) { broomRestPosition = heldBroom.localPosition; broomRestRotation = heldBroom.localRotation; }
        if (heldHammer != null) { hammerRestPosition = heldHammer.localPosition; hammerRestRotation = heldHammer.localRotation; }
        if (heldSledgehammer != null) { sledgeRestPosition = heldSledgehammer.localPosition; sledgeRestRotation = heldSledgehammer.localRotation; }
        ApplyVisuals();
    }

    public void Tick(bool acceptInput, bool wasLocked)
    {
        if (!acceptInput) { broomImpactPending = false; return; }
        if (broomImpactPending && equipped == Tool.Broom && Time.time >= strikeStart + StrikeDuration * .45f)
        {
            broomImpactPending = false;
            broomImpactTime = Time.time;
            PerformStrike();
        }
        if (Input.GetKeyDown(KeyCode.Alpha1)) TryEquip(Tool.Shovel);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) TryEquip(Tool.Broom);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) TryEquip(Tool.Hammer);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) TryEquip(Tool.Sledgehammer);

        if (equipped == Tool.Shovel || !wasLocked || !Input.GetMouseButtonDown(0) ||
            Time.time < strikeStart + StrikeDuration || viewCamera == null ||
            (bags != null && bags.IsCarrying) || (interactor != null && interactor.PushedWheelbarrow != null)) return;

        strikeStart = Time.time;
        if (equipped == Tool.Broom) { broomImpactPending = true; return; }
        PerformStrike();
    }

    private void PerformStrike()
    {
        var ray = PlayerAim.CenterRay(viewCamera);
        var hits = equipped == Tool.Broom
            ? Physics.SphereCastAll(ray, .22f, strikeReach, ~0, QueryTriggerInteraction.Ignore)
            : Physics.RaycastAll(ray, strikeReach, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
        RaycastHit hit = default;
        bool found = false;
        foreach (var candidate in hits)
        {
            if (candidate.transform.IsChildOf(transform)) continue;
            hit = candidate; found = true; break;
        }
        if (!found) return;
        if (equipped == Tool.Broom)
        {
            var pest = hit.collider.GetComponentInParent<GrainPest>();
            if (pest != null) pest.ScaredAway(transform.position);
            if (dustEffect != null) { var fx = Instantiate(dustEffect, hit.point, Quaternion.LookRotation(hit.normal)); Destroy(fx, 4f); }
            return;
        }
        BreakableGrainBarrier wall = hit.collider.GetComponentInParent<BreakableGrainBarrier>();
        if (wall != null) wall.TryHit(equipped);
    }

    public bool TryEquip(Tool tool)
    {
        if (tool == equipped) return true;
        if (tool == Tool.Broom && !BuriedFind.WasFound(SaveSystem.Data, BroomFindId)) return false;
        if (tool == Tool.Hammer && !BuriedFind.WasFound(SaveSystem.Data, HammerFindId)) return false;
        if (tool == Tool.Sledgehammer && !BuriedFind.WasFound(SaveSystem.Data, SledgehammerFindId)) return false;
        if (bags != null && bags.IsCarrying && !bags.TryInteract()) return false;
        if (interactor != null && interactor.Items != null && interactor.Items.IsCarrying && !interactor.Items.TryPlace()) return false;
        if (interactor != null && interactor.PushedWheelbarrow != null) return false;
        if (shovelTool != null && (shovelTool.Kilograms > 0f || shovelTool.IsBusy)) return false;
        if (tool != Tool.Shovel && (tool == Tool.Broom ? heldBroom : tool == Tool.Hammer ? heldHammer : heldSledgehammer) == null) return false;
        if (shovelTool != null) shovelTool.CancelAction();
        equipped = tool;
        strikeStart = -10f;
        broomImpactPending = false;
        ApplyVisuals();
        return true;
    }

    public void HideForPushing(System.Collections.Generic.List<GameObject> hidden)
    {
        strikeStart = -10f;
        broomImpactPending = false;
        foreach (var tool in new[] { heldBroom, heldHammer, heldSledgehammer })
            if (tool != null && tool.gameObject.activeSelf) { hidden.Add(tool.gameObject); tool.gameObject.SetActive(false); }
    }

    private void ApplyVisuals()
    {
        if (shovelHands != null) shovelHands.gameObject.SetActive(equipped == Tool.Shovel);
        if (heldBroom != null) heldBroom.gameObject.SetActive(equipped == Tool.Broom);
        if (heldHammer != null) heldHammer.gameObject.SetActive(equipped == Tool.Hammer);
        if (heldSledgehammer != null) heldSledgehammer.gameObject.SetActive(equipped == Tool.Sledgehammer);
    }

    private void LateUpdate()
    {
        Transform held = equipped == Tool.Broom ? heldBroom : equipped == Tool.Hammer ? heldHammer : equipped == Tool.Sledgehammer ? heldSledgehammer : null;
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
        float swing = phase >= 1f ? 0f : Mathf.Sin(phase * Mathf.PI);
        Vector3 restPosition = equipped == Tool.Broom ? broomRestPosition : equipped == Tool.Hammer ? hammerRestPosition : sledgeRestPosition;
        Quaternion restRotation = equipped == Tool.Broom ? broomRestRotation : equipped == Tool.Hammer ? hammerRestRotation : sledgeRestRotation;
        held.localPosition = restPosition + new Vector3(0f, -.12f * swing, .18f * swing);
        held.localRotation = restRotation * Quaternion.Euler(55f * swing, 0f, -12f * swing);
    }
}
