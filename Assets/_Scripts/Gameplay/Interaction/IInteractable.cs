/// <summary>Implemented by world objects that the player can focus and use.</summary>
public interface IInteractable
{
    bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt);
    bool TryInteract(PlayerInteractor interactor);
}
