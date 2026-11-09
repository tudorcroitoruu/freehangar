using UnityEngine;
using System.Collections.Generic;

/// <summary>Place on the physical weighs button, with a non-trigger collider.</summary>
[DisallowMultipleComponent]
public sealed class SellingWeighButton : MonoBehaviour, IInteractable
{
    [SerializeField] private SellingZone sellingZone;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .2f;
    [Header("Ready to sell arrow")]
    [SerializeField] private Transform readyArrow;
    [SerializeField] private PlayerInteractor player;
    [SerializeField, Min(0f)] private float arrowTravel = .3f;
    [SerializeField, Min(.1f)] private float arrowPeriod = 1.5f;
    private Vector3 arrowRestPosition;
    private float nextArrowScan, arrowTime;
    private bool arrowReady;
    private readonly List<GrainContainer> containers = new List<GrainContainer>();
    private Vector3 PromptPosition => (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset;

    private void Reset() => sellingZone = GetComponentInParent<SellingZone>();

    private void Awake()
    {
        if (sellingZone == null) sellingZone = GetComponentInParent<SellingZone>();
        if (readyArrow != null) arrowRestPosition = readyArrow.localPosition;
        SetArrowVisible(false);
    }

    private void Update()
    {
        if (readyArrow == null) return;
        bool blocked = Time.timeScale <= 0f || UpgradeManager.BlocksGameplayInput ||
            SellingScreen.BlocksGameplayInput || UIPopups.BasePopupUI.HasActivePopup;
        if (Time.unscaledTime >= nextArrowScan)
        {
            nextArrowScan = Time.unscaledTime + .1f;
            if (player == null) player = FindFirstObjectByType<PlayerInteractor>();
            arrowReady = sellingZone != null && sellingZone.CanWeigh(player);
        }
        SetArrowVisible(!blocked && arrowReady);
        if (!readyArrow.gameObject.activeSelf) return;
        arrowTime = Mathf.Repeat(arrowTime + Time.deltaTime, Mathf.Max(.1f, arrowPeriod));
        float height = (1f - Mathf.Cos(arrowTime * Mathf.PI * 2f / Mathf.Max(.1f, arrowPeriod))) * .5f * arrowTravel;
        Vector3 offset = Vector3.up * height;
        readyArrow.localPosition = arrowRestPosition +
            (readyArrow.parent != null ? readyArrow.parent.InverseTransformVector(offset) : offset);
    }

    private void SetArrowVisible(bool visible)
    {
        if (readyArrow == null) return;
        if (readyArrow.gameObject.activeSelf != visible) readyArrow.gameObject.SetActive(visible);
        if (!visible)
        {
            arrowTime = 0f;
            readyArrow.localPosition = arrowRestPosition;
        }
    }

    private void OnDisable()
    {
        arrowReady = false;
        nextArrowScan = 0f;
        SetArrowVisible(false);
    }

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
