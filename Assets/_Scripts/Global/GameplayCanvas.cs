using UnityEngine;
using UnityEngine.UI;

public class GameplayCanvas : MonoBehaviour
{
    public static GameplayCanvas Instance;
    
    [Header("Money")]
    [SerializeField] private Text moneyText;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        SaveSystem.MoneyChanged += RefreshMoney;
        RefreshMoney(SaveSystem.Money);
    }

    private void OnDisable()
    {
        SaveSystem.MoneyChanged -= RefreshMoney;
    }

    private void RefreshMoney(int amount)
    {
        if (moneyText != null) moneyText.text = "$ " + amount.ToString("N0");
    }

    public bool AddMoney(int amount) => SaveSystem.AddMoney(amount);

    public bool SpendMoney(int amount) => SaveSystem.SpendMoney(amount);
}
