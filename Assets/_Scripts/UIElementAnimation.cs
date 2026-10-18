using DG.Tweening;
using UnityEngine;


/// <summary>
/// Plays a small, reusable entrance animation for a UI element.
/// The authored position and scale are always treated as the end state.
/// </summary>
[AddComponentMenu("UI/UI Element Animation")]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class UIElementAnimation : MonoBehaviour
{
    public enum AnimationType
    {
        Fade,
        ScaleFromZero,
        ComeFromUp,
        ComeFromLeft,
        ComeFromDown,
        ComeFromRight,
        Ping
    }

    [Header("Trigger")]
    [Tooltip("Off: replay whenever the object is enabled. On: play only once, during Start.")]
    [SerializeField]
    private bool playOnceOnStart;

    [SerializeField, Min(0f)] private float delay;

    [Tooltip("Keeps menu animations running while Time.timeScale is zero.")] [SerializeField]
    private bool useUnscaledTime = true;

    [Header("Animation")] [SerializeField] private AnimationType animationType = AnimationType.ScaleFromZero;

    [SerializeField, Min(0f)] private float moveDistance = 80f;

    [SerializeField, Min(0.01f)] private float entranceDuration = 0.25f;

    [SerializeField] private Ease entranceEase = Ease.OutCubic;

    [SerializeField, Min(1f)] private float pingMultiplier = 1.06f;

    [SerializeField, Min(0.01f)] private float pingDuration = 0.1f;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Sequence sequence;
    private Vector2 initialAnchoredPosition;
    private Vector3 initialScale;
    private float initialAlpha = 1f;
    private bool initialized;

    public float Delay => delay;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        if (playOnceOnStart)
        {
            Play();
        }
    }

    private void OnEnable()
    {
        if (!playOnceOnStart && Application.isPlaying)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        KillAnimation();
        RestoreInitialState();
    }

    private void OnDestroy()
    {
        KillAnimation();
    }

    /// <summary>
    /// Restarts the configured animation from its entrance state.
    /// </summary>
    [ContextMenu("Play Animation")]
    public void Play()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Initialize();
        KillAnimation();
        RestoreInitialState();

        sequence = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(useUnscaledTime);

        if (delay > 0f)
        {
            sequence.AppendInterval(delay);
        }

        AppendSelectedAnimation();

        sequence.OnComplete(() =>
        {
            RestoreInitialState();
            sequence = null;
        });
    }

    /// <summary>
    /// Reverses the current entrance animation. If the entrance already
    /// finished, a new sequence is prepared at its end state and played back.
    /// The element remains in its entrance state until it is opened again.
    /// </summary>
    [ContextMenu("Play Animation Backwards")]
    public void PlayBackwards()
    {
        PlayBackwards(0f);
    }

    /// <summary>
    /// Plays the entrance path in reverse after an exit-specific delay.
    /// </summary>
    public void PlayBackwards(float exitDelay)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Initialize();
        KillAnimation();
        RestoreInitialState();

        sequence = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(useUnscaledTime)
            .SetAutoKill(false);

        // This interval is appended so it is encountered first while the
        // sequence plays backwards.
        AppendSelectedAnimation();
        if (exitDelay > 0f)
        {
            sequence.AppendInterval(exitDelay);
        }
        sequence.Pause();
        sequence.Goto(sequence.Duration(), false);
        PlaySequenceBackwards(sequence);
    }

    /// <summary>
    /// Stops the animation and immediately restores the authored UI state.
    /// </summary>
    public void StopAndRestore()
    {
        KillAnimation();
        RestoreInitialState();
    }

    /// <summary>
    /// Changes the entrance delay. When requested, an animation already started
    /// by OnEnable is immediately restarted so the new delay takes effect.
    /// </summary>
    public void SetDelay(float value, bool replayIfActive = false)
    {
        delay = Mathf.Max(0f, value);

        if (replayIfActive && isActiveAndEnabled && Application.isPlaying)
        {
            Play();
        }
    }

    /// <summary>
    /// Re-captures the position assigned by a layout group, then plays.
    /// </summary>
    public void PlayFromCurrentLayout(float value)
    {
        Initialize();
        KillAnimation();

        // An OnEnable animation may already have changed these values. Restore
        // the authored scale/alpha but keep the position produced by the layout.
        rectTransform.localScale = initialScale;
        if (canvasGroup != null)
            canvasGroup.alpha = initialAlpha;

        initialAnchoredPosition = rectTransform.anchoredPosition;
        delay = Mathf.Max(0f, value);
        Play();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        rectTransform = (RectTransform)transform;
        initialAnchoredPosition = rectTransform.anchoredPosition;
        initialScale = rectTransform.localScale;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        initialAlpha = canvasGroup.alpha;
        initialized = true;
    }

    private void AppendSelectedAnimation()
    {
        switch (animationType)
        {
            case AnimationType.Fade:
                canvasGroup.alpha = 0f;
                sequence.Append(
                    canvasGroup
                        .DOFade(initialAlpha, entranceDuration)
                        .SetEase(Ease.OutQuad));
                break;

            case AnimationType.ScaleFromZero:
                rectTransform.localScale = Vector3.zero;
                sequence.Append(
                    rectTransform
                        .DOScale(initialScale, entranceDuration)
                        .SetEase(entranceEase));
                break;

            case AnimationType.ComeFromUp:
                AppendMove(Vector2.up);
                break;

            case AnimationType.ComeFromLeft:
                AppendMove(Vector2.left);
                break;

            case AnimationType.ComeFromDown:
                AppendMove(Vector2.down);
                break;

            case AnimationType.ComeFromRight:
                AppendMove(Vector2.right);
                break;

            case AnimationType.Ping:
                sequence
                    .Append(
                        rectTransform
                            .DOScale(
                                initialScale * pingMultiplier,
                                pingDuration)
                            .SetEase(Ease.OutQuad))
                    .Append(
                        rectTransform
                            .DOScale(initialScale, pingDuration)
                            .SetEase(Ease.InOutSine));
                break;
        }
    }

    private void AppendMove(Vector2 direction)
    {
        rectTransform.anchoredPosition =
            initialAnchoredPosition + direction * moveDistance;
        sequence.Append(
            rectTransform
                .DOAnchorPos(initialAnchoredPosition, entranceDuration)
                .SetEase(entranceEase));
    }

    private void PlaySequenceBackwards(Sequence animationSequence)
    {
        animationSequence.OnComplete(null);
        animationSequence.OnRewind(() =>
        {
            if (sequence == animationSequence)
                sequence = null;

            animationSequence.Kill(false);
        });
        animationSequence.PlayBackwards();
    }

    private void KillAnimation()
    {
        if (sequence == null)
        {
            return;
        }

        sequence.Kill(false);
        sequence = null;
    }

    private void RestoreInitialState()
    {
        if (!initialized)
        {
            return;
        }

        rectTransform.anchoredPosition = initialAnchoredPosition;
        rectTransform.localScale = initialScale;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = initialAlpha;
        }
    }

