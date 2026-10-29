using UnityEngine;

/// <summary>Equips the buried hand tools and drives short first-person barrier strikes.</summary>
[DisallowMultipleComponent]
public sealed class PlayerCombatTools : MonoBehaviour
{
    public enum Tool { Shovel = 1, Hammer = 2, Sledgehammer = 3 }

    public const string HammerFindId = "farm.tool.hammer";
    public const string SledgehammerFindId = "farm.tool.sledgehammer";

    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform shovelHands;
    [SerializeField] private ShovelTool shovelTool;
    [SerializeField] private BagInteraction bags;
    [SerializeField] private Transform heldHammer;
    [SerializeField] private Transform heldSledgehammer;
    [SerializeField, Min(.5f)] private float strikeReach = 3f;
    [SerializeField, Min(.1f)] private float strikeDuration = .55f;

    private PlayerInteractor interactor;
    private Tool equipped = Tool.Shovel;
    private float strikeStart = -10f;
    private Vector3 hammerRestPosition, sledgeRestPosition;
    private Quaternion hammerRestRotation, sledgeRestRotation;

    public Tool Equipped => equipped;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (bags == null) bags = GetComponent<BagInteraction>();
        if (heldHammer != null) { hammerRestPosition = heldHammer.localPosition; hammerRestRotation = heldHammer.localRotation; }
        if (heldSledgehammer != null) { sledgeRestPosition = heldSledgehammer.localPosition; sledgeRestRotation = heldSledgehammer.localRotation; }
        ApplyVisuals();
    }

    public void Tick(bool acceptInput, bool wasLocked)
    {
        if (!acceptInput) return;
        if (Input.GetKeyDown(KeyCode.Alpha1)) TryEquip(Tool.Shovel);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) TryEquip(Tool.Hammer);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) TryEquip(Tool.Sledgehammer);

        if (equipped == Tool.Shovel || !wasLocked || !Input.GetMouseButtonDown(0) ||
            Time.time < strikeStart + strikeDuration || viewCamera == null ||
            (bags != null && bags.IsCarrying) || (interactor != null && interactor.PushedWheelbarrow != null)) return;

        strikeStart = Time.time;
        if (!Physics.Raycast(PlayerAim.CenterRay(viewCamera), out RaycastHit hit, strikeReach, ~0, QueryTriggerInteraction.Ignore)) return;
        if (hit.transform.IsChildOf(transform)) return;
        BreakableGrainBarrier wall = hit.collider.GetComponentInParent<BreakableGrainBarrier>();
        if (wall != null) wall.TryHit(equipped);
    }

    public bool TryEquip(Tool tool)
    {
        if (tool == equipped) return true;
        if (tool == Tool.Hammer && !BuriedFind.WasFound(SaveSystem.Data, HammerFindId)) return false;
        if (tool == Tool.Sledgehammer && !BuriedFind.WasFound(SaveSystem.Data, SledgehammerFindId)) return false;
        if (bags != null && bags.IsCarrying) return false;
        if (interactor != null && interactor.PushedWheelbarrow != null) return false;
        if (shovelTool != null && (shovelTool.Kilograms > 0f || shovelTool.IsBusy)) return false;
        if (tool != Tool.Shovel && (tool == Tool.Hammer ? heldHammer : heldSledgehammer) == null) return false;
        if (shovelTool != null) shovelTool.CancelAction();
        equipped = tool;
        strikeStart = -10f;
        ApplyVisuals();
        return true;
    }

    public void HideForPushing(System.Collections.Generic.List<GameObject> hidden)
    {
        strikeStart = -10f;
        foreach (var tool in new[] { heldHammer, heldSledgehammer })
            if (tool != null && tool.gameObject.activeSelf) { hidden.Add(tool.gameObject); tool.gameObject.SetActive(false); }
    }

    private void ApplyVisuals()
    {
        if (shovelHands != null) shovelHands.gameObject.SetActive(equipped == Tool.Shovel);
        if (heldHammer != null) heldHammer.gameObject.SetActive(equipped == Tool.Hammer);
        if (heldSledgehammer != null) heldSledgehammer.gameObject.SetActive(equipped == Tool.Sledgehammer);
    }

    private void LateUpdate()
    {
        Transform held = equipped == Tool.Hammer ? heldHammer : equipped == Tool.Sledgehammer ? heldSledgehammer : null;
        if (held == null) return;
        float phase = Mathf.Clamp01((Time.time - strikeStart) / strikeDuration);
        float swing = phase >= 1f ? 0f : Mathf.Sin(phase * Mathf.PI);
        Vector3 restPosition = equipped == Tool.Hammer ? hammerRestPosition : sledgeRestPosition;
        Quaternion restRotation = equipped == Tool.Hammer ? hammerRestRotation : sledgeRestRotation;
        held.localPosition = restPosition + new Vector3(0f, -.12f * swing, .18f * swing);
        held.localRotation = restRotation * Quaternion.Euler(55f * swing, 0f, -12f * swing);
    }
}
