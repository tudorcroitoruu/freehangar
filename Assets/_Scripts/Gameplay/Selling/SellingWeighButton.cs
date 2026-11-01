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
        prompt = new InteractionPromptData(
            PromptPosition,
            ready ? "Weighs" : "Park vehicle on scale and step off", ready);
        prompt.RequiresFreeHands = interactor != null && interactor.HandsOccupied;
        return isActiveAndEnabled && sellingZone != null && interactor != null &&
            !UpgradeManager.BlocksGameplayInput && !UIPopups.BasePopupUI.HasActivePopup;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out var prompt) || !prompt.ShowKey) return false;
        return sellingZone.TryOpen(interactor);
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
