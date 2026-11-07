using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Displays weighing instructions only. SellingScreen owns all sales and payments.</summary>
public sealed class GrainWeighStation : MonoBehaviour
{
    [SerializeField] private SellingZone sellingZone;
    [SerializeField] private PlayerInteractor player;
    [SerializeField] private TMP_Text statusText;
    private readonly List<GrainContainer> containers = new List<GrainContainer>();
    private float nextScan;
    public string StatusMessage { get; private set; }
    public const string RedButtonInstruction = "Press on red weight button on right side of the weighting machine";

    private void OnEnable() => RefreshStatus();
    private void OnDisable() => SetWrongWarning(false);

    private void Update()
    {
        if (Time.deltaTime <= 0 || Time.time < nextScan) return;
        nextScan = Time.time + .1f;
        RefreshStatus();
    }

    public void RefreshStatus()
    {
        if (statusText == null) statusText = GetComponentInChildren<TMP_Text>(true);
        if (sellingZone == null) sellingZone = GetComponentInParent<SellingZone>();
        if (player == null) player = FindFirstObjectByType<PlayerInteractor>();
        if (sellingZone == null || !sellingZone.isActiveAndEnabled)
        {
            SetWrongWarning(false);
            SetStatus("WEIGHING UNAVAILABLE", Color.yellow);
            return;
        }
        bool wrong = sellingZone.HasVehicleOnScale || (player != null && sellingZone.Contains(player)) ||
            sellingZone.HasBlockingOccupants;
        // HUD warnings belong to the aimed red button (PlayerInteractor).
        // Merely standing on or walking past the scale must not show a popup.
        SetWrongWarning(false);
        if (wrong)
        {
            SetStatus("", Color.white);
            return;
        }
        sellingZone.CollectContainers(containers);
        if (containers.Count == 0)
        {
            SetStatus("Place containers with grain on the scale.\n" + RedButtonInstruction, Color.white);
            return;
        }
        SetStatus("READY TO WEIGH\n" + RedButtonInstruction, new Color(.5f, 1, .65f));
    }

    private void SetStatus(string message, Color color)
    {
        StatusMessage = message;
        if (statusText == null) return;
        if (statusText.text != message) statusText.text = message;
        statusText.color = color;
    }

    private void SetWrongWarning(bool visible) =>
        GameplayCanvas.Instance?.SetWarning(WarningType.weighingWrong, this,
            visible && (player == null || !player.HandsOccupied));
}
