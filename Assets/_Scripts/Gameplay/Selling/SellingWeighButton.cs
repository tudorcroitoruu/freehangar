using UnityEngine;
using System.Collections.Generic;

/// <summary>Place on the physical weighs button, with a non-trigger collider.</summary>
[DisallowMultipleComponent]
public sealed class SellingWeighButton : MonoBehaviour, IInteractable
{
    [SerializeField] private SellingZone sellingZone;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .2f;
    private readonly List<GrainContainer> containers = new List<GrainContainer>();
    private Vector3 PromptPosition => (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset;

    private void Reset() => sellingZone = GetComponentInParent<SellingZone>();

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || sellingZone == null || !sellingZone.isActiveAndEnabled || interactor == null ||
            UpgradeManager.BlocksGameplayInput || UIPopups.BasePopupUI.HasActivePopup) return false;
        bool wrong = sellingZone.HasBlockingOccupants || sellingZone.Contains(interactor);
        if (wrong) sellingZone.PingBlockers();
        sellingZone.CollectContainers(containers);
        bool empty = !wrong && containers.Count == 0;
        bool ready = !wrong && !empty && sellingZone.CanWeigh(interactor);
        prompt = new InteractionPromptData(PromptPosition, "Start weighing", ready)
        {
            WeighingWrong = wrong,
            WeighingEmpty = empty,
            RequiresFreeHands = !wrong && !empty && interactor.HandsOccupied
        };
        return true;
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
