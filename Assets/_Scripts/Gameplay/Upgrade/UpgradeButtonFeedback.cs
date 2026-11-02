using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Disabled purchase buttons still explain a rejected click with the failure sound.</summary>
[DisallowMultipleComponent]
public sealed class UpgradeButtonFeedback : MonoBehaviour, IPointerClickHandler
{
    private Button button;
    private Behaviour owner;

    public static void Bind(Button button, Behaviour owner)
    {
        if (button == null) return;
        var feedback = button.GetComponent<UpgradeButtonFeedback>() ?? button.gameObject.AddComponent<UpgradeButtonFeedback>();
        feedback.button = button;
        feedback.owner = owner;
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || owner == null || !owner.isActiveAndEnabled ||
            button == null || !button.isActiveAndEnabled || button.IsInteractable() || SaveSystem.UpgradePurchaseLocked) return;
        GameplayAudio.Instance?.UpgradeResult(false);
    }
}
