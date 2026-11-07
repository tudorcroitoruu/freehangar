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
        // Riding moves the view onto the vehicle while the walking body stays
        // at the mounting point. Shop visibility must follow the player's view.
        Vector3 position = player && player.characterCamera != null
            ? player.characterCamera.transform.position : player ? player.transform.position : Vector3.zero;
        SetInside(player && localBounds.Contains(transform.InverseTransformPoint(position)));
    }

    private void SetInside(bool value)
    {
        if (inside == value) return;
        inside = value;
        foreach (var display in displays)
            if (display) display.SetShopVisible(value);
    }

    private void OnDisable() => SetInside(false);

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Color color = inside ? Color.green : Color.cyan;
        Gizmos.color = new Color(color.r, color.g, color.b, .12f);
        Gizmos.DrawCube(localBounds.center, localBounds.size);
        Gizmos.color = color;
        Gizmos.DrawWireCube(localBounds.center, localBounds.size);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
