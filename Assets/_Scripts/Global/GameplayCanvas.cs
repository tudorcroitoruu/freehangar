using UnityEngine;
using UnityEngine.UI;

public class GameplayCanvas : MonoBehaviour
{
    public static GameplayCanvas Instance;

    [SerializeField] private GameObject hudObject;
    private bool hudHiddenByPause;
    private bool hudActiveBeforePause;
    [Header("Money")]
    [SerializeField] private Text moneyText;
    
    [Space, Header("Interactor")]
    public InteractionPrompt interactionPrompt;
    [SerializeField] private CanvasGroup freeHandsWarning;
    [SerializeField, Min(0f)] private float freeHandsFadeDuration = .2f;
    private bool showFreeHandsWarning;
    
    [Space, Header("Hints")]
    [SerializeField] private GameObject[] walkingHints;
    [SerializeField] private GameObject[] drivingHints;
    [SerializeField] private GameObject bicycleBoostHint;

    [Space, Header("Tools Equip Hints")] 
    [SerializeField] private GameObject hintsParent;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        ResetFreeHandsWarning();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        Pause.PauseOnAction += HideHudForPause;
        Pause.PauseOffAction += RestoreHudAfterPause;
        if (Pause.Instance != null && Pause.Instance.paused) HideHudForPause();
        ResetFreeHandsWarning();
        SaveSystem.MoneyChanged += RefreshMoney;
        RefreshMoney(SaveSystem.Money);
        var rider = FindFirstObjectByType<PlayerBicycleInteraction>();
        SetDrivingHints(rider != null && rider.IsRiding);
    }

    private void OnDisable()
    {
        Pause.PauseOnAction -= HideHudForPause;
        Pause.PauseOffAction -= RestoreHudAfterPause;
        RestoreHudAfterPause();
        SaveSystem.MoneyChanged -= RefreshMoney;
        ResetFreeHandsWarning();
    }

    private void HideHudForPause()
    {
        if (hudObject == null || hudHiddenByPause) return;
        hudActiveBeforePause = hudObject.activeSelf;
        hudHiddenByPause = true;
        hudObject.SetActive(false);
    }

    private void RestoreHudAfterPause()
    {
        if (!hudHiddenByPause) return;
        hudHiddenByPause = false;
        if (hudObject != null) hudObject.SetActive(hudActiveBeforePause);
    }

    public void SetFreeHandsWarning(bool visible)
    {
        showFreeHandsWarning = visible && isActiveAndEnabled;
        if (freeHandsWarning == null) return;
        freeHandsWarning.interactable = false;
        freeHandsWarning.blocksRaycasts = false;
        if (showFreeHandsWarning && !freeHandsWarning.gameObject.activeSelf)
            freeHandsWarning.gameObject.SetActive(true);
        if (freeHandsFadeDuration <= 0f) freeHandsWarning.alpha = showFreeHandsWarning ? 1f : 0f;
    }

    public void SetDrivingHints(bool driving)
    {
        if (walkingHints != null)
            foreach (var hint in walkingHints)
                if (hint != null) hint.SetActive(!driving);
        if (drivingHints != null)
            foreach (var hint in drivingHints)
                if (hint != null) hint.SetActive(driving);
    }

    private void Update() => FadeFreeHandsWarning(Time.unscaledDeltaTime);

    private void FadeFreeHandsWarning(float deltaTime)
    {
        if (freeHandsWarning == null) return;
        float step = freeHandsFadeDuration > 0f ? Mathf.Max(0f, deltaTime) / freeHandsFadeDuration : 1f;
        freeHandsWarning.alpha = Mathf.MoveTowards(freeHandsWarning.alpha, showFreeHandsWarning ? 1f : 0f, step);
    }

    private void ResetFreeHandsWarning()
    {
        showFreeHandsWarning = false;
        if (freeHandsWarning == null) return;
        freeHandsWarning.alpha = 0f;
        freeHandsWarning.interactable = false;
        freeHandsWarning.blocksRaycasts = false;
    }

    private void RefreshMoney(int amount)
    {
        if (moneyText != null) moneyText.text = "$ " + EconomyNumberFormat.Accounting(amount);
    }

    public bool AddMoney(int amount) => SaveSystem.AddMoney(amount);

    public bool SpendMoney(int amount) => SaveSystem.SpendMoney(amount);
}
