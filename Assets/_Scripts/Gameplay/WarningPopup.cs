using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum WarningType
{
    RatsAttack,
    CrowsAttack,
    BlockHangarEntry,
    VehicleFull,
    BlockVehicleExit,
    OccupiedHands,
    OccupiedArea,
    UseForkliftToMove,
    NewContainerOpened,
    FindAxe,
    FindHammer,
    LockedBag,
    DogResting,
    hangarCleared,
    notFitInWheelBarrow,
    mixGrainNotAllowedInShovel,
    mixGrainNotAllowedInContainer,
    notHungry,
    weighingEmpty,
    weighingWrong,
    cannotLoadInWheelbarrow,
    needKeyForChest,
    hangarLocked
}
public class WarningPopup : MonoBehaviour
{
    [SerializeField] private WarningPresets[] warningPresets;
    [Space]
    [SerializeField] private CanvasGroup warningCanvasGroup;
    [SerializeField] private Image warningImage;
    [SerializeField] private Text warningTitle;
    [SerializeField] private TMP_Text warningMessage;
    [SerializeField, Min(.1f)] private float rotationSeconds = 3f;
    [SerializeField, Min(0f)] private float fadeSeconds = .2f;

    public sealed class Request
    {
        public WarningType type;
        public UnityEngine.Object source;
        public bool timed;
        public float remaining;
    }

    private readonly List<Request> requests = new List<Request>();
    private readonly List<WarningType> activeTypes = new List<WarningType>();
    public WarningType? CurrentType { get; private set; }
    private float shownSeconds;

    public void Initialize()
    {
        if (warningCanvasGroup == null) return;
        warningCanvasGroup.alpha = 0f;
        warningCanvasGroup.interactable = false;
        warningCanvasGroup.blocksRaycasts = false;
    }

    // Kept for existing Inspector events. Repeated requests extend one notification.
    public void ShopWarning(WarningType type) => ShowWarning(type);

    public void ShowWarning(WarningType type, float seconds = 2f)
    {
        var request = requests.Find(item => item.type == type && item.timed);
        if (request == null)
        {
            request = new Request { type = type, timed = true };
            requests.Add(request);
            PreferContextWarning(type);
        }
        request.remaining = Mathf.Max(.1f, seconds);
    }

    public void SetWarning(WarningType type, UnityEngine.Object source, bool visible)
    {
        if (source == null) return;
        var request = requests.Find(item => item.type == type && !item.timed && item.source == source);
        if (!visible)
        {
            if (request != null) requests.Remove(request);
            return;
        }
        if (request != null) return;
        bool alreadyActive = requests.Exists(item => item.type == type);
        requests.Add(new Request { type = type, source = source });
        if (!alreadyActive) PreferContextWarning(type);
    }

    private void PreferContextWarning(WarningType type)
    {
        if (type != WarningType.RatsAttack && type != WarningType.CrowsAttack) Display(type);
    }

    // Driven by GameplayCanvas, including when the authored popup starts inactive.
    public void Tick(float deltaTime)
    {
        deltaTime = Mathf.Max(0f, deltaTime);
        for (int i = requests.Count - 1; i >= 0; i--)
        {
            var request = requests[i];
            if (request.timed) request.remaining -= deltaTime;
            if (request.timed ? request.remaining <= 0f : request.source == null) requests.RemoveAt(i);
        }
        activeTypes.Clear();
        foreach (var request in requests)
            if (!activeTypes.Contains(request.type)) activeTypes.Add(request.type);

        if (activeTypes.Count == 0) CurrentType = null;
        else if (!CurrentType.HasValue || !activeTypes.Contains(CurrentType.Value)) Display(activeTypes[0]);
        else
        {
            shownSeconds += deltaTime;
            if (activeTypes.Count > 1 && shownSeconds >= rotationSeconds)
                Display(activeTypes[(activeTypes.IndexOf(CurrentType.Value) + 1) % activeTypes.Count]);
        }
        if (CurrentType.HasValue && !gameObject.activeSelf) gameObject.SetActive(true);
        if (warningCanvasGroup != null)
        {
            warningCanvasGroup.interactable = false;
            warningCanvasGroup.blocksRaycasts = false;
            warningCanvasGroup.alpha = Mathf.MoveTowards(warningCanvasGroup.alpha, CurrentType.HasValue ? 1f : 0f,
                fadeSeconds > 0f ? deltaTime / fadeSeconds : 1f);
        }
    }

    private void Display(WarningType type)
    {
        CurrentType = type;
        shownSeconds = 0f;
        var preset = warningPresets == null ? null : Array.Find(warningPresets, item => item != null && item.warningType == type);
        if (warningImage != null)
        {
            warningImage.sprite = preset?.icon;
            warningImage.enabled = preset?.icon != null;
        }
        if (warningTitle != null) warningTitle.text = preset != null ? preset.title : type.ToString();
        if (type == WarningType.LockedBag &&
            requests.Find(request => request.type == type)?.source is ShopProduct bag)
        {
            if (warningMessage != null) warningMessage.text = bag.UnlockHint();
            return;
        }
        if (type == WarningType.needKeyForChest &&
            requests.Find(request => request.type == type)?.source is BuriedFind chest)
        {
            if (warningMessage != null) warningMessage.text = chest.ChestKeyHint;
            return;
        }
        if (type == WarningType.hangarLocked &&
            requests.Find(request => request.type == type)?.source is HangarKeyDoor door)
        {
            if (warningMessage != null) warningMessage.text = door.KeyHint;
            return;
        }
        if (warningMessage != null) warningMessage.text = preset != null ? preset.message : "";
    }
}

[Serializable]
public class WarningPresets
{
    public WarningType warningType;
    public Sprite icon;
    public string title;
    public string message;
}
