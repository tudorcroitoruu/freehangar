using UnityEngine;
using UnityEngine.UI;

public class UpgradeStamina : MonoBehaviour
{
    [SerializeField] private Text currentLevelText;
    [SerializeField] private Text staminaModification;
    [SerializeField] private Color nextLevelColor = Color.forestGreen;

    [Space, Header("Price")]
    [SerializeField] private Text price;
    [SerializeField] private Text notEnoughMoney;

    [SerializeField] private GameObject notEnoughMoneyPopup;
    
    [Space, Header("Upgrade Button")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private CanvasGroup upgradeButtonCanvas;
    [SerializeField] private Image upgradeButtonImage;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
