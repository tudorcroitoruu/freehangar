using UnityEngine;

/// <summary>Place on an upgrade station and assign the vehicle it services.</summary>
[DisallowMultipleComponent]
public sealed class VehicleUpgradeInteractable : MonoBehaviour, IKeyedInteractable
{
    public KeyCode InteractionKey => KeyCode.F;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private GameObject targetVehicle;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .3f;

    public static VehicleUpgradeInteractable FindForHit(Collider hit, PlayerInteractor interactor)
    {
        if (hit == null || (interactor != null && interactor.Bags != null && interactor.Bags.IsCarrying) ||
            hit.GetComponentInParent<GrainContainer>() != null || hit.GetComponentInParent<BicycleCargoBox>() != null) return null;
        var station = hit.GetComponentInParent<VehicleUpgradeInteractable>();
        return station != null && station.isActiveAndEnabled ? station : null;
    }

    private VehicleUpgrade Target
    {
        get
        {
            var root = targetVehicle != null ? targetVehicle : gameObject;
            var target = root.GetComponent<VehicleUpgrade>();
            if (target == null && (root.GetComponent<WheelbarrowCargo>() != null || root.GetComponent<BicycleController>() != null))
                target = root.AddComponent<VehicleUpgrade>();
            return target;
        }
    }
    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = new InteractionPromptData((promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset, "Upgrade vehicle");
        prompt.InteractionKey = InteractionKey;
        if (upgradeManager == null) upgradeManager = FindFirstObjectByType<UpgradeManager>(FindObjectsInactive.Include);
        var target = Target;
        return isActiveAndEnabled && interactor != null && target != null && target.isActiveAndEnabled && target.IsConfigured &&
            upgradeManager != null && !UpgradeManager.BlocksGameplayInput && !UIPopups.BasePopupUI.HasActivePopup;
    }
    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out _) || !upgradeManager.OpenVehicle(Target)) return false;
        var player = interactor.GetComponent<PlayerController>();
        if (player != null && player.Shovel != null) player.Shovel.CancelAction();
        return true;
    }
}
