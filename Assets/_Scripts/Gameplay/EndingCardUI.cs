using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored in GameCanvas; instantiated separately for the ending cinematic.</summary>
public sealed class EndingCardUI : MonoBehaviour
{
    public Canvas canvas;
    public Image black;
    public TextMeshProUGUI continuedText;
    public TextMeshProUGUI friendsText;

    private void OnEnable()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        // Nested canvases serialize as world-space inside GameCanvas. The copied
        // ending panel is a standalone screen overlay, not a scene-space object.
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.enabled = true;
        transform.localScale = Vector3.one;
    }
}
