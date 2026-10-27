using UnityEngine;

/// <summary>Resolves the nearest visible interactable and owns its shared prompt.</summary>
[DefaultExecutionOrder(300), DisallowMultipleComponent]
public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private InteractionPrompt prompt;
    [SerializeField, Min(.1f)] private float interactionDistance = 4f;
    [SerializeField] private LayerMask interactionMask = ~0;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private PlayerController player;
    private PlayerBicycleInteraction bicycle;
    private InteractionHighlight highlight;
    public BagInteraction Bags { get; private set; }
    public IInteractable Target { get; private set; }

    private bool CanInteract => isActiveAndEnabled && viewCamera != null && viewCamera.isActiveAndEnabled &&
        Time.timeScale > 0 && Application.isFocused && Cursor.lockState == CursorLockMode.Locked &&
        (player == null || (player.isActiveAndEnabled && player.characterController != null && player.characterController.enabled)) &&
        (bicycle == null || !bicycle.IsRiding);

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        bicycle = GetComponent<PlayerBicycleInteraction>();
        Bags = GetComponent<BagInteraction>();
        if (viewCamera == null && player != null) viewCamera = player.characterCamera;
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        if (prompt == null && Bags != null) prompt = Bags.Prompt;
    }

    private void LateUpdate()
    {
        Target = null;
        if (!CanInteract) { SetHighlightedTarget(null); HidePrompt(); return; }
        if (Bags != null && Bags.isActiveAndEnabled && Bags.IsCarrying)
        {
            SetHighlightedTarget(null);
            if (Bags.TryGetPrompt(Bags.CarriedBag, out var carriedPrompt) && prompt != null)
                prompt.Show(viewCamera, carriedPrompt);
            else HidePrompt();
            return;
        }
        Target = FindTarget();
        if (Target != null && Target.TryGetPrompt(this, out var data))
        {
            SetHighlightedTarget(Target);
            if (prompt != null) prompt.Show(viewCamera, data);
        }
        else { SetHighlightedTarget(null); HidePrompt(); }
    }

    public bool TryInteract()
    {
        if (!CanInteract) return false;
        // Carrying reserves E for dropping; bicycle cargo handling takes priority in PlayerController.
        if (Bags != null && Bags.isActiveAndEnabled && Bags.IsCarrying) return Bags.TryInteract();
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

    private void HidePrompt() { if (prompt != null) prompt.Hide(); }

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

    private void OnDisable() { Target = null; SetHighlightedTarget(null); HidePrompt(); }
}
