using System.Collections.Generic;
using UnityEngine;

/// <summary>Controls the outlines of a single interaction target, including multi-model displays.</summary>
[DefaultExecutionOrder(100), DisallowMultipleComponent]
public sealed class InteractionHighlight : MonoBehaviour
{
    [SerializeField, Tooltip("Outlines controlled together by player focus. Can be on child models.")]
    private Outline[] outlines = new Outline[0];
    private bool focused, shopVisible;
    private readonly Dictionary<Outline, (Color color, float width, Outline.Mode mode)> originalStyles = new();
    private readonly HashSet<Object> warningSources = new();
    public bool IsWarningVisible => isActiveAndEnabled && warningSources.Count > 0;
    private static readonly Color ShopFocusColor = new Color(1f, .65f, .04f);

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

    public void SetWarning(Object source, bool visible)
    {
        if (source == null) return;
        if (visible)
        {
            if (!warningSources.Add(source)) return;
            if (outlines == null || outlines.Length == 0) Reset();
            // Movable props and separate trailer bodies may not have an interaction outline.
            if (outlines.Length == 0 && (GetComponentInChildren<MeshRenderer>() != null || GetComponentInChildren<SkinnedMeshRenderer>() != null))
                outlines = new[] { gameObject.AddComponent<Outline>() };
        }
        else if (!warningSources.Remove(source)) return;
        Apply();
    }

    private void LateUpdate()
    {
        if (warningSources.Count > 0) Apply();
    }

    private void Apply()
    {
        if (outlines == null || outlines.Length == 0) return;
        for (int i = 0; i < outlines.Length; i++)
        {
            var outline = outlines[i];
            if (!outline) continue;
            // Cache by object so edits to the outline list cannot invalidate saved styles.
            bool warning = IsWarningVisible;
            if ((shopVisible || warning) && !originalStyles.ContainsKey(outline))
                originalStyles.Add(outline, (outline.OutlineColor, outline.OutlineWidth, outline.OutlineMode));
            if (originalStyles.TryGetValue(outline, out var original))
            {
                outline.OutlineColor = warning ? Color.red : shopVisible ? (focused ? ShopFocusColor : Color.white) : original.color;
                outline.OutlineWidth = warning ? 5.5f + 1.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 3f) : shopVisible ? (focused ? 6f : 2f) : original.width;
                outline.OutlineMode = warning ? Outline.Mode.OutlineAll : original.mode;
            }
            bool show = isActiveAndEnabled && (focused || shopVisible || warning);
            if (outline.enabled != show) outline.enabled = show;
        }
    }

    private void OnEnable() => Apply();
    private void OnDisable() { focused = false; Apply(); }
}
