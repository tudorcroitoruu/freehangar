using TMPro;
using UnityEngine;

/// <summary>Numbered hangar keys unlock matching sliding doors and their grain simulation.</summary>
public sealed class HangarKeyDoor : MonoBehaviour, IInteractable
{
    [SerializeField, Range(2, 5)] private int hangarNumber = 2;
    [SerializeField] private HangarGrainVisibility grainVisibility;
    [SerializeField] private Transform leftDoor, rightDoor;
    [SerializeField, Min(.1f)] private float slideDistance = 5.5f;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text overheadLabel;
    [SerializeField] private DiscoveryBurst unlockEffect;
    private Vector3 leftClosed, rightClosed;
    private PlayerData loadedProfile;
    private float progress;
    public int HangarNumber => hangarNumber;
    public bool IsUnlocked { get; private set; }
    public string KeyId => KeyFor(hangarNumber);
    public static string KeyFor(int number) => "farm.hangar.key." + number;
    public static string KeyColor(int number) => number == 2 ? "orange" : number == 3 ? "blue" : number == 4 ? "green" : "gold";
    private void Awake()
    {
        if (leftDoor != null) leftClosed = leftDoor.localPosition;
        if (rightDoor != null) rightClosed = rightDoor.localPosition;
    }
    private void Update()
    {
        if (SaveSystem.savesLoaded && SaveSystem.Data != null && loadedProfile != SaveSystem.Data)
        {
            loadedProfile = SaveSystem.Data;
            IsUnlocked = loadedProfile.IsHangarOpen(hangarNumber);
            progress = IsUnlocked ? 1 : 0;
            if (grainVisibility != null) grainVisibility.SetHangarOpen(IsUnlocked);
            RefreshLabel();
        }
        if (IsUnlocked && progress < 1) progress = Mathf.MoveTowards(progress, 1, Time.deltaTime / 1.6f);
        float slide = Mathf.SmoothStep(0, 1, progress) * slideDistance;
        if (leftDoor != null) leftDoor.localPosition = leftClosed - Vector3.right * slide;
        if (rightDoor != null) rightDoor.localPosition = rightClosed + Vector3.right * slide;
    }
    public bool TryGetPrompt(PlayerInteractor interactor, out InteractionPromptData prompt)
    {
        prompt = default;
        if (!isActiveAndEnabled || IsUnlocked) return false;
        bool key = SaveSystem.savesLoaded && BuriedFind.WasFound(SaveSystem.Data, KeyId);
        prompt = new InteractionPromptData(transform.position + Vector3.up * .6f,
            key ? $"Unlock HANGAR {hangarNumber}" : $"HANGAR {hangarNumber} • Find its {KeyColor(hangarNumber)} key in Hangar {hangarNumber - 1}", key);
        return true;
    }
    public bool TryInteract(PlayerInteractor interactor)
    {
        if (Time.timeScale <= 0 || !TryGetPrompt(interactor, out var prompt) || !prompt.ShowKey) return false;
        var data = SaveSystem.Data;
        if (data == null) return false;
        if (!SaveSystem.TryOpenHangar(hangarNumber)) return false;
        loadedProfile = data; IsUnlocked = true;
        if (grainVisibility != null) grainVisibility.SetHangarOpen(true);
        RefreshLabel();
        DiscoveryBurst.Play(unlockEffect, transform.position, $"HANGAR {hangarNumber} UNLOCKED", interactor != null ? interactor.GetComponentInChildren<Camera>() : null);
        GoalChecker.ReportProgress(GoalType.HangarUnlocked, 1, hangarNumber);
        return true;
    }
    private void RefreshLabel()
    {
        if (TryGetComponent<Collider>(out var lockCollider)) lockCollider.enabled = !IsUnlocked;
        if (label != null) label.text = $"HANGAR {hangarNumber}\n" + (IsUnlocked ? "OPEN" : $"{KeyColor(hangarNumber).ToUpperInvariant()} KEY REQUIRED");
        if (overheadLabel != null) overheadLabel.text = IsUnlocked ? "OPEN" : "LOCKED";
    }
}
