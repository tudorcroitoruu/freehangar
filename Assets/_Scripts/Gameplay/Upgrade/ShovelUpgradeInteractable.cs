using UnityEngine;

/// <summary>Attach to the workshop display root, with shovel models and colliders on children.</summary>
[DisallowMultipleComponent]
public sealed class ShovelUpgradeInteractable : MonoBehaviour, IKeyedInteractable
{
    public KeyCode InteractionKey => KeyCode.F;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .3f;
    [Header("Displayed Shovel")]
    [SerializeField] private ShovelUpgradeSettings settings;
    [Tooltip("One model per shovel type, in the same order as Shovel Upgrade Settings. Use child objects; keep this interaction root active.")]
    [SerializeField] private GameObject[] shovelModels;

    private void OnEnable()
    {
        if (settings == null) settings = Resources.Load<ShovelUpgradeSettings>(ShovelUpgradeSettings.ResourcePath);
        SaveSystem.MoneyChanged += OnProgressChanged;
        RefreshDisplay();
    }

    private void OnDisable() => SaveSystem.MoneyChanged -= OnProgressChanged;

    private void OnProgressChanged(int _) => RefreshDisplay();

    public void RefreshDisplay()
    {
        if (settings == null || !settings.IsConfigured || shovelModels == null) return;
        int type = settings.TypeIndex(SaveSystem.Data?.shovelUpgradeLevel ?? 0);
        for (int i = 0; i < shovelModels.Length; i++)
        {
            var model = shovelModels[i];
            // A model must never disable this component or one of its ancestors.
            if (model == null || transform.IsChildOf(model.transform)) continue;
            bool active = i == type;
            if (model.activeSelf != active) model.SetActive(active);
        }
    }

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
            (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset, "Upgrade shovel");
        prompt.InteractionKey = InteractionKey;
        return isActiveAndEnabled && interactor != null && Manager != null && !Manager.IsOpen &&
            !UpgradeManager.BlocksGameplayInput && !UIPopups.BasePopupUI.HasActivePopup;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out _) || !Manager.OpenShovel()) return false;
        var player = interactor.GetComponent<PlayerController>();
        if (player != null && player.Shovel != null) player.Shovel.CancelAction();
        return true;
    }
}
