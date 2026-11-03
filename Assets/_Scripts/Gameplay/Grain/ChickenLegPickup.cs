using UnityEngine;

public sealed class ChickenLegPickup : MonoBehaviour, IInteractable
{
    private bool collected;
    private bool carried;
    private float expiresAt;
    private void Start() => expiresAt = Time.time + 60f;
    private void Update() { if (!carried && Time.time >= expiresAt) Destroy(gameObject); }
    public bool Carry(Transform mouth)
    {
        if (collected || carried || mouth == null) return false;
        carried = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        transform.SetParent(mouth, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        return true;
    }
    public void PutDown(Vector3 position)
    {
        transform.SetParent(null, true);
        transform.position = position;
        carried = false;
        expiresAt = Time.time + 60f;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
    }
    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = new InteractionPromptData(transform.position + Vector3.up*.2f, "Eat chicken leg — restore stamina");
        return !collected && !carried && interactor != null;
    }
    public bool TryInteract(PlayerInteractor interactor)
    {
        if (collected || carried || interactor == null || Time.timeScale <= 0) return false;
        var stamina = interactor.GetComponent<StaminaManager>();
        if (stamina == null) return false;
        collected = true;
        stamina.RestoreSave(stamina.CurrentMaxStamina, stamina.HungryPercent);
        Destroy(gameObject);
        return true;
    }
}
