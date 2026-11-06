using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class DogScreenStatus : MonoBehaviour
{
    [SerializeField] private FarmDog dog;
    [SerializeField] private DogStatusPreset[] presets;
    [SerializeField] private RectTransform rectTransform;
    [Space, Header("Main")] 
    [SerializeField] private Image dogStatusImage;
    [SerializeField] private Image dogStatusSecondImage;
    [SerializeField] private Text statusNameText;
    [FormerlySerializedAs("heightOnIdle")]
    [SerializeField] private float heightOnReady = 120;

    [Space, Header("Rest timer")]
    [SerializeField] private GameObject restingStatus;
    [SerializeField] private Text timerText;
    [SerializeField] private Image timerImageFill;
    [SerializeField] private float heightOnResting = 200;

    [SerializeField] private Color readyColorText = Color.darkGreen;
    [SerializeField] private Color timerColorText = Color.sandyBrown;
    [SerializeField, Min(0f)] private float readyDelay = 3f;
    [SerializeField, Min(0f)] private float heightTransitionDuration = .3f;

    private FarmDog.DogState? displayedState;
    private bool timerRunning, closing;
    private float readyUntil = -1f, nextDogSearch;
    private float heightFrom, heightTarget, heightElapsed;
    private int displayedSeconds = -1;

    private void OnEnable()
    {
        ResetView();
        nextDogSearch = 0f;
    }

    private void LateUpdate()
    {
        if (!dog && Time.unscaledTime >= nextDogSearch)
        {
            dog = FindFirstObjectByType<FarmDog>();
            nextDogSearch = Time.unscaledTime + 1f;
        }
        if (!dog || !dog.isActiveAndEnabled || !SaveSystem.savesLoaded || SaveSystem.Data == null)
        {
            if (displayedState.HasValue) ResetView();
            return;
        }

        if (displayedState != dog.State) ApplyPreset(dog.State);
        UpdateTimer();
        UpdateHeight();
    }

    private void ResetView()
    {
        displayedState = null;
        timerRunning = closing = false;
        readyUntil = -1f;
        displayedSeconds = -1;
        heightFrom = heightTarget = heightOnReady;
        heightElapsed = 0f;
        if (restingStatus) restingStatus.SetActive(false);
        if (rectTransform) rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, heightOnReady);
    }

    private void ApplyPreset(FarmDog.DogState state)
    {
        displayedState = state;
        if (presets == null) return;
        foreach (var preset in presets)
        {
            if (preset == null || preset.state != state) continue;
            if (dogStatusImage) dogStatusImage.sprite = preset.statusImage;
            if (dogStatusSecondImage) dogStatusSecondImage.sprite = preset.statusSecondImage;
            if (statusNameText) statusNameText.text = preset.statusName;
            return;
        }
    }

    private void UpdateTimer()
    {
        float remaining = dog.RestRemaining;
        if (remaining > 0f)
        {
            if (!timerRunning)
            {
                timerRunning = true;
                closing = false;
                readyUntil = -1f;
                displayedSeconds = -1;
                if (restingStatus) restingStatus.SetActive(true);
                if (timerText) timerText.color = timerColorText;
                TransitionHeight(heightOnResting);
            }

            int seconds = Mathf.CeilToInt(remaining);
            if (seconds != displayedSeconds)
            {
                displayedSeconds = seconds;
                if (timerText) timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            }
            float interval = FarmDog.HuntInterval(dog.Level);
            if (timerImageFill) timerImageFill.fillAmount = interval > 0f ? Mathf.Clamp01(remaining / interval) : 0f;
            return;
        }

        if (timerRunning)
        {
            timerRunning = false;
            if (timerText)
            {
                timerText.text = "Ready";
                timerText.color = readyColorText;
            }
            if (timerImageFill) timerImageFill.fillAmount = 0f;
            readyUntil = Time.unscaledTime + readyDelay;
        }

        if (readyUntil >= 0f && Time.unscaledTime >= readyUntil)
        {
            readyUntil = -1f;
            closing = true;
            TransitionHeight(heightOnReady);
        }
    }

    private void TransitionHeight(float target)
    {
        heightFrom = rectTransform ? rectTransform.rect.height : target;
        heightTarget = target;
        heightElapsed = 0f;
    }

    private void UpdateHeight()
    {
        heightElapsed += Time.unscaledDeltaTime;
        float progress = heightTransitionDuration > 0f ? Mathf.Clamp01(heightElapsed / heightTransitionDuration) : 1f;
        if (rectTransform)
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Lerp(heightFrom, heightTarget, Mathf.SmoothStep(0f, 1f, progress)));
        // Keep the Ready panel active until the collapse finishes.
        if (closing && progress >= 1f)
        {
            closing = false;
            if (restingStatus) restingStatus.SetActive(false);
        }
    }
}

[Serializable]
public class DogStatusPreset
{
    public FarmDog.DogState state;
    public string statusName;
    public Sprite statusImage;
    public Sprite statusSecondImage;

}
