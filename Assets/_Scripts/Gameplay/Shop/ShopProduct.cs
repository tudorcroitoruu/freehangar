using UnityEngine;

public class ShopProduct : MonoBehaviour, IInteractable
{
    [SerializeField] private string productID;
    [SerializeField] private string productName;
    [SerializeField, Min(0)] private int productPrice;
    [SerializeField] private bool toBuy = true;
    [SerializeField] private bool toRent;

    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || (!toBuy && !toRent)) return false;

        string action = toBuy ? "Buy" : "Rent";
        prompt = new InteractionPromptData(
            transform.position + Vector3.up * .5f,
            $"{action} {productName}\n$ {productPrice}");
        return true;
    }

    public bool TryInteract(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || !toBuy) return false;
        return SaveSystem.TryBuyProduct(productID, productPrice);
    }
}
