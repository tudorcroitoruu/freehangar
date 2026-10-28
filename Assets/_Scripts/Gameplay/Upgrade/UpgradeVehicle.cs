using UnityEngine;
using UnityEngine.UI;

public class UpgradeVehicle : MonoBehaviour
{
    [SerializeField] private GameObject currentLevelHolder;
    [SerializeField] private GameObject nextLevelHolder;
    [SerializeField] private GameObject maxLevelHolder;
    
    [Space, Header("Upgrade Data")]
    [SerializeField] private Image[] vehicleImages;
    [SerializeField] private Text currentLevelCapacity;
    [SerializeField] private Text nextLevelCapacity;
    [SerializeField] private Text maxLevelCapacity;
    [SerializeField] private Text currentLevelSlots;
    [SerializeField] private Text nextLevelSlots;
    [SerializeField] private Text maxLevelSlots;
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
