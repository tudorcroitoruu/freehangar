using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementsPopup : MonoBehaviour
{
    [SerializeField] private WarningPresets[] achievementsPresets;
    [Space]
    [SerializeField] private CanvasGroup achievementsCanvasGroup;
    [SerializeField] private Image achievementsImage;
    [SerializeField] private Text achievementsTitle;
    [SerializeField, Min(.1f)] private float rotationSeconds = 3f;
    [SerializeField, Min(0f)] private float fadeSeconds = .2f;

    private readonly List<WarningPopup.Request> requests = new List<WarningPopup.Request>();
    private readonly List<WarningType> activeTypes = new List<WarningType>();
    public WarningType? CurrentType { get; private set; }
    private float shownSeconds;

    public void Initialize()
    {
        if (achievementsCanvasGroup == null) return;
        achievementsCanvasGroup.alpha = 0f;
        achievementsCanvasGroup.interactable = false;
        achievementsCanvasGroup.blocksRaycasts = false;
    }

    // Kept for existing Inspector events. Repeated requests extend one notification.
    public void ShopWarning(WarningType type) => ShowWarning(type);

    public void ShowWarning(WarningType type, float seconds = 2f)
    {
        var request = requests.Find(item => item.type == type && item.timed);
        if (request == null)
        {
            request = new WarningPopup.Request { type = type, timed = true };
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
        requests.Add(new WarningPopup.Request { type = type, source = source });
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
        if (achievementsCanvasGroup != null)
        {
            achievementsCanvasGroup.interactable = false;
            achievementsCanvasGroup.blocksRaycasts = false;
            achievementsCanvasGroup.alpha = Mathf.MoveTowards(achievementsCanvasGroup.alpha, CurrentType.HasValue ? 1f : 0f,
                fadeSeconds > 0f ? deltaTime / fadeSeconds : 1f);
        }
    }

    private void Display(WarningType type)
    {
        CurrentType = type;
        shownSeconds = 0f;
        var preset = achievementsPresets == null ? null : Array.Find(achievementsPresets, item => item != null && item.warningType == type);
        if (achievementsImage != null)
        {
            achievementsImage.sprite = preset?.icon;
            achievementsImage.enabled = preset?.icon != null;
        }
        if (achievementsTitle != null) achievementsTitle.text = preset != null ? preset.title : type.ToString();
    }
}
