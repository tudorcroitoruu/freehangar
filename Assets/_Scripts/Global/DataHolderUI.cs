using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UIPopups;

public class DataHolderUI : MonoBehaviour
{
    public static DataHolderUI Instance;

//     private const int DefaultMaxHearts = 5;
//     private const string FullHeartsText = "Full";
//     private const float HeartTimerRefreshInterval = 1f;

    [Space]
    [SerializeField] private Text coinText;
//     [SerializeField] private Text heartText;
//     [SerializeField] private Text heartTimer;

    [Header("Animation Settings")]
    [SerializeField] private float coinAnimDuration = 0.6f;
    [SerializeField] private Ease coinEase = Ease.OutCubic;

    private Tween coinTween;
    private int currentCoinsDisplayed;
//     private float nextHeartRefreshTime;


    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RefreshDataPlayerInstant();
    }

    private void OnEnable()
    {
        Instance = this;
        RefreshDataPlayerInstant();
//         nextHeartRefreshTime = Time.unscaledTime + HeartTimerRefreshInterval;
    }

//     private void Update()
//     {
//         if (Time.unscaledTime < nextHeartRefreshTime)
//             return;
// 
//         nextHeartRefreshTime = Time.unscaledTime + HeartTimerRefreshInterval;
//         RefreshHeartTimer();
//     }

    public void RefreshDataPlayer()
    {
        if (SaveSystem.Data == null)
            return;

        AnimateCoins(SaveSystem.Data.cash);
//         RefreshOnlyHearts();
        GlobalManager.RefreshAllDataHolders();
    }

    public void RefreshDataPlayerInstant()
    {
        if (SaveSystem.Data == null)
            return;

        currentCoinsDisplayed = SaveSystem.Data.cash;
        SetCoinsText(currentCoinsDisplayed);
//         RefreshOnlyHearts();
    }

    public void AnimateCoins(int targetCoins)
    {
        if (coinText == null)
            return;

        coinTween?.Kill();
        int startValue = currentCoinsDisplayed;

        coinTween = DOTween.To(() => startValue, x =>
        {
            currentCoinsDisplayed = x;
            SetCoinsText(x);
        }, targetCoins, coinAnimDuration)
        .SetEase(coinEase)
        .OnComplete(() =>
        {
            currentCoinsDisplayed = targetCoins;
            SetCoinsText(targetCoins);
        });
    }

    private void SetCoinsText(int value)
    {
        if (coinText != null)
            coinText.text = value.ToString();
    }

//     private void RefreshHearts()
//     {
//         if (heartText == null || SaveSystem.Data == null)
//             return;
// 
//         /*
//         heartText.text = HeartSystem.Instance != null
//             ? HeartSystem.Instance.CurrentHearts.ToString()
//             : SaveSystem.Data.lives.ToString();*/
//     }

//     private void RefreshHeartTimer()
//     {
//         if (heartTimer == null)
//             return;
// 
//         if (SaveSystem.Data == null)
//         {
//             heartTimer.text = "";
//             return;
//         }
// 
//         if (HeartSystem.Instance == null)
//         {
//             heartTimer.text = SaveSystem.Data.lives >= DefaultMaxHearts ? FullHeartsText : "";
//             return;
//         }
// 
//         HeartSystem.Instance.RefreshHeartsFromStoredTime();
//         RefreshHearts();
// 
//         if (HeartSystem.Instance.CurrentHearts >= HeartSystem.Instance.MaxHearts)
//         {
//             heartTimer.text = FullHeartsText;
//             return;
//         }
// 
//         if (HeartSystem.Instance.HasInfiniteHearts)
//         {
//             heartTimer.text = "∞";
//             return;
//         }
// 
//         int seconds = HeartSystem.Instance.GetSecondsUntilNextHeart();
//         if (seconds < 0)
//         {
//             heartTimer.text = "--:--";
//             return;
//         }
// 
//         heartTimer.text = HeartSystem.FormatTime(seconds);
//     }
    

    public void AddCoinsAnimated(int amount)
    {
        if (SaveSystem.Data == null)
            return;

        SaveSystem.Data.cash += amount;
        SaveSystem.SaveData();
        AnimateCoins(SaveSystem.Data.cash);

        if (coinText != null)
            coinText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1);
    }

    public void RemoveCoinsAnimated(int amount)
    {
        if (SaveSystem.Data == null)
            return;

        SaveSystem.Data.cash = Mathf.Max(0, SaveSystem.Data.cash - amount);
        SaveSystem.SaveData();

        AnimateCoins(SaveSystem.Data.cash);

        if (coinText != null)
            coinText.transform.DOShakePosition(0.3f, 10f, 10);
    }

//     public void RefreshOnlyHearts()
//     {
//         RefreshHearts();
//         RefreshHeartTimer();
//     }

    public void OpenStore()
    {
        if (BasePopupUI.HasActivePopup)
        {
            BasePopupUI.CloseCurrentPopup();
        }
    }
    public void OpenStore(BasePopupUI popup)
    {
        popup.Show(true);
    }
    public static void ShowStoreSafe()
    {
        if (Instance != null)
            Instance.OpenStore();
        else
            Debug.LogWarning("DataHolderUI.Instance is null, cannot open store.");
    }

    private void OnDestroy()
    {
        coinTween?.Kill();
        if (Instance == this)
            Instance = null;
    }
}