#if UNITY_EDITOR
    private double lastEditorUpdateTime;

    public void PreviewInEditor()
    {
        if (Application.isPlaying)
        {
            Play();
            return;
        }

        StopEditorUpdates();
        KillAnimation();
        RestoreInitialState();
        initialized = false;
        Initialize();

        sequence = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(UpdateType.Manual);

        if (delay > 0f)
        {
            sequence.AppendInterval(delay);
        }

        AppendSelectedAnimation();

        sequence.OnComplete(() =>
        {
            RestoreInitialState();
            StopEditorUpdates();
            sequence = null;
        });

        lastEditorUpdateTime = UnityEditor.EditorApplication.timeSinceStartup;
        UnityEditor.EditorApplication.update -= UpdateEditorPreview;
        UnityEditor.EditorApplication.update += UpdateEditorPreview;
    }

    public void StopEditorPreview()
    {
        if (Application.isPlaying)
        {
            StopAndRestore();
            return;
        }

        StopEditorUpdates();
        KillAnimation();
        RestoreInitialState();
    }

    private void UpdateEditorPreview()
    {
        if (Application.isPlaying || sequence == null || !sequence.IsActive())
        {
            StopEditorPreview();
            return;
        }

        double currentTime = UnityEditor.EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Max(0f, (float)(currentTime - lastEditorUpdateTime));
        lastEditorUpdateTime = currentTime;

        sequence.ManualUpdate(deltaTime, deltaTime);
        UnityEditor.SceneView.RepaintAll();
    }

    private void StopEditorUpdates()
    {
        UnityEditor.EditorApplication.update -= UpdateEditorPreview;
    }

    private void OnValidate()
    {
        delay = Mathf.Max(0f, delay);
        moveDistance = Mathf.Max(0f, moveDistance);
        entranceDuration = Mathf.Max(0.01f, entranceDuration);
        pingMultiplier = Mathf.Max(1f, pingMultiplier);
        pingDuration = Mathf.Max(0.01f, pingDuration);
    }
#endif
}
