using System;
using UnityEngine;
using UnityEngine.UI;

public enum WarningType
{
    RatsAttack,
    CrowsAttack,
    BlockHangarEntry,
    VehicleFull
}
public class WarningPopup : MonoBehaviour
{
    [SerializeField] private WarningPresets[] warningPresets;
    [Space]
    [SerializeField] private CanvasGroup warningCanvasGroup;
    [SerializeField] private Image warningImage;
    [SerializeField] private Text warningTitle;
    [SerializeField] private Text warningMessage;

    public void ShopWarning(WarningType type)
    {
        //Sett all infos from warningPresets based on type
        gameObject.SetActive(true);
        //fadeIn
    }
}

[Serializable]
public class WarningPresets
{
    public WarningType warningType;
    public Sprite icon;
    public string title;
    public string message;
}
