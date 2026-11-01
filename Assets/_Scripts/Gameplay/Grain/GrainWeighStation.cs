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
    public const string RedButtonInstruction = "Press on red weight button on left side of the weighting machine";

    private void OnEnable() => RefreshStatus();

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
            SetStatus("WEIGHING UNAVAILABLE", Color.yellow);
            return;
        }
        if (sellingZone.HasVehicleOnScale)
        {
            SetStatus("Unload the containers, then move the vehicle off the scale.\n" + RedButtonInstruction, new Color(1, .55f, .15f));
            return;
        }
        if (player != null && sellingZone.Contains(player))
        {
            SetStatus("Step off the scale. Only containers may remain.\n" + RedButtonInstruction, new Color(1, .55f, .15f));
            return;
        }
        if (sellingZone.HasBlockingOccupants)
        {
            SetStatus("Remove people and other objects. Only containers may remain.\n" + RedButtonInstruction, new Color(1, .55f, .15f));
            return;
        }
        if (player != null && player.HandsOccupied)
        {
            SetStatus("Free your hands before weighing.\n" + RedButtonInstruction, new Color(1, .55f, .15f));
            return;
        }
        sellingZone.CollectContainers(containers);
        if (containers.Count == 0)
        {
            SetStatus("Place filled grain containers on the scale.\n" + RedButtonInstruction, Color.white);
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
}
