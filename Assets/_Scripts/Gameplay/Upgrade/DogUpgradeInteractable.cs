using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(FarmDog))]
public sealed class DogUpgradeInteractable : MonoBehaviour, IKeyedInteractable
{
    public DogUpgradeSettings settings;
    private FarmDog dog;
    public KeyCode InteractionKey => KeyCode.F;
    private void Awake() => dog = GetComponent<FarmDog>();
    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        if (!dog) dog = GetComponent<FarmDog>();
        int level = dog.Level;
        bool voucher = (SaveSystem.Data?.freeUpgradeVouchers ?? 0) > 0;
        int price = settings ? settings.Price(level) : 0;
        string cost = voucher ? "Free voucher" : "$" + EconomyNumberFormat.Accounting(price);
        if (!voucher && (SaveSystem.Data == null || price < 0 || SaveSystem.Money < price))
            cost = $"<color=#D92D2D>{cost}</color>";
        prompt = new InteractionPromptData(transform.position + Vector3.up,
            "Upgrade dog to level " + (level + 1));
        prompt.SecondaryDescription = "Price: " + cost;
        prompt.InteractionKey = KeyCode.F;
        return isActiveAndEnabled && interactor != null && settings && level < 5 && dog.CanPet &&
            !SaveSystem.UpgradePurchaseLocked && SaveSystem.savesLoaded;
    }
    public bool TryInteract(PlayerInteractor interactor)
    {
        if (interactor == null || interactor.HandsOccupied || Time.timeScale <= 0 || !TryGetPrompt(interactor, out _)) return false;
        bool purchased = SaveSystem.TryPurchaseDogUpgrade(settings.Price(dog.Level), dog.Level);
        GameplayAudio.Instance?.UpgradeResult(purchased);
        if (purchased) SaveSystem.SaveData();
        return purchased;
    }
}
