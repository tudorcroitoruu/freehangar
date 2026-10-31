using UnityEngine;

/// <summary>Place on the physical weighs button, with a non-trigger collider.</summary>
[DisallowMultipleComponent]
public sealed class SellingWeighButton : MonoBehaviour, IInteractable
{
    [SerializeField] private SellingZone sellingZone;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .2f;

    private void Reset() => sellingZone = GetComponentInParent<SellingZone>();

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        bool ready = sellingZone != null && sellingZone.CanWeigh(interactor);
        prompt = new InteractionPromptData(
            (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset,
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
}
