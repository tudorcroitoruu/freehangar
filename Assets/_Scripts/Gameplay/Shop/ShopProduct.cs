using UnityEngine;

public class ShopProduct : MonoBehaviour, IInteractable
{
    [SerializeField] private string productName;
    [SerializeField] private int productPrice;
    [SerializeField] private bool toBuy = true;
    [SerializeField] private bool toRent;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

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
        throw new System.NotImplementedException();
    }
}
