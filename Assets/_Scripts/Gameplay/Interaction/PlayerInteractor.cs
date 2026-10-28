using UnityEngine;

/// <summary>Resolves the nearest visible interactable and owns its shared prompt.</summary>
[DefaultExecutionOrder(300), DisallowMultipleComponent]
public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(.1f)] private float interactionDistance = 4f;
    [SerializeField] private LayerMask interactionMask = ~0;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private PlayerController player;
    private PlayerBicycleInteraction bicycle;
    private InteractionHighlight highlight;
    public BagInteraction Bags { get; private set; }
    public PlayerItemCarry Items { get; private set; }
    public WheelbarrowCargo PushedWheelbarrow { get; set; }
    public IInteractable Target { get; private set; }
    private Object discoveryOwner;
    public bool DiscoveryBusy => discoveryOwner != null;
    public bool BeginDiscovery(Object owner)
    {
        if (owner == null || DiscoveryBusy) return false;
        discoveryOwner = owner; return true;
    }
    public void EndDiscovery(Object owner) { if (discoveryOwner == owner) discoveryOwner = null; }
    public bool HandsOccupied => (GetComponent<PlayerController>() is PlayerController actor &&
        (actor.IsShovelEquipped || actor.IsCarrying)) || (Bags != null && Bags.IsCarrying) ||
        (Items != null && Items.IsCarrying) || PushedWheelbarrow != null;

    private InteractionPrompt Prompt => GameplayCanvas.Instance != null ? GameplayCanvas.Instance.interactionPrompt : null;

    private bool CanInteract => !DiscoveryBusy && isActiveAndEnabled && viewCamera != null && viewCamera.isActiveAndEnabled &&
        Time.timeScale > 0 && Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !UIPopups.BasePopupUI.HasActivePopup &&
        (player == null || (player.isActiveAndEnabled && player.characterController != null && player.characterController.enabled)) &&
        (bicycle == null || !bicycle.IsRiding);

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        bicycle = GetComponent<PlayerBicycleInteraction>();
        Bags = GetComponent<BagInteraction>();
        Items = GetComponent<PlayerItemCarry>();
        if (Items == null && player != null) Items = gameObject.AddComponent<PlayerItemCarry>();
        if (viewCamera == null && player != null) viewCamera = player.characterCamera;
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
    }

    private void LateUpdate()
    {
        var prompt = Prompt;
        Target = null;
        if (!CanInteract) { SetFreeHandsWarning(false); SetHighlightedTarget(null); HidePrompt(); return; }
        var aimedTarget = FindTarget();
        InteractionPromptData aimedPrompt = default;
        bool hasAimedPrompt = aimedTarget != null && aimedTarget.TryGetPrompt(this, out aimedPrompt);
        SetFreeHandsWarning(hasAimedPrompt && aimedPrompt.RequiresFreeHands);
        if (hasAimedPrompt && aimedPrompt.RequiresFreeHands)
        {
            Target = aimedTarget;
            SetHighlightedTarget(null);
            HidePrompt();
            return;
        }
        if (Items != null && Items.IsCarrying)
        {
            SetHighlightedTarget(null);
            HidePrompt();
            return;
        }
        if (PushedWheelbarrow != null)
        {
            SetHighlightedTarget(null);
            if (PushedWheelbarrow.TryGetPrompt(this, out var pushPrompt) && prompt != null)
                prompt.Show(viewCamera, pushPrompt);
            else HidePrompt();
            return;
        }
        if (Bags != null && Bags.isActiveAndEnabled && Bags.IsCarrying)
        {
            SetHighlightedTarget(null);
            var wheelbarrow = aimedTarget as WheelbarrowCargo;
            if (wheelbarrow != null && wheelbarrow.TryGetPrompt(this, out var loadPrompt))
            {
                if (prompt != null) prompt.Show(viewCamera, loadPrompt);
                return;
            }
            HidePrompt();
            return;
        }
        Target = aimedTarget;
        if (hasAimedPrompt)
        {
            SetHighlightedTarget(Target);
            if (prompt != null) prompt.Show(viewCamera, aimedPrompt);
        }
        else { SetHighlightedTarget(null); HidePrompt(); }
    }

    public bool TryInteract()
    {
        if (!CanInteract) return false;
        if (Items != null && Items.IsCarrying) return Items.TryPlace();
        if (PushedWheelbarrow != null) return PushedWheelbarrow.TryInteract(this);
        // Carrying reserves E for dropping; bicycle cargo handling takes priority in PlayerController.
        if (Bags != null && Bags.isActiveAndEnabled && Bags.IsCarrying)
        {
            var wheelbarrow = FindTarget() as WheelbarrowCargo;
            if (wheelbarrow != null)
                return wheelbarrow.TryGetPrompt(this, out var loadPrompt) && loadPrompt.ShowKey &&
                    wheelbarrow.TryInteract(this);
            return Bags.TryInteract();
        }
        Target = FindTarget();
        return Target != null && Target.TryGetPrompt(this, out var data) && data.ShowKey && Target.TryInteract(this);
    }

    private IInteractable FindTarget()
    {
        int count = Physics.RaycastNonAlloc(PlayerAim.CenterRay(viewCamera), hits, interactionDistance,
            interactionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return null;
        Collider closest = null;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(transform) || hits[i].distance >= distance) continue;
            closest = hits[i].collider;
            distance = hits[i].distance;
        }
        // Resolve only the first obstruction, including interactables on a collider's parent.
        if (closest == null) return null;
        var candidate = closest.GetComponentInParent<IInteractable>();
        if (candidate is Behaviour behaviour && !behaviour.isActiveAndEnabled) return null;
        return candidate;
    }

    private void HidePrompt()
    {
        var prompt = Prompt;
        if (prompt != null) prompt.Hide();
    }

    private void SetHighlightedTarget(IInteractable target)
    {
        var next = target is Component component && component != null ? component.GetComponent<InteractionHighlight>() : null;
        if (highlight != next)
        {
            if (highlight != null) highlight.SetFocused(false);
            highlight = next;
        }
        if (highlight != null) highlight.SetFocused(true);
    }

    private void SetFreeHandsWarning(bool visible)
    {
        if (GameplayCanvas.Instance != null) GameplayCanvas.Instance.SetFreeHandsWarning(visible);
    }

    private void OnDisable() { Target = null; SetFreeHandsWarning(false); SetHighlightedTarget(null); HidePrompt(); }
}
