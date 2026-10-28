using UnityEngine;

/// <summary>World pickup for inventory prefabs without a GrainContainer.</summary>
[DisallowMultipleComponent]
public sealed class CarryableItem : MonoBehaviour, IInteractable
{
    [SerializeField] private bool canBeCarried = true;
    public bool CanBeCarried => canBeCarried;

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = new InteractionPromptData(transform.position + Vector3.up * .5f, "Take",
            interactor != null && interactor.Items != null && interactor.Items.CanTake);
        prompt.RequiresFreeHands = !prompt.ShowKey && interactor != null && interactor.HandsOccupied;
        return isActiveAndEnabled && canBeCarried;
    }

    public bool TryInteract(PlayerInteractor interactor) => isActiveAndEnabled && canBeCarried &&
        interactor != null && interactor.Items != null && interactor.Items.TryTake(gameObject);
}
