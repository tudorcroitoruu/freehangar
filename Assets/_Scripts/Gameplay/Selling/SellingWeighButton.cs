using UnityEngine;

/// <summary>Place on the physical weighs button, with a non-trigger collider.</summary>
[DisallowMultipleComponent]
public sealed class SellingWeighButton : MonoBehaviour, IInteractable
{
    [SerializeField] private SellingZone sellingZone;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .2f;
    private Vector3 PromptPosition => (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset;

    private void Reset() => sellingZone = GetComponentInParent<SellingZone>();

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        bool ready = sellingZone != null && sellingZone.CanWeigh(interactor);
        string action = ready ? "Start weighing" : "Place filled containers on the scale and step off";
        if (!ready && sellingZone != null)
        {
            if (sellingZone.HasVehicleOnScale) action = "Unload containers and move the vehicle off the scale";
            else if (interactor != null && sellingZone.Contains(interactor)) action = "Step off the scale";
            else if (sellingZone.HasBlockingOccupants) action = "Clear the scale: containers only";
            else if (interactor != null && interactor.HandsOccupied) action = "Free your hands before weighing";
        }
        prompt = new InteractionPromptData(
            PromptPosition,
            action, ready);
        prompt.RequiresFreeHands = interactor != null && interactor.HandsOccupied;
        return isActiveAndEnabled && sellingZone != null && interactor != null &&
            !UpgradeManager.BlocksGameplayInput && !UIPopups.BasePopupUI.HasActivePopup;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out var prompt) || !prompt.ShowKey) return false;
        bool opened = sellingZone.TryOpen(interactor);
        if (opened) GameplayAudio.Instance?.PressWeighButton();
        return opened;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = promptAnchor != null ? promptAnchor.position : transform.position;
        Vector3 position = PromptPosition;
        Color previous = Gizmos.color;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, position);
        Gizmos.DrawWireSphere(position, .06f);
        Gizmos.DrawLine(position - Vector3.right * .09f, position + Vector3.right * .09f);
        Gizmos.DrawLine(position - Vector3.up * .09f, position + Vector3.up * .09f);
        Gizmos.DrawLine(position - Vector3.forward * .09f, position + Vector3.forward * .09f);
        Gizmos.color = previous;
#if UNITY_EDITOR
        UnityEditor.Handles.Label(position + Vector3.up * .12f, "Weigh prompt (promptOffset)");
#endif
    }
}
