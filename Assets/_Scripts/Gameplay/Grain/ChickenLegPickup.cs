using UnityEngine;

public sealed class ChickenLegPickup : MonoBehaviour, IInteractable
{
    private bool collected;
    private void Start() => Destroy(gameObject, 60f);
    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = new InteractionPromptData(transform.position + Vector3.up*.2f, "Eat chicken leg — restore stamina");
        return !collected && interactor != null;
    }
    public bool TryInteract(PlayerInteractor interactor)
    {
        if (collected || interactor == null || Time.timeScale <= 0) return false;
        var stamina = interactor.GetComponent<StaminaManager>();
        if (stamina == null) return false;
        collected = true;
        stamina.RestoreSave(stamina.CurrentMaxStamina, stamina.HungryPercent);
        Destroy(gameObject);
        return true;
    }
}
