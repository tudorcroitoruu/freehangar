using UnityEngine;

/// <summary>Controls the outlines of a single interaction target, including multi-model displays.</summary>
[DefaultExecutionOrder(100), DisallowMultipleComponent]
public sealed class InteractionHighlight : MonoBehaviour
{
    [SerializeField, Tooltip("Outlines controlled together by player focus. Can be on child models.")]
    private Outline[] outlines = new Outline[0];
    private bool focused, shopVisible;
    private Color[] originalColors;
    private float[] originalWidths;
    private static readonly Color ShopFocusColor = new Color(1f, .42f, .04f);

    private void Reset() => outlines = GetComponentsInChildren<Outline>(true);

    private void Awake()
    {
        if (outlines == null || outlines.Length == 0) Reset();
    }

    public void SetFocused(bool value)
    {
        if (focused == value) return;
        focused = value;
        Apply();
    }

    public void SetShopVisible(bool value)
    {
        if (shopVisible == value) return;
        shopVisible = value;
        Apply();
    }

    private void Apply()
    {
        if (outlines == null || outlines.Length == 0) return;
        if (shopVisible && originalColors == null)
        {
            originalColors = new Color[outlines.Length];
            originalWidths = new float[outlines.Length];
            for (int i = 0; i < outlines.Length; i++)
                if (outlines[i])
                {
                    originalColors[i] = outlines[i].OutlineColor;
                    originalWidths[i] = outlines[i].OutlineWidth;
                }
        }

        for (int i = 0; i < outlines.Length; i++)
        {
            var outline = outlines[i];
            if (!outline) continue;
            if (originalColors != null)
            {
                outline.OutlineColor = shopVisible ? (focused ? ShopFocusColor : Color.white) : originalColors[i];
                outline.OutlineWidth = shopVisible ? (focused ? 6f : 2f) : originalWidths[i];
            }
            bool show = isActiveAndEnabled && (focused || shopVisible);
            if (outline.enabled != show) outline.enabled = show;
        }
    }

    private void OnEnable() => Apply();
    private void OnDisable() { focused = false; Apply(); }
}
