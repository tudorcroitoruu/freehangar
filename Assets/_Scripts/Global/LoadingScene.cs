using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class LoadingScene : MonoBehaviour
{
    private const float MinimumLoadingDuration = 1f;
    private static float loadingStartedAt;



    private static bool clearPreviousScene;
    private static string TargetScene;
    private static string currentScene = "LoadingScene";
    private static bool sceneLoadedSubscribed;
    private static bool gameplayRevealScheduled;
    public static bool online;
    public static bool activeLoading;
    public static bool asyncCompleted = false;

    private static float timeDefault;
    private static LoadingScene instance;
    
    [Header("UI")]
    [SerializeField] private Image loadingImage;
    [SerializeField] private Text loadingText;
    private bool toMenu;

    private void Awake()
    {
        EnsureSceneLoadedSubscription();

        instance = this;
        timeDefault = QualitySettings.asyncUploadTimeSlice;

        if (clearPreviousScene)
        {
            clearPreviousScene = false;
            StartCoroutine(DelayedUnload());
        }
    }

    /*private IEnumerator Start()
    {
        DOVirtual.Float(loadingImage.fillAmount, 1f, 3f, value => loadingImage.fillAmount = value);
        while (loadingImage.fillAmount <= 1f)
        {
            CalculatePlanePos(loadingImage.fillAmount);
            yield return new WaitForSeconds(0.05f);
        }
        
        plane.SetActive(false);
        yield return new WaitForSeconds(1f);
        DOVirtual.Vector3(crashQuestionMarks.localScale, Vector3.one, 0.2f,
            value => crashQuestionMarks.localScale = value);
    }*/

    private IEnumerator DelayedUnload()
    {
        while (instance == null)
            yield return null;

        yield return UnloadOldScene();
    }

    public static void LoadScene(SceneName sceneName)
    {
        if (!PrepareSceneLoad(sceneName.ToString()))
            return;

        StartPreparedSceneLoad();
    }

    public static void LoadScene(string sceneName)
    {
        if (!PrepareSceneLoad(sceneName))
            return;

        StartPreparedSceneLoad();
    }

    private static bool PrepareSceneLoad(string sceneName)
    {
        if (activeLoading)
            return false;

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Cannot load scene because it is missing from Build Settings: " + sceneName);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(currentScene))
        {
            Debug.LogError("Cannot load transition scene because it is missing from Build Settings: " + currentScene);
            return false;
        }

        EnsureSceneLoadedSubscription();
        Time.timeScale = 1f;
        asyncCompleted = false;
        activeLoading = true;
        loadingStartedAt = Time.realtimeSinceStartup;
        gameplayRevealScheduled = false;
        progress = 0f;
        TargetScene = sceneName;
        clearPreviousScene = true;

        // The SceneName overload deliberately waits before changing scenes. Mark the
        // transition active before that wait so outgoing gameplay cannot emit effects
        // (notably loose-brick collision haptics) into the incoming level.
        // SDKManager.StopHaptics();
        LoadingScreen.Instance?.LoadingOn();
        return true;
    }

    private static void StartPreparedSceneLoad()
    {
        // Cover the current scene first. Waiting until the end of this frame guarantees the
        // loading canvas is rendered before the asynchronous transition begins.
        MonoBehaviour coroutineRunner = GlobalManager.Instance != null
            ? GlobalManager.Instance
            : LoadingScreen.Instance;
        if (coroutineRunner != null && coroutineRunner.isActiveAndEnabled)
        {
            coroutineRunner.StartCoroutine(BeginSceneLoadAfterCover());
        }
        else
        {
            StartLoadingSceneAsync();
        }
    }

    private static IEnumerator BeginSceneLoadAfterCover()
    {
        yield return new WaitForEndOfFrame();
        StartLoadingSceneAsync();
    }

    private static void StartLoadingSceneAsync()
    {
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(currentScene);
        if (loadOperation == null)
            AbortSceneLoad("Failed to start loading scene: " + currentScene);
    }

    private static void EnsureSceneLoadedSubscription()
    {
        if (sceneLoadedSubscribed)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneLoadedSubscribed = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!activeLoading)
            return;

        if (!string.IsNullOrEmpty(TargetScene) && scene.name == TargetScene)
            LoadedScene();
    }

    private static IEnumerator UnloadOldScene()
    {
        if (instance == null)
            yield break;

        if (string.IsNullOrEmpty(TargetScene))
        {
            AbortSceneLoad("TargetScene is null or empty. Aborting scene load.");
            yield break;
        }

        asyncCompleted = true;

        /*
        if (ShouldUnloadUnusedAssetsBeforeLoad())
        {
            AsyncOperation unloadOperation = Resources.UnloadUnusedAssets();
            while (!unloadOperation.isDone)
                yield return null;
        }*/
        
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(TargetScene);
        if (loadOperation == null)
        {
            AbortSceneLoad("Failed to start loading scene: " + TargetScene);
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        instance.toMenu = TargetScene == "Menu";
        //LoadingScreen.LoadingOffEvent?.Invoke();
        yield return UpdateLoadingProgressCustom(loadOperation);
    }


    public static float progress;

    private static IEnumerator UpdateLoadingProgress(AsyncOperation loadOperation)
    {
        while (!loadOperation.isDone)
        {
            progress = Mathf.Clamp01(loadOperation.progress / 0.9f);

            if (instance.loadingImage != null)
            {
                instance.loadingImage.fillAmount = Mathf.Lerp(instance.loadingImage.fillAmount, progress, Time.deltaTime * 2f);
                if (instance.loadingText != null)
                    instance.loadingText.text = (loadOperation.progress * 100f).ToString("N0") + "%";
            }

            if (loadOperation.progress >= 0.9f)
            {
                loadOperation.allowSceneActivation = true;
            }

            yield return null;
        }

        LoadedScene();
    }

    private static IEnumerator UpdateLoadingProgressCustom(AsyncOperation loadOperation)
    {
        float targetFill = instance != null && instance.toMenu ? 1f : 0.92f;

        while (loadOperation.progress < 0.9f)
        {
            progress = Mathf.Clamp01(loadOperation.progress / 0.9f);

            if (instance != null)
            {
                if (instance.loadingImage != null)
                    instance.loadingImage.fillAmount = targetFill * progress;

                if (instance.loadingText != null)
                    instance.loadingText.text = (progress * 100f).ToString("N0") + "%";
            }

            yield return null;
        }

        progress = 1f;
        if (instance != null)
        {
            if (instance.loadingImage != null)
                instance.loadingImage.fillAmount = targetFill;

            if (instance.loadingText != null)
                instance.loadingText.text = "100%";

        }

        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
            yield return null;

        // OnSceneLoaded can complete the transition first during scene activation.
        // Only invoke the completion path here when it has not already done so.
        if (activeLoading)
            LoadedScene();
    }

    private static bool ShouldUnloadUnusedAssetsBeforeLoad()
    {
        if (TargetScene == "Game")
            return false;

        if (Application.isMobilePlatform && currentScene == "Menu")
            return false;

        return true;
    }



    private static void LoadedScene()
    {
        if (activeLoading)
        {
            if (gameplayRevealScheduled)
                return;

            MonoBehaviour coroutineRunner = GlobalManager.Instance != null
                ? GlobalManager.Instance
                : LoadingScreen.Instance;
            if (coroutineRunner != null && coroutineRunner.isActiveAndEnabled)
            {
                gameplayRevealScheduled = true;
                coroutineRunner.StartCoroutine(RevealGameplayAfterDelay());
                return;
            }
        }

        CompleteSceneLoad();
    }

    private static IEnumerator RevealGameplayAfterDelay()
    {
        // Let the incoming scene finish Start before revealing it, even on a slow load.
        yield return null;
        float remaining = MinimumLoadingDuration - (Time.realtimeSinceStartup - loadingStartedAt);
        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);
        CompleteSceneLoad();
    }

    private static void CompleteSceneLoad()
    {
        gameplayRevealScheduled = false;
        activeLoading = false;
        asyncCompleted = false;
        LoadingScreen.Instance?.LoadingOff();
    }

    private static void AbortSceneLoad(string message)
    {
        Debug.LogError(message);
        gameplayRevealScheduled = false;
        activeLoading = false;
        asyncCompleted = false;
        clearPreviousScene = false;
        LoadingScreen.Instance?.LoadingOff();
    }
}

public enum SceneName
{
    Bootstrap,
    Menu,
    LoadingScene,
    Gameplay
}
