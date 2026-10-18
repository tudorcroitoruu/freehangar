using UIPopups;
using UnityEngine;

public class LoadingScreen : MonoBehaviour
{
    public BasePopupUI popupUI;
    [Space]
    public bool animating;
    public static bool isLoadingOn;
    private CanvasGroup canvasGroup;
    public static LoadingScreen Instance;

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

    public void LoadingOff()
    {
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
