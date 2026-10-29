using UnityEngine;

/// <summary>Opens stamina upgrades when the player interacts with this station's collider.</summary>
[DisallowMultipleComponent]
public sealed class StaminaUpgradeInteractable : MonoBehaviour, IKeyedInteractable
{
    public KeyCode InteractionKey => KeyCode.F;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .3f;

    private UpgradeManager Manager
    {
        get
        {
            if (upgradeManager == null)
                upgradeManager = FindFirstObjectByType<UpgradeManager>(FindObjectsInactive.Include);
            return upgradeManager;
        }
    }

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = new InteractionPromptData(
            (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset, "Upgrade stamina");
        prompt.InteractionKey = InteractionKey;
        return isActiveAndEnabled && interactor != null && Manager != null && !Manager.IsOpen &&
            !UpgradeManager.BlocksGameplayInput && !UIPopups.BasePopupUI.HasActivePopup;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out _) || !Manager.OpenStamina()) return false;
        var player = interactor.GetComponent<PlayerController>();
        if (player != null && player.Shovel != null) player.Shovel.CancelAction();
        return true;
    }
}
