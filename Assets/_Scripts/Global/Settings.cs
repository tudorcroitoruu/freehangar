using System;
using System.Collections.Generic;
using UIPopups;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
  //  [SerializeField] private GameObject noAdsObject;
    public static Settings Instance { get; private set; }

    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unSelectedColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Text[] texts;
    [SerializeField] private Image[] enabledSettingsImages;
    [SerializeField] private Image[] iconSettings;
    [SerializeField, Range(0f, 1f)] private float unselectedIconAlpha = 0.5f;
  //  [SerializeField] private GameObject[] enabledAddSettingsObjects;
    [SerializeField] private GameObject[] disabledSettingsObjects;
    [SerializeField] private GameObject downButtons;
    
    [Space,Header("Bees")]
    [SerializeField] private Image[] beeImages;
    [SerializeField, Min(1f)] private float beeFramesPerSecond = 12f;
    [SerializeField, Min(0f)] private float beeHoverHeight = 10f;
    [SerializeField, Min(0.1f)] private float beeHoverPeriod = 2.4f;
    private float beeClock;
    private BasePopupUI basePopupUI;
    private bool transitionRequested;

    public bool settingOpen;

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
        }

        if (!basePopupUI)
        {
            basePopupUI = GetComponent<BasePopupUI>();
        }
    }

    private void Start()
    {
        downButtons.SetActive(SceneManager.GetActiveScene().name == SceneName.Gameplay.ToString());
        //InitializeBeeAnimations();
    }




    public void Close()
    {
        basePopupUI?.Show(false);
    }

    private void OnEnable()
    {
        Init();
        settingOpen = true;
    }


    
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Init()
    {
        RefreshUI();
        ApplySettings();
        AudioListener.volume = 1;
    }


    public void ToggleMusic()
    {
        if (SaveSystem.settingsData == null) return;
        SaveSystem.settingsData.musicVolume = SaveSystem.settingsData.musicVolume > 0f ? 0f : 1f;
        RefreshUI();
        ApplySettings();
        if ((SaveSystem.settingsData.musicVolume > 0f))
        {
            AudioManager.Instance.PlayConfirmStandart(0);
        }
        else
        {
            AudioManager.Instance.PlayCancelStandart(0);
        }
    }

    public void ToggleSFX()
    {
        if (SaveSystem.settingsData == null) return;
        SaveSystem.settingsData.effectsVolume = SaveSystem.settingsData.effectsVolume > 0f ? 0f : 1f;
        RefreshUI();
        ApplySettings();
        if ((SaveSystem.settingsData.effectsVolume > 0f))
        {
            AudioManager.Instance.PlayConfirmStandart(0);
        }
        else
        {
            AudioManager.Instance.PlayCancelStandart(0);
        }
    }

    public void ToggleHaptics()
    {
        if (SaveSystem.Data == null) return;
        bool enabled = !SaveSystem.Data.haptics;
        // SDKManager.SetHapticsEnabled(enabled);
        SaveSystem.Data.haptics = enabled;
        RefreshUI();
        ApplySettings();
        if (enabled)
        {
            AudioManager.Instance.PlayConfirmStandart(0);
        }
        else
        {
            AudioManager.Instance.PlayCancelStandart(0);
        }
    }

    private void RefreshUI()
    {
        if (SaveSystem.Data == null || SaveSystem.settingsData == null) return;
        /*if (noAdsObject != null)
        {
            bool noAdsPurchased = SDKManager.AdsDisabled ||
                                  SDKManager.IsPurchased(IAPProduct.noads);
            noAdsObject.SetActive(!noAdsPurchased);
        }*/
        SetSettingVisual(0, (SaveSystem.settingsData.effectsVolume > 0f));
        SetSettingVisual(1, (SaveSystem.settingsData.musicVolume > 0f));
        SetSettingVisual(2, SaveSystem.Data.haptics);
    }

    private void SetSettingVisual(int index, bool selected)
    {
        if (enabledSettingsImages != null && index < enabledSettingsImages.Length && enabledSettingsImages[index])
            enabledSettingsImages[index].enabled = selected;

        if (disabledSettingsObjects != null && index < disabledSettingsObjects.Length && disabledSettingsObjects[index])
            disabledSettingsObjects[index].SetActive(!selected);

        if (texts != null && index < texts.Length && texts[index])
            texts[index].color = selected ? selectedColor : unSelectedColor;

        if (iconSettings == null || index >= iconSettings.Length || !iconSettings[index])
            return;

        Color iconColor = iconSettings[index].color;
        iconColor.a = selected ? 1f : unselectedIconAlpha;
        iconSettings[index].color = iconColor;
    }


    private bool IsRetryButton(GameObject settingsObject)
    {
        return settingsObject.name.IndexOf("Retry", StringComparison.OrdinalIgnoreCase) >= 0;
    }
    
    private void ApplySettings()
    {
        if (SaveSystem.Data == null || SaveSystem.settingsData == null) return;
        SoundManager.instance?.VolumeMusic((SaveSystem.settingsData.musicVolume > 0f) ? -9f : -30f);
        SoundManager.instance?.VolumeEffects((SaveSystem.settingsData.effectsVolume > 0f) ? 0f : -30f);
        if (SaveSystem.Data.haptics)
        {
            // SDKManager.Haptic(HapticPreset.Toggle);
        }
    }



    public void Retry()
    {
        if (transitionRequested)
            return;

        // HeartSystem is unavailable; allow retry without consuming a heart.
        // if (HeartSystem.Instance != null && HeartSystem.Instance.UseHeart())
        {
            transitionRequested = true;
            Close();
            LoadingScene.LoadScene(SceneName.Gameplay);
        }
        // else
        // {
        //     PopupController.Instance?.OpenPopup("noHearts");
        // }

    }

    public void GoToMenu()
    {
        if (transitionRequested)
            return;

        transitionRequested = true;
        // HeartSystem.Instance?.UseHeart();

        Close();
        LoadingScene.LoadScene(SceneName.Menu);
    }

//IAP
    public void TryRestore()
    {
        // LoadingScreen.Instance?.LoadingOn();
        // SDKManager.RestorePurchases(success =>
        // {
        // LoadingScreen.Instance?.LoadingOff();
        // RefreshUI();
        // });
    }
    public void Terms()
    {
        // SDKManager.OpenTerms();
    }

    public void Privacy()
    {
        // SDKManager.OpenPrivacyPolicy();
    }

    public void PurchaseNoAds()
    {
        // GlobalManager.instance.PurchaseNoAds();
    }
}
