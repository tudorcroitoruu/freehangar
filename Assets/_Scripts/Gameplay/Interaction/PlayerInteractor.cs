using UnityEngine;
using System.Collections.Generic;

/// <summary>Resolves the nearest visible interactable and owns its shared prompt.</summary>
[DefaultExecutionOrder(300), DisallowMultipleComponent]
public sealed class PlayerInteractor : MonoBehaviour
{
    private readonly List<MonoBehaviour> targetBehaviours = new List<MonoBehaviour>(16);
    private readonly List<MonoBehaviour> actionBehaviours = new List<MonoBehaviour>(16);
    [SerializeField] private Camera viewCamera;
    [SerializeField, Min(.1f)] private float interactionDistance = 4f;
    [SerializeField] private LayerMask interactionMask = ~0;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private PlayerController player;
    private PlayerBicycleInteraction bicycle;
    private InteractionHighlight highlight;
    private CombatToolUpgradeInteractable focusedToolDesk;
    private ShopProduct focusedLockedBag;
    private BuriedFind focusedLockedChest;
    private Transform actionOrigin;
    public BagInteraction Bags { get; private set; }
    public PlayerItemCarry Items { get; private set; }
    public WheelbarrowCargo PushedWheelbarrow { get; set; }
    public IInteractable Target { get; private set; }
    private Object discoveryOwner;
    private VehicleReset resetTarget;
    private GrainContainer emptyBagTarget;
    private float emptyBagHeld;
    private bool emptyBagHoldCompleted;
    private bool emptyBagSucceeded;
    public bool DiscoveryBusy => discoveryOwner != null;
    public bool BeginDiscovery(Object owner)
    {
        if (owner == null || DiscoveryBusy) return false;
        discoveryOwner = owner; return true;
    }
    public void EndDiscovery(Object owner) { if (discoveryOwner == owner) discoveryOwner = null; }
    public bool HandsOccupied => (GetComponent<PlayerController>() is PlayerController actor &&
        actor.IsCarrying) || (Bags != null && Bags.IsCarrying) ||
        (Items != null && Items.IsCarrying) || PushedWheelbarrow != null;

    private InteractionPrompt Prompt => GameplayCanvas.Instance != null ? GameplayCanvas.Instance.interactionPrompt : null;

