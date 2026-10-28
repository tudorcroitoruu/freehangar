using UnityEngine;

/// <summary>Single-use food that reduces hunger when the player interacts with it.</summary>
[DisallowMultipleComponent]
public sealed class ConsumableFood : MonoBehaviour, IInteractable
{
    [SerializeField] private string foodName = "Food";
    [SerializeField, Min(0)] private int foodPrice = 10;
    [SerializeField, Range(0f, 100f)] private float hungerReductionPercent = 20f;
    private bool consumed;

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        var stamina = GetStamina(interactor);
        bool canEat = stamina != null && stamina.isActiveAndEnabled && stamina.HungryPercent > 0f &&
            float.IsFinite(hungerReductionPercent) && hungerReductionPercent > 0f;
        bool canAfford = foodPrice >= 0 && (foodPrice == 0 || (SaveSystem.Data != null && SaveSystem.Money >= foodPrice));
        string action = $"Buy & eat {foodName}\n$ {foodPrice}";
        if (stamina != null && stamina.HungryPercent <= 0f) action += "\nNot hungry";
        else if (!canAfford) action += "\nNot enough money";
        prompt = new InteractionPromptData(transform.position + Vector3.up * .2f,
            action, canEat && canAfford);
        return isActiveAndEnabled && !consumed && stamina != null;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || consumed || foodPrice < 0 ||
            (foodPrice > 0 && (SaveSystem.Data == null || SaveSystem.Money < foodPrice))) return false;
        var stamina = GetStamina(interactor);
        if (stamina == null) return false;
        float previousHunger = stamina.HungryPercent;
        // Reserve this portion before money notifications can trigger another interaction.
        consumed = true;
        if (!stamina.ConsumeFood(hungerReductionPercent)) { consumed = false; return false; }
        if (foodPrice > 0 && !SaveSystem.SpendMoney(foodPrice))
        {
            stamina.SetHungerPercent(previousHunger);
            consumed = false;
            return false;
        }
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    private static StaminaManager GetStamina(PlayerInteractor interactor)
    {
        if (interactor == null) return null;
        var player = interactor.GetComponent<PlayerController>();
        return player != null ? player.Stamina : null;
    }
}
