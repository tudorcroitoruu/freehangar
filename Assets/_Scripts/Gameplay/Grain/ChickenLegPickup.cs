using UnityEngine;

public sealed class ChickenLegPickup : MonoBehaviour, IInteractable
{
    private const float SpinDegreesPerSecond = 90f;
    [SerializeField, Min(0f)] private float hungerReductionPercent = 15f;
    [SerializeField, Min(0f)] private float hungerReductionDuration = 1f;
    [SerializeField] private Vector3 mouthGripPoint;
    [SerializeField] private Vector3 mouthEulerAngles;
    private Vector3 uncarriedScale;
    private Quaternion uncarriedRotation;
    private bool collected;
    private bool carried;
    private float expiresAt;
    private void Start() => expiresAt = Time.time + 60f;
    private void Update()
    {
        if (carried || collected) return;
        if (Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        transform.Rotate(Vector3.up, SpinDegreesPerSecond * Time.deltaTime, Space.World);
    }

    public bool Carry(Transform mouth)
    {
        if (collected || carried || mouth == null) return false;
        uncarriedScale = transform.lossyScale;
        uncarriedRotation = transform.rotation;
        carried = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        transform.SetParent(mouth, false);
        Vector3 parentScale = mouth.lossyScale;
        transform.localScale = new Vector3(uncarriedScale.x / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
            uncarriedScale.y / Mathf.Max(.001f, Mathf.Abs(parentScale.y)),
            uncarriedScale.z / Mathf.Max(.001f, Mathf.Abs(parentScale.z)));
        transform.localRotation = Quaternion.Euler(mouthEulerAngles);
        transform.localPosition = -(transform.localRotation * Vector3.Scale(mouthGripPoint, transform.localScale));
        return true;
    }
    public void PutDown(Vector3 position)
    {
        transform.SetParent(null, true);
        if (carried)
        {
            transform.localScale = uncarriedScale;
            transform.rotation = uncarriedRotation;
        }
        transform.position = position;
        carried = false;
        expiresAt = Time.time + 60f;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
        var outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = Color.yellow;
        outline.OutlineWidth = 4f;
        outline.enabled = true;
    }
    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = new InteractionPromptData(transform.position + Vector3.up*.2f, "Eat chicken leg — restore stamina");
        return !collected && !carried && interactor != null;
    }
    public bool TryInteract(PlayerInteractor interactor)
    {
        if (collected || carried || interactor == null || Time.timeScale <= 0) return false;
        var stamina = GameplayCanvas.Instance != null ? GameplayCanvas.Instance.staminaManager : null;
        if (stamina == null) return false;
        collected = true;
        stamina.RestoreStaminaAndReduceHunger(hungerReductionPercent, hungerReductionDuration);
        GameplayAudio.Instance?.Eat();
        Destroy(gameObject);
        return true;
    }
}