    private bool CanInteract => !LoadingScreen.IsLoading && !UpgradeManager.BlocksGameplayInput && !DiscoveryBusy && isActiveAndEnabled && viewCamera != null && viewCamera.isActiveAndEnabled &&
        Time.timeScale > 0 && Application.isFocused && Cursor.lockState == CursorLockMode.Locked && !UIPopups.BasePopupUI.HasActivePopup &&
        (player == null || (player.isActiveAndEnabled && player.characterController != null && player.characterController.enabled)) &&
        (bicycle == null || !bicycle.IsRiding);

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        bicycle = GetComponent<PlayerBicycleInteraction>();
        Bags = GetComponent<BagInteraction>();
        Items = GetComponent<PlayerItemCarry>();
        if (Items == null && player != null) Items = gameObject.AddComponent<PlayerItemCarry>();
        if (viewCamera == null && player != null) viewCamera = player.characterCamera;
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
    }

    private void LateUpdate()
    {
        var prompt = Prompt;
        Target = null;
        if (!CanInteract) CancelBagHold();
        if (!CanInteract) { SetWeighingWarnings(false, false); SetNotHungryWarning(false); SetNotFitInWheelBarrowWarning(false); SetCannotLoadInWheelbarrowWarning(false); SetNeedKeyForChestWarning(null); SetDogRestingWarning(false); SetVehicleFullWarning(false); SetUseForkliftWarning(false); SetResetTarget(null); SetFreeHandsWarning(false); SetHighlightedTarget(null); HidePrompt(); return; }
        var aimedTarget = FindTarget();
        TickBagHold(aimedTarget);
        InteractionPromptData aimedPrompt = default;
        bool hasAimedPrompt = aimedTarget != null && aimedTarget.TryGetPrompt(this, out aimedPrompt);
        if (hasAimedPrompt && emptyBagTarget != null && ReferenceEquals(aimedTarget, emptyBagTarget))
            aimedPrompt.SecondaryDescription = emptyBagHoldCompleted
                ? emptyBagSucceeded ? "Bag emptied" : "No clear ground beside bag"
                : $"Hold {FarmInput.Label("Interact", "E")} to empty bag ({emptyBagHeld:0.0} / 1.0 s)";
        // Driving owns the interaction prompt. A carried bag also prevents taking
        // another bag; looking at cargo should not continuously warn about that.
        if (PushedWheelbarrow != null ||
            (Bags != null && Bags.IsCarrying && aimedTarget is GrainContainer))
        {
            hasAimedPrompt = false;
            aimedPrompt = default;
        }
        SetDogRestingWarning(hasAimedPrompt && aimedPrompt.DogResting);
        SetVehicleFullWarning(hasAimedPrompt && aimedPrompt.VehicleFull);
        SetNotFitInWheelBarrowWarning(hasAimedPrompt && aimedPrompt.NotFitInWheelBarrow);
        SetCannotLoadInWheelbarrowWarning(hasAimedPrompt && aimedPrompt.CannotLoadInWheelbarrow);
        SetNeedKeyForChestWarning(hasAimedPrompt && aimedPrompt.NeedKeyForChest ? aimedTarget as BuriedFind : null);
        SetNotHungryWarning(hasAimedPrompt && aimedPrompt.NotHungry);
        SetWeighingWarnings(hasAimedPrompt && aimedPrompt.WeighingEmpty, hasAimedPrompt && aimedPrompt.WeighingWrong);
        SetUseForkliftWarning(hasAimedPrompt && aimedPrompt.UseForkliftToMove);
        UpdateResetTarget(hasAimedPrompt && aimedPrompt.ShowKey && !aimedPrompt.RequiresFreeHands ? aimedTarget : null);
        if (hasAimedPrompt)
        {
            AddActions(aimedTarget, ref aimedPrompt);
            if (aimedTarget is BicycleController) AddResetAction(ref aimedPrompt);
        }
        SetFreeHandsWarning(hasAimedPrompt && aimedPrompt.RequiresFreeHands);
        // Being full blocks eating, not independent actions such as stamina upgrades.
        bool hungerBlocksPrompt = aimedPrompt.NotHungry && !aimedPrompt.HasAvailableInput;
        if (hasAimedPrompt && (aimedPrompt.RequiresFreeHands || aimedPrompt.VehicleFull || aimedPrompt.NotFitInWheelBarrow || aimedPrompt.CannotLoadInWheelbarrow || aimedPrompt.NeedKeyForChest || hungerBlocksPrompt || aimedPrompt.WeighingEmpty || aimedPrompt.WeighingWrong || aimedPrompt.UseForkliftToMove || aimedPrompt.DogResting))
        {
            Target = aimedTarget;
            SetHighlightedTarget(aimedTarget is SellingWeighButton ? aimedTarget : null);
            HidePrompt();
            return;
        }
        if (Items != null && Items.IsCarrying)
        {
            SetHighlightedTarget(null);
            if (aimedTarget is WheelbarrowCargo && hasAimedPrompt && prompt != null) prompt.Show(viewCamera, aimedPrompt);
            else HidePrompt();
            return;
        }
        if (PushedWheelbarrow != null)
        {
            SetHighlightedTarget(null);
            if (PushedWheelbarrow.TryGetPrompt(this, out var pushPrompt) && prompt != null)
            {
                AddResetAction(ref pushPrompt);
                prompt.Show(viewCamera, pushPrompt);
            }
            else HidePrompt();
            return;
        }
        if (Bags != null && Bags.isActiveAndEnabled && Bags.IsCarrying)
        {
            SetHighlightedTarget(null);
            if ((aimedTarget is WheelbarrowCargo || aimedTarget is BicycleController) &&
                aimedTarget.TryGetPrompt(this, out var loadPrompt))
            {
                if (prompt != null) prompt.Show(viewCamera, loadPrompt);
                return;
            }
            HidePrompt();
            return;
        }
        Target = aimedTarget;
        if (hasAimedPrompt)
        {
            SetHighlightedTarget(Target);
            if (focusedToolDesk != null || Target is ShopProduct product && product.Locked) HidePrompt();
            else if (prompt != null) prompt.Show(viewCamera, aimedPrompt);
        }
        else { SetHighlightedTarget(null); HidePrompt(); }
    }

    public bool TryInteract()
    {
        if (!CanInteract) return false;
        if (Items != null && Items.IsCarrying)
            return FindTarget() is WheelbarrowCargo barrow ? barrow.TryInteract(this) : Items.TryPlace();
        if (PushedWheelbarrow != null) return PushedWheelbarrow.TryInteract(this);
        // Carrying reserves E for dropping; bicycle cargo handling takes priority in PlayerController.
        if (Bags != null && Bags.isActiveAndEnabled && Bags.IsCarrying)
        {
            var wheelbarrow = FindTarget() as WheelbarrowCargo;
            if (wheelbarrow != null)
                return wheelbarrow.TryGetPrompt(this, out var loadPrompt) && loadPrompt.ShowKey &&
                    wheelbarrow.TryInteract(this);
            return Bags.TryInteract();
        }
        Target = FindTarget();
        if (!HandsOccupied && Target is GrainContainer bag && Bags != null && Bags.CanEmpty(bag))
        {
            emptyBagTarget = bag;
            emptyBagHeld = 0f;
            emptyBagHoldCompleted = false;
            return true;
        }
        return Target != null && KeyFor(Target) == KeyCode.E && Target.TryGetPrompt(this, out var data) && data.ShowKey && Target.TryInteract(this);
    }

    private void CancelBagHold()
    {
        emptyBagTarget = null;
        emptyBagHeld = 0f;
        emptyBagHoldCompleted = false;
        emptyBagSucceeded = false;
    }

    private void TickBagHold(IInteractable aimed)
    {
        if (emptyBagTarget == null) return;
        if (!ReferenceEquals(aimed, emptyBagTarget) || HandsOccupied || Bags == null)
        {
            CancelBagHold();
            return;
        }
        if (!FarmInput.Held("Interact"))
        {
            var bag = emptyBagTarget;
            bool tap = !emptyBagHoldCompleted;
            CancelBagHold();
            if (tap) Bags.TryInteract(bag);
            return;
        }
        if (emptyBagHoldCompleted) return;
        if (!Bags.CanEmpty(emptyBagTarget)) { CancelBagHold(); return; }
        emptyBagHeld += Time.deltaTime;
        if (emptyBagHeld < 1f) return;
        emptyBagHoldCompleted = true;
        emptyBagSucceeded = Bags.TryEmpty(emptyBagTarget);
    }

    public bool TryAdditionalInput()
    {
        if (!CanInteract || (!FarmInput.Down("Secondary") && !FarmInput.Down("Cargo"))) return false;
        if (PushedWheelbarrow != null) return false;
        var primary = FindTarget();
        if (!(primary is Component component)) return false;
        foreach (var behaviour in ActionBehaviours(component))
            if (behaviour != null && behaviour.isActiveAndEnabled && behaviour is IKeyedInteractable action &&
                action.InteractionKey != KeyCode.E && action.InteractionKey != KeyCode.None && FarmInput.ContextDown(action.InteractionKey))
                return TryInteract(action.InteractionKey);
        return false;
    }

    public bool TryInteract(KeyCode key)
    {
        if (key == KeyCode.E) return TryInteract();
        if (!CanInteract || (Items != null && Items.IsCarrying) || (Bags != null && Bags.IsCarrying)) return false;
        if (PushedWheelbarrow != null) return false;
        var primary = FindTarget();
        if (!(primary is Component component)) return false;
        foreach (var behaviour in ActionBehaviours(component))
            if (behaviour != null && behaviour.isActiveAndEnabled && behaviour is IKeyedInteractable action && action.InteractionKey == key &&
                action.TryGetPrompt(this, out var data) && data.ShowKey && !data.RequiresFreeHands)
                return action.TryInteract(this);
        return false;
    }

    private static KeyCode KeyFor(IInteractable target) => target is IKeyedInteractable action ? action.InteractionKey : KeyCode.E;

    private void AddActions(IInteractable primary, ref InteractionPromptData data)
    {
        data.InteractionKey = KeyFor(primary);
        if (!(primary is Component component) || HandsOccupied) return;
        var actions = new List<InteractionPromptAction>();
        foreach (var behaviour in ActionBehaviours(component))
        {
            if (behaviour == null || !behaviour.isActiveAndEnabled || !(behaviour is IKeyedInteractable action) ||
                ReferenceEquals(primary, action) || action.InteractionKey == data.InteractionKey ||
                !action.TryGetPrompt(this, out var extra) || !extra.ShowKey || extra.RequiresFreeHands) continue;
            if (actions.Exists(item => item.Key == action.InteractionKey)) continue;
            actions.Add(new InteractionPromptAction(extra.Action, action.InteractionKey)
            {
                SecondaryDescription = extra.SecondaryDescription
            });
        }
        data.AdditionalActions = actions.Count > 0 ? actions.ToArray() : null;
    }

    private void SetResetTarget(VehicleReset next)
    {
        if (resetTarget == next) return;
        if (resetTarget != null) resetTarget.CancelHold();
        resetTarget = next;
    }

    private void UpdateResetTarget(IInteractable aimedTarget)
    {
        VehicleReset next = null;
        if (PushedWheelbarrow != null && PushedWheelbarrow.IsBeingPushed) next = PushedWheelbarrow.GetComponent<VehicleReset>();
        else if (!HandsOccupied && aimedTarget is BicycleController vehicle && !vehicle.IsRidden)
            next = vehicle.GetComponent<VehicleReset>();
        SetResetTarget(next);
        if (resetTarget != null) resetTarget.TickHold(FarmInput.Held("ResetVehicle"), Time.deltaTime);
    }

    private void AddResetAction(ref InteractionPromptData data)
    {
        if (resetTarget == null || !resetTarget.CanReset) return;
        var existing = data.AdditionalActions;
        int count = existing != null ? existing.Length : 0;
        var actions = new InteractionPromptAction[count + 1];
        if (count > 0) System.Array.Copy(existing, actions, count);
        string action = resetTarget.Progress > 0
            ? $"Resetting {resetTarget.HeldSeconds:0.0} / {resetTarget.HoldSeconds:0.0} s"
            : "Hold to reset position";
        // Reset feedback is text; bagSliderFill shows container or cargo filling.
        actions[count] = new InteractionPromptAction(action, KeyCode.R);
        data.AdditionalActions = actions;
    }

    private IEnumerable<MonoBehaviour> ActionBehaviours(Component primary)
    {
        var origin = actionOrigin != null && actionOrigin.IsChildOf(primary.transform) ? actionOrigin : primary.transform;
        for (var host = origin; host != null; host = host.parent)
        {
            host.GetComponents(actionBehaviours);
            for (int i = 0; i < actionBehaviours.Count; i++) yield return actionBehaviours[i];
            if (host == primary.transform) yield break;
        }
    }

    private IInteractable FindTarget()
    {
        actionOrigin = null;
        // A loaded bag belongs to its vehicle while carrying another bag. Use
        // exactly the same forgiving aim query as the E loading action.
        if (bicycle != null && bicycle.TryFindLoadingVehicle(out var loadingVehicle))
        {
            actionOrigin = loadingVehicle.transform;
            return loadingVehicle;
        }
        int count = Physics.RaycastNonAlloc(PlayerAim.CenterRay(viewCamera), hits, interactionDistance,
            interactionMask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) return null;
        Collider closest = null;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].transform.IsChildOf(transform) || hits[i].distance >= distance) continue;
            closest = hits[i].collider;
            distance = hits[i].distance;
        }
        // Resolve only the first obstruction, including interactables on a collider's parent.
        if (closest == null) return null;
        // Empty sacks sit below the vehicle's broad collision envelope. Allow a
        // ray that actually hits attached cargo to pass only that owner's body.
        var vehicle = VehicleCargoOwner.Resolve(closest);
        if (vehicle != null && closest.GetComponentInParent<GrainContainer>() == null)
        {
            Collider cargoHit = null;
            float cargoDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var bag = hits[i].collider.GetComponentInParent<GrainContainer>();
                if (bag == null || !vehicle.HasCargo(bag.transform) || hits[i].distance >= cargoDistance) continue;
                cargoHit = hits[i].collider;
                cargoDistance = hits[i].distance;
            }
            if (cargoHit != null)
            {
                bool blocked = false;
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (hit.transform.IsChildOf(transform) || hit.distance >= cargoDistance) continue;
                    if (VehicleCargoOwner.Resolve(hit.collider) != vehicle ||
                        hit.collider.GetComponentInParent<GrainContainer>() != null) { blocked = true; break; }
                }
                if (!blocked) closest = cargoHit;
            }
        }
        var cargoOwner = closest.GetComponentInParent<VehicleCargoOwner>();
        if (cargoOwner != null && cargoOwner.vehicle != null && cargoOwner.vehicle.isActiveAndEnabled &&
            closest.GetComponentInParent<GrainContainer>() == null)
        {
            actionOrigin = cargoOwner.vehicle.transform;
            return cargoOwner.vehicle;
        }
        actionOrigin = closest.transform;
        // Prefer E on the nearest interaction host; other keys are shown beside it.
        // Never skip an obstruction to reach an interaction farther behind it.
        IInteractable fallback = null;
        for (Transform host = closest.transform; host != null; host = host.parent)
        {
            bool foundDefault = false;
            host.GetComponents(targetBehaviours);
            foreach (var behaviour in targetBehaviours)
            {
                if (!(behaviour is IInteractable candidate)) continue;
                if (KeyFor(candidate) == KeyCode.E) foundDefault = true;
                if (!behaviour.isActiveAndEnabled) continue;
                if (KeyFor(candidate) == KeyCode.E) return candidate;
                if (fallback == null) fallback = candidate;
            }
            if (foundDefault) return fallback;
        }
        return fallback;
    }

    private void HidePrompt()
    {
        var prompt = Prompt;
        if (prompt != null) prompt.Hide();
    }

    private void SetHighlightedTarget(IInteractable target)
    {
        var next = target is Component component && component != null ? component.GetComponent<InteractionHighlight>() : null;
        var desk = target as CombatToolUpgradeInteractable;
        var hud = GameplayCanvas.Instance;
        if (hud != null && (desk != focusedToolDesk || focusedToolDesk != null && focusedToolDesk.Found))
        {
            if (focusedToolDesk != null) hud.SetWarning(focusedToolDesk.FindWarning, this, false);
            focusedToolDesk = desk != null && !desk.Found ? desk : null;
            if (focusedToolDesk != null) hud.SetWarning(focusedToolDesk.FindWarning, this, true);
        }
        var bag = target as ShopProduct;
        if (bag != null && (!bag.ShowWhileLocked || !bag.Locked)) bag = null;
        if (hud != null && focusedLockedBag != bag)
        {
            if (focusedLockedBag) hud.SetWarning(WarningType.LockedBag, focusedLockedBag, false);
            focusedLockedBag = bag;
            if (bag) hud.SetWarning(WarningType.LockedBag, bag, true);
        }
        if (highlight != next)
        {
            if (highlight != null) highlight.SetFocused(false);
            highlight = next;
        }
        if (highlight != null) highlight.SetFocused(true);
    }

    private void SetFreeHandsWarning(bool visible)
    {
        if (GameplayCanvas.Instance != null) GameplayCanvas.Instance.SetFreeHandsWarning(visible);
    }

    private void SetVehicleFullWarning(bool visible) =>
        GameplayCanvas.Instance?.SetWarning(WarningType.VehicleFull, this, visible);

    private void SetNotFitInWheelBarrowWarning(bool visible) =>
        GameplayCanvas.Instance?.SetWarning(WarningType.notFitInWheelBarrow, this, visible);

    private void SetCannotLoadInWheelbarrowWarning(bool visible) =>
        GameplayCanvas.Instance?.SetWarning(WarningType.cannotLoadInWheelbarrow, this, visible);

    private void SetNeedKeyForChestWarning(BuriedFind chest)
    {
        var hud = GameplayCanvas.Instance;
        if (hud == null || focusedLockedChest == chest) return;
        if (focusedLockedChest != null) hud.SetWarning(WarningType.needKeyForChest, focusedLockedChest, false);
        focusedLockedChest = chest;
        if (chest != null) hud.SetWarning(WarningType.needKeyForChest, chest, true);
    }

    private void SetNotHungryWarning(bool visible) =>
        GameplayCanvas.Instance?.SetWarning(WarningType.notHungry, this, visible);

    private void SetWeighingWarnings(bool empty, bool wrong)
    {
        GameplayCanvas.Instance?.SetWarning(WarningType.weighingEmpty, this, empty);
        GameplayCanvas.Instance?.SetWarning(WarningType.weighingWrong, this, wrong && !HandsOccupied);
    }

    private void SetUseForkliftWarning(bool visible) =>
        GameplayCanvas.Instance?.SetWarning(WarningType.UseForkliftToMove, this, visible);

    private void SetDogRestingWarning(bool visible) =>
        GameplayCanvas.Instance?.SetWarning(WarningType.DogResting, this, visible);

    private void OnDisable() { CancelBagHold(); SetWeighingWarnings(false, false); SetNotHungryWarning(false); SetNotFitInWheelBarrowWarning(false); SetCannotLoadInWheelbarrowWarning(false); SetNeedKeyForChestWarning(null); SetDogRestingWarning(false); SetVehicleFullWarning(false); SetUseForkliftWarning(false); SetResetTarget(null); Target = null; SetFreeHandsWarning(false); SetHighlightedTarget(null); HidePrompt(); }
}
