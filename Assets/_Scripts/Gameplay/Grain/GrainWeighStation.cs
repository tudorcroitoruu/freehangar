using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>Sells unloaded grain containers only while the entire weighbridge is clear.</summary>
public sealed class GrainWeighStation : MonoBehaviour
{
    [SerializeField] private Vector3 platformSize = new Vector3(7, 4, 12);
    [SerializeField] private Vector2 dropSize = new Vector2(3.8f, 4.8f);
    [SerializeField, Min(1)] private int cashPerKg = 2;
    [SerializeField, Min(.1f)] private float clearDelay = 1.25f;
    [SerializeField, Min(.1f)] private float saleDuration = .8f;
    [SerializeField] private TMP_Text statusText;
    private readonly Collider[] overlaps = new Collider[256];
    private readonly HashSet<GrainContainer> candidates = new HashSet<GrainContainer>();
    private GrainContainer selling;
    private Rigidbody saleBody;
    private Vector3 startPosition, startScale;
    private bool wasKinematic;
    private float clearTime, animationTime, nextScan, receiptUntil;
    private int quotedCash;
    public bool IsBlocked { get; private set; }
    public bool IsSelling => selling != null;
    public int CompletedSales { get; private set; }

    private void Update()
    {
        if (Time.deltaTime <= 0) return;
        if (selling == null && Time.time < nextScan) return;
        float elapsed = selling != null ? Time.deltaTime : .1f;
        nextScan = Time.time + .1f;
        Scan();
        if (IsBlocked)
        {
            CancelSale(); clearTime = 0;
            SetStatus("STEP OFF THE SCALE\nMove players & vehicles away", new Color(1, .55f, .15f));
            return;
        }
        if (selling != null) { AnimateSale(); return; }
        GrainContainer candidate = null;
        foreach (var bag in candidates) if (Eligible(bag)) { candidate = bag; break; }
        if (candidate == null)
        {
            clearTime = 0;
            if (Time.time >= receiptUntil) SetStatus("DROP FILLED BAGS / BOXES\nCenter only • step off to sell", Color.white);
            return;
        }
        if (!CanPay(candidate, out int cash))
        {
            clearTime = 0; SetStatus("SALE UNAVAILABLE\nCash account is not ready", Color.yellow); return;
        }
        clearTime += elapsed;
        SetStatus($"WEIGHING  {candidate.Kilograms:0.0} kg\nKeep the scale clear • +${cash}", new Color(.5f, 1, .65f));
        if (clearTime < clearDelay) return;
        selling = candidate; quotedCash = cash; animationTime = 0; clearTime = 0;
        startPosition = selling.transform.position; startScale = selling.transform.localScale;
        saleBody = selling.GetComponent<Rigidbody>();
        if (saleBody != null) { wasKinematic = saleBody.isKinematic; saleBody.isKinematic = true; }
        // Disabled receivers cannot be picked up, filled or claimed by another scale.
        selling.enabled = false;
    }

    private void Scan()
    {
        candidates.Clear();
        int count = Physics.OverlapBoxNonAlloc(transform.TransformPoint(Vector3.up * platformSize.y * .5f),
            platformSize * .5f, overlaps, transform.rotation, ~0, QueryTriggerInteraction.Collide);
        IsBlocked = count == overlaps.Length; // Fail closed if the query is saturated.
        for (int i = 0; i < count; i++)
        {
            var c = overlaps[i];
            if (c.GetComponentInParent<PlayerController>() != null || c is CharacterController ||
                c.GetComponentInParent<BicycleController>() != null || c.GetComponentInParent<WheelbarrowCargo>() != null)
            { IsBlocked = true; continue; }
            var bag = c.GetComponentInParent<GrainContainer>();
            if (bag != null) { candidates.Add(bag); continue; }
            // Unknown movable cargo must be removed; scenery and station fixtures are ignored.
            if (!c.isTrigger && c.attachedRigidbody != null) IsBlocked = true;
        }
    }

    private bool Eligible(GrainContainer bag)
    {
        if (bag == null || !bag.isActiveAndEnabled || bag.IsCarried || bag.IsClosing || bag.Kilograms <= .001f ||
            bag.GetComponentInParent<BicycleController>() != null || bag.GetComponentInParent<WheelbarrowCargo>() != null) return false;
        var box = bag.GetComponent<BicycleCargoBox>();
        if (box != null && box.IsLoaded) return false;
        var body = bag.GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > .04f) return false;
        Bounds bounds = bag.WorldBounds;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            if (Mathf.Abs(p.x) > dropSize.x * .5f || Mathf.Abs(p.z) > dropSize.y * .5f || p.y > platformSize.y || p.y < -.25f) return false;
        }
        return true;
    }

    private bool CanPay(GrainContainer bag, out int cash)
    {
        double value = System.Math.Floor(bag.Kilograms * (double)cashPerKg);
        cash = value >= 1 && value <= int.MaxValue ? (int)value : 0;
        return cash > 0 && SaveSystem.Data != null && SaveSystem.Money <= int.MaxValue - cash;
    }

    private void AnimateSale()
    {
        animationTime += Time.deltaTime;
        float t = Mathf.Clamp01(animationTime / saleDuration);
        float pop = t < .22f ? Mathf.Lerp(1, 1.15f, t / .22f) : Mathf.Lerp(1.15f, 0, Mathf.SmoothStep(0, 1, (t - .22f) / .78f));
        selling.transform.position = startPosition + Vector3.up * (1.4f * Mathf.Sin(t * Mathf.PI * .5f));
        selling.transform.localScale = startScale * pop;
        SetStatus($"SELLING {selling.Kilograms:0.0} kg\n+${quotedCash}", new Color(.5f, 1, .65f));
        if (t < 1) return;
        if (!SaveSystem.AddMoney(quotedCash)) { CancelSale(); return; }
        if (DataHolderUI.Instance != null) DataHolderUI.Instance.RefreshDataPlayer();
        SetStatus($"+${quotedCash}  CASH RECEIVED\nThank you!", new Color(.5f, 1, .65f));
        receiptUntil = Time.time + 2;
        var sold = selling.gameObject; selling = null; saleBody = null;
        sold.SetActive(false); Destroy(sold); CompletedSales++;
    }

    private void CancelSale()
    {
        if (selling != null)
        {
            selling.transform.position = startPosition; selling.transform.localScale = startScale;
            selling.enabled = true;
            if (saleBody != null) saleBody.isKinematic = wasKinematic;
        }
        selling = null; saleBody = null; animationTime = 0;
    }
    private void OnDisable() { CancelSale(); clearTime = 0; }
    private void SetStatus(string message, Color color)
    {
        if (statusText == null) return;
        if (statusText.text != message) statusText.text = message;
        statusText.color = color;
    }
}
