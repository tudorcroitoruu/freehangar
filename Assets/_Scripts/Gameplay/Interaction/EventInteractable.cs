using UnityEngine;
using UnityEngine.Events;

/// <summary>Add to an object with a collider and connect its action in the Inspector.</summary>
[DisallowMultipleComponent]
public sealed class EventInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string action = "Interact";
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .2f;
    [SerializeField] private bool available = true;
    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    public void SetAvailable(bool value) => available = value;

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = new InteractionPromptData((promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset, action);
        return isActiveAndEnabled && available && !string.IsNullOrEmpty(action);
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!TryGetPrompt(interactor, out _)) return false;
        onInteract.Invoke();
        return true;
    }
}
