using System;
using System.Collections;
using UIPopups;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    public BasePopupUI popupUI;
    [Space]
    public bool animating;
    public static bool isLoadingOn;
    public static bool IsLoading => isLoadingOn || LoadingScene.activeLoading ||
        (Instance != null && Instance.transitionRunning);
    private CanvasGroup canvasGroup;
    public static LoadingScreen Instance;
    [SerializeField] private Image loadingImage;
    [SerializeField, Min(.1f)] private float progressFillSeconds = 1f;
    private float displayedProgress;
    private float targetProgress;
    public bool IsProgressComplete => displayedProgress >= 1f;
    private Material originalLoadingMaterial, loadingMaterial;
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int UseFillId = Shader.PropertyToID("_UseFill");
    private bool transitionRunning;
    private Coroutine fadeOut;

    public static bool CanLoadScene(string sceneName)
    {
        if (!string.IsNullOrWhiteSpace(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName)) return true;
        Debug.LogError("Cannot load scene because it is missing from Build Settings: " + sceneName);
        return false;
    }

    public bool LoadScene(string sceneName, float minimumSeconds = 1f)
    {
        return CanLoadScene(sceneName) && RunTransition(LoadSceneRoutine(sceneName), minimumSeconds);
    }

    internal static IEnumerator LoadSceneRoutine(string sceneName)
    {
        // Unload the outgoing scene through the lightweight loading scene
        // before allocating the destination scene's assets.
        LoadingScene.LoadScene(sceneName);
        while (LoadingScene.activeLoading)
            yield return null;
    }

    public bool RunTransition(IEnumerator operation, float minimumSeconds = 1f)
    {
        if (transitionRunning || isLoadingOn || LoadingScene.activeLoading || operation == null) return false;
        LoadingOn();
        transitionRunning = true;
        StartCoroutine(RunCoveredTransition(operation, minimumSeconds));
        return true;
    }

    private IEnumerator RunCoveredTransition(IEnumerator operation, float minimumSeconds)
    {
        float started = Time.realtimeSinceStartup;
        // Render the curtain before changing the active objects or resetting the scene.
        yield return new WaitForEndOfFrame();
        SetProgress(.25f);
        // Flatten nested routines so an operation failure still removes the curtain.
        var stack = new System.Collections.Generic.Stack<IEnumerator>();
        stack.Push(operation);
        while (stack.Count > 0)
        {
            object yielded = null;
            bool advanced = false;
            Exception failure = null;
            try
            {
                advanced = stack.Peek().MoveNext();
                if (advanced) yielded = stack.Peek().Current;
            }
            catch (Exception exception) { failure = exception; }
            if (failure != null)
            {
                Debug.LogException(failure, this);
                break;
            }
            if (!advanced) { stack.Pop(); continue; }
            if (yielded is IEnumerator nested) stack.Push(nested);
            else yield return yielded;
        }
        SetProgress(1f);
        yield return null;
        float remaining = Mathf.Max(0f, minimumSeconds) - (Time.realtimeSinceStartup - started);
        if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
        transitionRunning = false;
        LoadingOff();
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (!Instance)
        {
            Instance = this;
            LoadingOff();
        }
    }

    public void LoadingOn()
    {
        AudioManager.Instance?.PlayLoadingMusic();
        if (fadeOut != null)
        {
            StopCoroutine(fadeOut);
            fadeOut = null;
        }
        gameObject.SetActive(true);
        displayedProgress = targetProgress = 0f;
        ApplyProgress(0f);
        animating = true;
        isLoadingOn = true;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        if (popupUI)
        {
            popupUI.ShowInstant(true, closePreviousPopup: false);
        }
    }

    public void SetProgress(float progress)
    {
        float amount = Mathf.Clamp01(progress);
        targetProgress = isLoadingOn ? Mathf.Max(targetProgress, amount) : amount;
        if (!isLoadingOn)
        {
            displayedProgress = targetProgress;
            ApplyProgress(displayedProgress);
        }
    }

    private void Update()
    {
        if (isLoadingOn) AdvanceProgress(Time.unscaledDeltaTime);
    }

    private void AdvanceProgress(float deltaTime)
    {
        // A long scene activation frame must not jump the displayed bar to 100%.
        displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress,
            Mathf.Clamp(deltaTime, 0f, .05f) / Mathf.Max(.1f, progressFillSeconds));
        ApplyProgress(displayedProgress);
    }

    private void ApplyProgress(float progress)
    {
        if (loadingImage == null) return;
        if (loadingMaterial == null)
        {
            originalLoadingMaterial = loadingImage.material;
            if (originalLoadingMaterial == null || !originalLoadingMaterial.HasProperty(FillAmountId)) return;
            // Each loading bar owns its progress; never modify the shared material asset.
            loadingMaterial = new Material(originalLoadingMaterial) { hideFlags = HideFlags.DontSave };
            loadingImage.material = loadingMaterial;
        }

        float amount = Mathf.Clamp01(progress);
        loadingMaterial.SetFloat(FillAmountId, amount);
        if (loadingMaterial.HasProperty(UseFillId)) loadingMaterial.SetFloat(UseFillId, 1f);
        // UI masks may render a cached stencil copy instead of the assigned material.
        var renderedMaterial = loadingImage.materialForRendering;
        if (renderedMaterial != null && renderedMaterial != loadingMaterial && renderedMaterial.HasProperty(FillAmountId))
        {
            renderedMaterial.SetFloat(FillAmountId, amount);
            if (renderedMaterial.HasProperty(UseFillId)) renderedMaterial.SetFloat(UseFillId, 1f);
        }
        loadingImage.SetMaterialDirty();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (loadingMaterial == null) return;
        if (loadingImage != null && loadingImage.material == loadingMaterial)
            loadingImage.material = originalLoadingMaterial;
        if (Application.isPlaying) Destroy(loadingMaterial);
        else DestroyImmediate(loadingMaterial);
    }

    public void LoadingOff(float duration)
    {
        if (fadeOut != null) return;
        if (duration <= 0f || canvasGroup == null || !isActiveAndEnabled || canvasGroup.alpha <= 0f)
        {
            LoadingOff();
            return;
        }
        fadeOut = StartCoroutine(FadeOut(duration));
    }

    private IEnumerator FadeOut(float duration)
    {
        SetProgress(1f);
        // Menu settings may request the reveal from Awake. Keep the curtain up
        // through scene initialization and until the visible bar reaches 100%.
        while (LoadingScene.activeLoading || transitionRunning || displayedProgress < 1f)
            yield return null;
        yield return new WaitForSecondsRealtime(.1f);
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        fadeOut = null;
        LoadingOff();
    }

    public void LoadingOff()
    {
        // A scene transition may finish while the menu's reveal is still fading.
        if (fadeOut != null) return;
        animating = false;
        isLoadingOn = false;
        if (popupUI)
        {
            popupUI.ShowInstant(false);
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
