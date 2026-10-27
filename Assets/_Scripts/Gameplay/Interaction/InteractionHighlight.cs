using UnityEngine;

/// <summary>Optional focus outlines. Add beside IInteractable and assign the object's outlines.</summary>
[DefaultExecutionOrder(100), DisallowMultipleComponent]
public sealed class InteractionHighlight : MonoBehaviour
{
    [SerializeField, Tooltip("Outlines controlled together by player focus. Can be on child models.")]
    private Outline[] outlines = new Outline[0];

    private void Reset()
    {
        outlines = GetComponentsInChildren<Outline>(true);
    }

    private void Awake()
    {
        if (outlines == null || outlines.Length == 0)
            outlines = GetComponentsInChildren<Outline>(true);
        SetFocused(false);
    }

    public void SetFocused(bool focused)
    {
        bool show = focused && isActiveAndEnabled;
        if (outlines == null) return;
        foreach (var item in outlines)
            if (item != null && item.enabled != show) item.enabled = show;
    }

    private void OnEnable() => SetFocused(false);
    private void OnDisable() => SetFocused(false);
}
