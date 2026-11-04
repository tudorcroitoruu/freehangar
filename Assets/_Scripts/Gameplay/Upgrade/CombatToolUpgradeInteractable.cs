using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatToolUpgradeInteractable : MonoBehaviour, IKeyedInteractable
{
    [SerializeField] private PlayerCombatTools.Tool tool;
    [SerializeField] private UpgradeManager manager;
    public KeyCode InteractionKey => KeyCode.F;
    public WarningType FindWarning => tool == PlayerCombatTools.Tool.Axe ? WarningType.FindAxe : WarningType.FindHammer;
    public bool Found => CombatToolUpgrades.Found(SaveSystem.Data, tool);

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = new InteractionPromptData(transform.position + Vector3.up * .15f, $"Upgrade {tool.ToString().ToLowerInvariant()}", Found);
        prompt.InteractionKey = InteractionKey;
        return isActiveAndEnabled && interactor && manager && !UpgradeManager.BlocksGameplayInput && !UIPopups.BasePopupUI.HasActivePopup;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!Found || !TryGetPrompt(interactor, out _) || !manager.OpenCombatTool(tool)) return false;
        interactor.GetComponent<PlayerController>()?.Shovel?.CancelAction();
        return true;
    }
}
