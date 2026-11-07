using UnityEngine;

[DisallowMultipleComponent]
public class FridgeProduct : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Vector3 promptOffset = Vector3.up * .3f;
    private bool purchasing;

    private StaminaManager Stamina => GameplayCanvas.Instance != null ? GameplayCanvas.Instance.staminaManager : null;

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        var stamina = Stamina;
        // Hunger accrues fractionally every frame. Do not sell another meal for
        // the first tiny increment immediately after the player has eaten.
        bool needsFood = stamina != null && stamina.HungryPercent >= 1f;
        int foodPrice = needsFood ? Mathf.CeilToInt(stamina.HungryPercent) : 0;
        bool canAfford = SaveSystem.Data != null && SaveSystem.Money >= foodPrice;
        string price = $"${foodPrice}";
        if (!canAfford) price = $"<color=#D92D2D>{price}</color>";
        prompt = new InteractionPromptData(
            (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset,
            "Eat & reduce hunger", needsFood && canAfford && !purchasing)
        {
            SecondaryDescription = needsFood ? $"Price: {price}" : "",
            NotHungry = !needsFood
        };
        return isActiveAndEnabled && interactor != null && stamina != null && stamina.isActiveAndEnabled;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (purchasing || Time.timeScale <= 0f || !TryGetPrompt(interactor, out var prompt) || !prompt.ShowKey)
            return false;
        var stamina = Stamina;
        int foodPrice = Mathf.CeilToInt(stamina.HungryPercent);
        purchasing = true;
        try
        {
            if (foodPrice > 0 && !SaveSystem.SpendMoney(foodPrice)) return false;
            stamina.RestoreSave(stamina.MaxStamina, 0f);
            GameplayAudio.Instance?.Eat();
            return true;
        }
        finally { purchasing = false; }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 anchor = promptAnchor != null ? promptAnchor.position : transform.position;
        Vector3 position = anchor + promptOffset;
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(anchor, position);
        Gizmos.DrawWireSphere(position, .08f);
        Gizmos.color = previousColor;
#if UNITY_EDITOR
        UnityEditor.Handles.Label(position + Vector3.up * .12f, "Fridge prompt (promptOffset)");
#endif
    }
}
