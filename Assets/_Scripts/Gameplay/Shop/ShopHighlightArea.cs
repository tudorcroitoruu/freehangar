using UnityEngine;

// Keeps nearby shop displays visible even when the player is looking elsewhere.
public sealed class ShopHighlightArea : MonoBehaviour
{
    [SerializeField] private Bounds localBounds;
    [SerializeField] private InteractionHighlight[] displays = System.Array.Empty<InteractionHighlight>();
    private PlayerController player;
    private bool inside;

    private void Update()
    {
        if (!player) player = FindFirstObjectByType<PlayerController>();
        SetInside(player && localBounds.Contains(transform.InverseTransformPoint(player.transform.position)));
    }

    private void SetInside(bool value)
    {
        if (inside == value) return;
        inside = value;
        foreach (var display in displays)
            if (display) display.SetShopVisible(value);
    }

    private void OnDisable() => SetInside(false);
}
