using UnityEngine;

/// <summary>An additional action that coexists with the object's default E interaction.</summary>
public interface IKeyedInteractable : IInteractable
{
    KeyCode InteractionKey { get; }
}
