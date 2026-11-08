using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LaunchCardUI : MonoBehaviour
{
    public Image fade;
    public TextMeshProUGUI countdown;
    public TextMeshProUGUI dialogue;

    private void OnEnable()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvas.enabled = true;
        transform.localScale = Vector3.one;
    }
}
