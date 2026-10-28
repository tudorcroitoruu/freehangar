using UnityEngine;
using UnityEngine.UI;

public class GameplayCanvas : MonoBehaviour
{
    public static GameplayCanvas Instance;
    
    [Header("Money")]
    [SerializeField] private Text moneyText;
    
    [Space, Header("Interactor")]
    public InteractionPrompt interactionPrompt;
    [SerializeField] private CanvasGroup freeHandsWarning;
    [SerializeField, Min(0f)] private float freeHandsFadeDuration = .2f;
    private bool showFreeHandsWarning;
    
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
        ResetFreeHandsWarning();
        SaveSystem.MoneyChanged += RefreshMoney;
        RefreshMoney(SaveSystem.Money);
    }

    private void OnDisable()
    {
        SaveSystem.MoneyChanged -= RefreshMoney;
        ResetFreeHandsWarning();
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
        if (moneyText != null) moneyText.text = "$ " + amount.ToString("N0");
    }

    public bool AddMoney(int amount) => SaveSystem.AddMoney(amount);

    public bool SpendMoney(int amount) => SaveSystem.SpendMoney(amount);
}
