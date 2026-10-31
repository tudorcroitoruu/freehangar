// DLSS integration is temporarily commented out for future implementation.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    [Header("Buttons")] 
    [SerializeField] private SelectedUI[] buttons;
    [SerializeField] private GameObject[] sections;
    
    [Header("VIDEO - Resolution")]
    [SerializeField] private Button previousResolutionButton;
    [SerializeField] private Button nextResolutionButton;
    [SerializeField] private TMP_Text resolutionValueText;

    [Header("VIDEO - Window Mode")]
    [SerializeField] private Button previousWindowModeButton;
    [SerializeField] private Button nextWindowModeButton;
    [SerializeField] private TMP_Text windowModeValueText;

    [Header("VIDEO - Display")]
    [SerializeField] private Button previousDisplayButton;
    [SerializeField] private Button nextDisplayButton;
    [SerializeField] private TMP_Text displayValueText;

    [Header("VIDEO - VSync")]
    [SerializeField] private Button vSyncOffButton;
    [SerializeField] private Button vSyncOnButton;
    [SerializeField] private Color vSyncSelectedBackground = Color.white;
    [SerializeField] private Color vSyncUnselectedBackground = new(0.12f, 0.12f, 0.12f, 1f);
    [SerializeField] private Color vSyncSelectedText = Color.black;
    [SerializeField] private Color vSyncUnselectedText = Color.gray;

    [Header("VIDEO - Frame Rate")]
    [SerializeField] private Button previousFrameRateButton;
    [SerializeField] private Button nextFrameRateButton;
    [SerializeField] private TMP_Text frameRateValueText;

    [Header("VIDEO - Quality")]
    [SerializeField] private Button previousQualityButton;
    [SerializeField] private Button nextQualityButton;
    [SerializeField] private TMP_Text qualityValueText;

    [SerializeField] private QualityPreset[] qualityPresets =
    {
        new() { name = "Low", renderScale = 0.65f, msaa = MsaaSamples.Off,
            shadowDistance = 20f, shadowResolution = 512, shadowCascades = 1,
            screenSpaceAmbientOcclusion = false, textureMipmapLimit = 2, lodBias = 0.6f },
        new() { name = "Medium", renderScale = 0.85f, msaa = MsaaSamples.X2,
            shadowDistance = 40f, shadowResolution = 1024, shadowCascades = 2,
            screenSpaceAmbientOcclusion = false, textureMipmapLimit = 1, lodBias = 1f },
        new() { name = "High", renderScale = 1f, msaa = MsaaSamples.X4,
            shadowDistance = 60f, shadowResolution = 2048, shadowCascades = 2,
            lodBias = 1.5f },
        new() { name = "Ultra", renderScale = 1f, msaa = MsaaSamples.X8,
            shadowDistance = 100f, shadowResolution = 4096, shadowCascades = 4,
            lodBias = 2f }
    };

    private enum MsaaSamples { Off = 1, X2 = 2, X4 = 4, X8 = 8 }

    [Serializable]
    private sealed class QualityPreset
    {
        public string name;
        [Range(0.25f, 1f)] public float renderScale = 1f;
        [Tooltip("MSAA applies to HDRP cameras using forward rendering.")]
        public MsaaSamples msaa = MsaaSamples.X4;
        [Min(0f)] public float shadowDistance = 60f;
        [Range(256, 4096)] public int shadowResolution = 2048;
        [Range(1, 4)] public int shadowCascades = 2;
        [Tooltip("Allow ambient occlusion from the scene's HDRP Volumes.")]
        public bool screenSpaceAmbientOcclusion = true;
        [Range(0, 3)] public int textureMipmapLimit;
        [Range(0.1f, 4f)] public float lodBias = 1.5f;
    }

    // One runtime instance is reused by every preset and across scene changes.
    // The project asset remains unchanged when testing settings in the Editor.
    private static HDRenderPipelineAsset runtimePipeline;
    private static RenderPipelineAsset originalQualityPipeline;
    private static int originalTextureMipmapLimit;
    private static float originalLodBias;
    private static Volume runtimeQualityVolume;
    private static VolumeProfile runtimeQualityProfile;
    private static readonly Dictionary<HDAdditionalCameraData, bool> originalDynamicResolutionStates = new();
    public static HDRenderPipelineAsset ActiveRenderPipeline => runtimePipeline;
    private int QualityPresetCount => qualityPresets?.Length ?? 0;

//     [Header("VIDEO - Upscaling")]
//     [SerializeField] private TMP_Dropdown upscalingDropdown;
//     [SerializeField] private TMP_Dropdown dlssQualityDropdown;
// 
//     [Header("DLSS Provider")]
//     [Tooltip("Component that implements IDLSSProvider.")]
//     [SerializeField] private MonoBehaviour dlssProviderBehaviour;

    [Header("AUDIO")]
    [SerializeField] private Button decreaseMasterVolumeButton;
    [SerializeField] private Button increaseMasterVolumeButton;
    [SerializeField] private TMP_Text masterVolumeValueText;
    [SerializeField] private Button decreaseMusicVolumeButton;
    [SerializeField] private Button increaseMusicVolumeButton;
    [SerializeField] private TMP_Text musicVolumeValueText;
    [SerializeField] private Button decreaseSFXVolumeButton;
    [SerializeField] private Button increaseSFXVolumeButton;
    [SerializeField] private TMP_Text sfxVolumeValueText;

    [Header("Optional Audio Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("CONTROLS")]
    [SerializeField] private Slider aimSensitivitySlider;
    [SerializeField] private TMP_Text aimSensitivityValueText;
    [SerializeField] private Button decreaseSensitivityButton;
    [SerializeField] private Button increaseSensitivityButton;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private ActionBindingButton[] actionsButtons;
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;
    private ActionBindingButton rebindingButton;
    public bool IsRebinding => rebindOperation != null;
    private int lastRebindFrame = -1;
    public static bool IsRebindingInputCaptured => Instance &&
        (Instance.IsRebinding || Instance.lastRebindFrame == Time.frameCount);
    public static event Action InputBindingsChanged;

    public float AimSensitivity { get; private set; } = 1f;

    public event Action<float> OnAimSensitivityChanged;

    private Resolution[] availableResolutions;
    private readonly List<DisplayInfo> availableDisplays = new();
    private bool displayMovePending;

//     private IDLSSProvider dlssProvider;
    private bool initialized;
    private bool settingsChanged;

    private SettingsData settings => SaveSystem.settingsData;

    private readonly int[] frameRateValues =
    {
        30,
        60,
        120,
        -1
    };

    private readonly string[] frameRateLabels =
    {
        "30",
        "60",
        "120",
        "Unlimited"
    };

//     private readonly string[] upscalingLabels =
//     {
//         "Off",
//         "DLSS"
//     };
// 
//     private readonly string[] dlssQualityLabels =
//     {
//         "Quality",
//         "Balanced",
//         "Performance",
//         "Ultra Performance"
//     };

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

//         dlssProvider = dlssProviderBehaviour as IDLSSProvider;
    }

    private void OnEnable()
    {
        ShowSection(0);
    }

    private void OnDisable()
    {
        CancelRebinding();
        SaveSettings();
    }

    private void Update()
    {
        if (!initialized || IsRebindingInputCaptured)
            return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            ResetToDefaults();
    }

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => SaveSystem.savesLoaded);
        Init();
    }

    public void Init()
    {
        if (initialized || !SaveSystem.savesLoaded || settings == null)
            return;

        // GlobalManager can initialize the inactive settings page before Awake.
        // Create the runtime asset before binding buttons keep references to its actions.
        if (inputActions)
            inputActions = Instantiate(inputActions);

//         dlssProvider = dlssProviderBehaviour as IDLSSProvider;

        SetupInputBindings();
        SetupUI();

        ValidateSettings();
        UpdateUI();

        ApplyAllSettings();

        RegisterListeners();
        initialized = true;
        UpdateVideoValues();
        UpdateAudioValues();
        UpdateSensitivityValues();
    }

    private void OnDestroy()
    {
        CancelRebinding();
        if (Instance == this && inputActions)
            Destroy(inputActions);
        UnregisterListeners();
        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region UI Setup

    private void SetupInputBindings()
    {
        ApplySavedInputBindings(inputActions);
        if (actionsButtons == null)
            return;

        foreach (var button in actionsButtons)
            if (button) button.Initialize(this, inputActions);
    }

    public static void ApplySavedInputBindings(InputActionAsset asset)
    {
        if (!asset || !SaveSystem.savesLoaded || SaveSystem.settingsData == null)
            return;

        asset.RemoveAllBindingOverrides();
        string json = SaveSystem.settingsData.inputBindingOverrides;
        if (string.IsNullOrEmpty(json))
            return;
        try
        {
            asset.LoadBindingOverridesFromJson(json);
            // Older rebinding code could save the synthetic fallback for an occupied key.
            foreach (var map in asset.actionMaps)
                foreach (var action in map.actions)
                    for (int i = 0; i < action.bindings.Count; i++)
                        if (string.Equals(action.bindings[i].overridePath, "<Keyboard>/anyKey",
                                StringComparison.OrdinalIgnoreCase))
                            action.RemoveBindingOverride(i);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not load input bindings: {exception.Message}");
        }
    }

    public void StartRebinding(ActionBindingButton button, InputAction action, int bindingIndex)
    {
        if (!initialized || !isActiveAndEnabled || !button || !inputActions ||
            action == null || action.actionMap?.asset != inputActions || bindingIndex < 0 ||
            bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite)
            return;

        CancelRebinding();
        rebindingButton = button;
        button.ShowWaitingForInput();
        try
        {
            rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithControlsExcluding("<Keyboard>/anyKey")
                .WithMatchingEventsBeingSuppressed()
                .OnMatchWaitForAnother(0f)
                .OnPotentialMatch(operation =>
                {
                    CompleteRebindingIfAvailable(operation, action, bindingIndex);
                })
                .OnCancel(_ => FinishRebinding(false))
                .OnComplete(_ => FinishRebinding(true));

            string path = action.bindings[bindingIndex].path;
            if (path.StartsWith("<Keyboard>/") || path.StartsWith("<Mouse>/"))
                rebindOperation.WithControlsHavingToMatchPath("<Keyboard>/*")
                    .WithControlsHavingToMatchPath("<Mouse>/*");
            else if (path.StartsWith("<Gamepad>/"))
                rebindOperation.WithControlsHavingToMatchPath("<Gamepad>/*");

            if (action.bindings[bindingIndex].isPartOfComposite)
                rebindOperation.WithExpectedControlType("Button");
            rebindOperation.Start();
        }
        catch (Exception exception)
        {
            FinishRebinding(false);
            Debug.LogWarning($"Could not start rebinding: {exception.Message}", this);
        }
    }

    public void CancelRebinding()
    {
        if (rebindOperation != null)
            rebindOperation.Cancel();
    }

    private static void CompleteRebindingIfAvailable(
        InputActionRebindingExtensions.RebindingOperation operation, InputAction action, int bindingIndex)
    {
        if (operation.candidates.Count == 0)
            return;

        // Reject the whole input event when its best match is occupied. Falling back
        // to a lower-scoring synthetic control (such as anyKey) bypasses the check.
        if (IsControlAlreadyBound(action, bindingIndex, operation.candidates[0]))
        {
            while (operation.candidates.Count > 0)
                operation.RemoveCandidate(operation.candidates[0]);
            return;
        }

        operation.Complete();
    }

    private static bool IsControlAlreadyBound(InputAction action, int bindingIndex, InputControl control)
    {
        // Check all gameplay actions, including ones without a settings button.
        // UI navigation in other maps intentionally shares gameplay controls.
        foreach (var otherAction in action.actionMap.actions)
        {
            for (int i = 0; i < otherAction.bindings.Count; i++)
            {
                var binding = otherAction.bindings[i];
                if (binding.isComposite || (otherAction == action && i == bindingIndex) ||
                    string.IsNullOrEmpty(binding.effectivePath))
                    continue;

                if (InputControlPath.Matches(binding.effectivePath, control))
                    return true;
            }
        }
        return false;
    }

    public void CancelRebinding(ActionBindingButton button)
    {
        if (rebindingButton == button)
            CancelRebinding();
    }

    private void FinishRebinding(bool save)
    {
        lastRebindFrame = Time.frameCount;
        var operation = rebindOperation;
        var button = rebindingButton;
        rebindOperation = null;
        rebindingButton = null;
        operation?.Dispose();
        if (button) button.RefreshDisplay();
        if (save) SaveInputBindings();
    }

    public void SaveInputBindings()
    {
        if (!inputActions || !SaveSystem.savesLoaded || settings == null)
            return;

        settings.inputBindingOverrides = inputActions.SaveBindingOverridesAsJson();
        settingsChanged = true;
        RefreshInputBindingButtons();
        InputBindingsChanged?.Invoke();
    }

    private void RefreshInputBindingButtons()
    {
        if (actionsButtons == null)
            return;
        foreach (var button in actionsButtons)
            if (button) button.RefreshDisplay();
    }

    public void ResetInputBindings()
    {
        CancelRebinding();
        if (!initialized || !inputActions)
            return;
        inputActions.RemoveAllBindingOverrides();
        SaveInputBindings();
    }

    public void ShowSection(int index)
    {
        if (sections == null || index < 0 || index >= sections.Length || !sections[index])
            return;

        for (int i = 0; i < sections.Length; i++)
        {
            if (sections[i])
                sections[i].SetActive(i == index);
        }

        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            if (!buttons[i])
                continue;

            buttons[i].Deselect();
            if (i == index)
                buttons[i].Select();
        }
    }

    private void SetupUI()
    {
        SetupDisplays();
        SetupResolutions();
//         SetupUpscalingDropdown();
//         SetupDLSSQualityDropdown();

        SetupAudioSliders();
        SetupSensitivitySlider();
    }

    private void SetupDisplays()
    {
        availableDisplays.Clear();
        Screen.GetDisplayLayout(availableDisplays);
    }

    private int FindCurrentDisplayIndex()
    {
        if (availableDisplays.Count == 0) return 0;
        DisplayInfo current = Screen.mainWindowDisplayInfo;
        int index = availableDisplays.FindIndex(display => display.name == current.name &&
            display.width == current.width && display.height == current.height);
        return index >= 0 ? index : 0;
    }

    private void SetupResolutions()
    {
        Resolution[] displayResolutions = Screen.resolutions
            .GroupBy(r => new
            {
                r.width,
                r.height
            })
            .Select(group => group.Last())
            .OrderBy(r => r.width)
            .ThenBy(r => r.height)
            .ToArray();

        Resolution display = Screen.currentResolution;
        float displayAspect = display.height > 0
            ? (float)display.width / display.height
            : (float)Screen.width / Mathf.Max(1, Screen.height);

        // FullscreenWindow preserves the desktop aspect. Other aspect ratios
        // are letterboxed by the display, even though SetResolution succeeds.
        availableResolutions = displayResolutions
            .Where(r => r.height > 0 &&
                Mathf.Abs((float)r.width / r.height - displayAspect) < 0.015f)
            .ToArray();

        if (availableResolutions.Length == 0)
        {
            availableResolutions = new[]
            {
                new Resolution { width = display.width, height = display.height,
                    refreshRateRatio = display.refreshRateRatio }
            };
        }

        // Older saves stored only an index into the unfiltered resolution list.
        // Migrate that choice before the list changes, then keep dimensions so
        // future changes to the OS resolution list do not shift saved choices.
        if (settings == null)
            return;

        int preferredWidth = settings.resolutionWidth;
        int preferredHeight = settings.resolutionHeight;
        if (preferredWidth <= 0 || preferredHeight <= 0)
        {
            if (settings.resolutionIndex >= 0 && settings.resolutionIndex < displayResolutions.Length)
            {
                preferredWidth = displayResolutions[settings.resolutionIndex].width;
                preferredHeight = displayResolutions[settings.resolutionIndex].height;
            }
            else
            {
                preferredWidth = Screen.width;
                preferredHeight = Screen.height;
            }
        }

        int closestIndex = 0;
        float closestDifference = float.MaxValue;
        for (int i = 0; i < availableResolutions.Length; i++)
        {
            Resolution candidate = availableResolutions[i];
            float difference =
                Mathf.Abs(candidate.width - preferredWidth) / (float)Mathf.Max(1, preferredWidth) +
                Mathf.Abs(candidate.height - preferredHeight) / (float)Mathf.Max(1, preferredHeight);
            if (difference >= closestDifference)
                continue;
            closestDifference = difference;
            closestIndex = i;
        }

        Resolution closest = availableResolutions[closestIndex];
        if (settings.resolutionIndex != closestIndex ||
            settings.resolutionWidth != closest.width ||
            settings.resolutionHeight != closest.height)
            settingsChanged = true;
        settings.resolutionIndex = closestIndex;
        settings.resolutionWidth = closest.width;
        settings.resolutionHeight = closest.height;
    }

//     private void SetupUpscalingDropdown()
//     {
//         if (upscalingDropdown == null)
//             return;
// 
//         upscalingDropdown.ClearOptions();
//         upscalingDropdown.AddOptions(
//             upscalingLabels.ToList()
//         );
//     }
// 
//     private void SetupDLSSQualityDropdown()
//     {
//         if (dlssQualityDropdown == null)
//             return;
// 
//         dlssQualityDropdown.ClearOptions();
//         dlssQualityDropdown.AddOptions(
//             dlssQualityLabels.ToList()
//         );
//     }
// 
    private void SetupAudioSliders()
    {
        if (masterSlider != null)
        {
            masterSlider.minValue = 0f;
            masterSlider.maxValue = 1f;
            masterSlider.wholeNumbers = false;
            ConfigureButtonControlledSlider(masterSlider, decreaseMasterVolumeButton, increaseMasterVolumeButton);
        }

        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.wholeNumbers = false;
            ConfigureButtonControlledSlider(musicSlider, decreaseMusicVolumeButton, increaseMusicVolumeButton);
        }

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.wholeNumbers = false;
            ConfigureButtonControlledSlider(sfxSlider, decreaseSFXVolumeButton, increaseSFXVolumeButton);
        }
    }

    private void SetupSensitivitySlider()
    {
        if (aimSensitivitySlider == null)
            return;

        aimSensitivitySlider.minValue = MinSensitivity;
        aimSensitivitySlider.maxValue = MaxSensitivity;
        aimSensitivitySlider.wholeNumbers = false;
        ConfigureButtonControlledSlider(aimSensitivitySlider, decreaseSensitivityButton, increaseSensitivityButton);
    }

    private static void ConfigureButtonControlledSlider(Slider slider, Button decrease, Button increase)
    {
        // The bar remains directly draggable when arrow buttons are also present.
        slider.interactable = true;
        if (!decrease && !increase)
            return;

        BlockButtonDrag(decrease);
        BlockButtonDrag(increase);

        // Keep keyboard navigation on the arrow buttons without disabling pointer input.
        var navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;

        void BlockButtonDrag(Button button)
        {
            if (!button || !button.transform.IsChildOf(slider.transform) ||
                button.GetComponent<IDragHandler>() != null) return;

            // Unity resolves the nearest IDragHandler separately from the clicked Button.
            // An empty EventTrigger consumes drag here, preserving the Button's normal click
            // and preventing the parent Slider from jumping to the pointer's endpoint.
            button.gameObject.AddComponent<EventTrigger>();
        }
    }

    #endregion

    #region Listeners

    private void RegisterListeners()
    {
        if (decreaseSensitivityButton) decreaseSensitivityButton.onClick.AddListener(DecreaseSensitivity);
        if (increaseSensitivityButton) increaseSensitivityButton.onClick.AddListener(IncreaseSensitivity);
        if (decreaseMasterVolumeButton) decreaseMasterVolumeButton.onClick.AddListener(DecreaseMasterVolume);
        if (increaseMasterVolumeButton) increaseMasterVolumeButton.onClick.AddListener(IncreaseMasterVolume);
        if (decreaseMusicVolumeButton) decreaseMusicVolumeButton.onClick.AddListener(DecreaseMusicVolume);
        if (increaseMusicVolumeButton) increaseMusicVolumeButton.onClick.AddListener(IncreaseMusicVolume);
        if (decreaseSFXVolumeButton) decreaseSFXVolumeButton.onClick.AddListener(DecreaseSFXVolume);
        if (increaseSFXVolumeButton) increaseSFXVolumeButton.onClick.AddListener(IncreaseSFXVolume);
        if (previousResolutionButton) previousResolutionButton.onClick.AddListener(PreviousResolution);
        if (nextResolutionButton) nextResolutionButton.onClick.AddListener(NextResolution);
        if (previousWindowModeButton) previousWindowModeButton.onClick.AddListener(PreviousWindowMode);
        if (nextWindowModeButton) nextWindowModeButton.onClick.AddListener(NextWindowMode);
        if (previousDisplayButton) previousDisplayButton.onClick.AddListener(PreviousDisplay);
        if (nextDisplayButton) nextDisplayButton.onClick.AddListener(NextDisplay);
        if (previousFrameRateButton) previousFrameRateButton.onClick.AddListener(PreviousFrameRate);
        if (nextFrameRateButton) nextFrameRateButton.onClick.AddListener(NextFrameRate);
        if (previousQualityButton) previousQualityButton.onClick.AddListener(PreviousQuality);
        if (nextQualityButton) nextQualityButton.onClick.AddListener(NextQuality);

        if (vSyncOffButton) vSyncOffButton.onClick.AddListener(DisableVSync);
        if (vSyncOnButton) vSyncOnButton.onClick.AddListener(EnableVSync);

//         if (upscalingDropdown != null)
//             upscalingDropdown.onValueChanged.AddListener(
//                 OnUpscalingChanged
//             );
// 
//         if (dlssQualityDropdown != null)
//             dlssQualityDropdown.onValueChanged.AddListener(
//                 OnDLSSQualityChanged
//             );

        if (masterSlider != null)
            masterSlider.onValueChanged.AddListener(
                OnMasterVolumeChanged
            );
        
        if (musicSlider != null)
            musicSlider.onValueChanged.AddListener(
                OnMusicVolumeChanged
            );

        if (sfxSlider != null)
            sfxSlider.onValueChanged.AddListener(
                OnSFXVolumeChanged
            );

        if (aimSensitivitySlider != null)
            aimSensitivitySlider.onValueChanged.AddListener(
                OnAimSensitivityChangedInternal
            );

    }

    private void UnregisterListeners()
    {
        if (decreaseSensitivityButton) decreaseSensitivityButton.onClick.RemoveListener(DecreaseSensitivity);
        if (increaseSensitivityButton) increaseSensitivityButton.onClick.RemoveListener(IncreaseSensitivity);
        if (decreaseMasterVolumeButton) decreaseMasterVolumeButton.onClick.RemoveListener(DecreaseMasterVolume);
        if (increaseMasterVolumeButton) increaseMasterVolumeButton.onClick.RemoveListener(IncreaseMasterVolume);
        if (decreaseMusicVolumeButton) decreaseMusicVolumeButton.onClick.RemoveListener(DecreaseMusicVolume);
        if (increaseMusicVolumeButton) increaseMusicVolumeButton.onClick.RemoveListener(IncreaseMusicVolume);
        if (decreaseSFXVolumeButton) decreaseSFXVolumeButton.onClick.RemoveListener(DecreaseSFXVolume);
        if (increaseSFXVolumeButton) increaseSFXVolumeButton.onClick.RemoveListener(IncreaseSFXVolume);
        if (masterSlider != null)
            masterSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);

        if (previousResolutionButton) previousResolutionButton.onClick.RemoveListener(PreviousResolution);
        if (nextResolutionButton) nextResolutionButton.onClick.RemoveListener(NextResolution);
        if (previousWindowModeButton) previousWindowModeButton.onClick.RemoveListener(PreviousWindowMode);
        if (nextWindowModeButton) nextWindowModeButton.onClick.RemoveListener(NextWindowMode);
        if (previousDisplayButton) previousDisplayButton.onClick.RemoveListener(PreviousDisplay);
        if (nextDisplayButton) nextDisplayButton.onClick.RemoveListener(NextDisplay);
        if (previousFrameRateButton) previousFrameRateButton.onClick.RemoveListener(PreviousFrameRate);
        if (nextFrameRateButton) nextFrameRateButton.onClick.RemoveListener(NextFrameRate);
        if (previousQualityButton) previousQualityButton.onClick.RemoveListener(PreviousQuality);
        if (nextQualityButton) nextQualityButton.onClick.RemoveListener(NextQuality);

        if (vSyncOffButton) vSyncOffButton.onClick.RemoveListener(DisableVSync);
        if (vSyncOnButton) vSyncOnButton.onClick.RemoveListener(EnableVSync);

//         if (upscalingDropdown != null)
//             upscalingDropdown.onValueChanged.RemoveListener(
//                 OnUpscalingChanged
//             );
// 
//         if (dlssQualityDropdown != null)
//             dlssQualityDropdown.onValueChanged.RemoveListener(
//                 OnDLSSQualityChanged
//             );

        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(
                OnMusicVolumeChanged
            );

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(
                OnSFXVolumeChanged
            );

        if (aimSensitivitySlider != null)
            aimSensitivitySlider.onValueChanged.RemoveListener(
                OnAimSensitivityChangedInternal
            );

    }

    #endregion

    #region Resolution

    public void PreviousWindowMode() => ChangeWindowMode();
    public void NextWindowMode() => ChangeWindowMode();

    public void PreviousDisplay() => ChangeDisplay(-1);
    public void NextDisplay() => ChangeDisplay(1);

    private void ChangeDisplay(int direction)
    {
        if (!initialized || settings == null) return;
        SetupDisplays();
        if (availableDisplays.Count < 2) { UpdateVideoValues(); return; }
        int index = (settings.displayIndex + direction + availableDisplays.Count) % availableDisplays.Count;
        settings.displayIndex = index;
        settings.displayName = availableDisplays[index].name;
        ApplyResolution();
        UpdateVideoValues();
        settingsChanged = true;
    }

    private void ChangeWindowMode()
    {
        if (!initialized || settings == null)
            return;

        settings.windowModeIndex = 1 - settings.windowModeIndex;
        ApplyResolution();
        UpdateVideoValues();
        settingsChanged = true;
    }

    public void PreviousResolution() => ChangeResolution(-1);
    public void NextResolution() => ChangeResolution(1);

    private void ChangeResolution(int direction)
    {
        if (!initialized || settings == null || availableResolutions == null || availableResolutions.Length < 2)
            return;

        OnResolutionChanged((settings.resolutionIndex + direction + availableResolutions.Length) % availableResolutions.Length);
    }

    private void OnResolutionChanged(int index)
    {
        settings.resolutionIndex = index;
        settings.resolutionWidth = availableResolutions[index].width;
        settings.resolutionHeight = availableResolutions[index].height;

        ApplyResolution();
        UpdateVideoValues();

        settingsChanged = true;
    }

    private void ApplyResolution()
    {
        if (displayMovePending) return;
#if !UNITY_EDITOR
        if (availableDisplays.Count > 0 && settings != null &&
            settings.displayIndex >= 0 && settings.displayIndex < availableDisplays.Count &&
            FindCurrentDisplayIndex() != settings.displayIndex)
        {
            displayMovePending = true;
            int requestedIndex = settings.displayIndex;
            DisplayInfo target = availableDisplays[requestedIndex];
            var move = Screen.MoveMainWindowTo(target, Vector2Int.zero);
            if (move != null)
            {
                move.completed += _ =>
                {
                    displayMovePending = false;
                    if (!this || settings == null) return;
                    SetupDisplays();
                    if (settings.displayIndex != requestedIndex)
                    {
                        // The player selected another display while this move was pending.
                        SetupResolutions();
                        ApplyResolution();
                        UpdateVideoValues();
                        return;
                    }
                    if (FindCurrentDisplayIndex() != requestedIndex)
                    {
                        // A removed monitor must not leave the game window off screen.
                        settings.displayIndex = FindCurrentDisplayIndex();
                        settings.displayName = availableDisplays.Count > 0
                            ? availableDisplays[settings.displayIndex].name : null;
                        settingsChanged = true;
                    }
                    SetupResolutions();
                    ApplyResolutionNow();
                    UpdateVideoValues();
                };
                return;
            }
            displayMovePending = false;
        }
#endif
        ApplyResolutionNow();
    }

    private void ApplyResolutionNow()
    {
        if (availableResolutions == null)
            return;

        if (settings.resolutionIndex < 0 ||
            settings.resolutionIndex >= availableResolutions.Length)
            return;

        Resolution resolution =
            availableResolutions[settings.resolutionIndex];

        Screen.SetResolution(
            resolution.width,
            resolution.height,
            settings.windowModeIndex == 1
                ? FullScreenMode.Windowed
                : FullScreenMode.FullScreenWindow
        );
    }

    #endregion

    #region VSync

    public void EnableVSync() => OnVSyncChanged(true);
    public void DisableVSync() => OnVSyncChanged(false);

    private void OnVSyncChanged(bool enabled)
    {
        if (!initialized || settings == null || settings.vSync == enabled)
            return;

        settings.vSync = enabled;

        ApplyVSync();
        UpdateVideoValues();

        settingsChanged = true;
    }

    private void ApplyVSync()
    {
        QualitySettings.vSyncCount =
            settings.vSync ? 1 : 0;

        ApplyFrameRate();
    }

    private void UpdateVSyncButton(Button button, bool selected)
    {
        if (!button)
            return;

        button.interactable = initialized;
        if (button.targetGraphic)
            button.targetGraphic.color = selected ? vSyncSelectedBackground : vSyncUnselectedBackground;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label)
            label.color = selected ? vSyncSelectedText : vSyncUnselectedText;
    }

    #endregion

    #region Frame Rate

    public void PreviousFrameRate() => ChangeFrameRate(-1);
    public void NextFrameRate() => ChangeFrameRate(1);

    private void ChangeFrameRate(int direction)
    {
        if (!initialized || settings == null)
            return;

        OnFrameRateChanged((settings.frameRateIndex + direction + frameRateValues.Length) % frameRateValues.Length);
    }

    private void OnFrameRateChanged(int index)
    {
        settings.frameRateIndex = index;

        ApplyFrameRate();

        UpdateVideoValues();
        settingsChanged = true;
    }

    private void ApplyFrameRate()
    {
        if (settings.frameRateIndex < 0 ||
            settings.frameRateIndex >= frameRateValues.Length)
            return;

        /*
         * When VSync is enabled Unity synchronizes
         * rendering to the display refresh rate.
         */
        if (settings.vSync)
        {
            Application.targetFrameRate = -1;
            return;
        }

        Application.targetFrameRate =
            frameRateValues[settings.frameRateIndex];
    }

    #endregion

    #region Quality

    public void PreviousQuality() => ChangeQuality(-1);
    public void NextQuality() => ChangeQuality(1);

    private void ChangeQuality(int direction)
    {
        if (!initialized || settings == null || QualityPresetCount < 2)
            return;

        OnQualityChanged((settings.qualityIndex + direction + QualityPresetCount) % QualityPresetCount);
    }

    private void OnQualityChanged(int index)
    {
        if (index < 0 || index >= QualityPresetCount)
            return;

        settings.qualityIndex = index;

        ApplyQuality();

        UpdateVideoValues();
        settingsChanged = true;
    }

    private void ApplyQuality()
    {
        if (settings.qualityIndex < 0 ||
            settings.qualityIndex >= QualityPresetCount)
            return;

        QualityPreset preset = qualityPresets[settings.qualityIndex];
        if (preset == null || !EnsureRuntimePipeline())
            return;

        // HDRP renders in HDR and exposes quality settings through its asset and Volumes.
        var pipelineSettings = runtimePipeline.currentPlatformRenderPipelineSettings;
        pipelineSettings.msaaSampleCount = (MSAASamples)(int)preset.msaa;
        pipelineSettings.supportSSAO = preset.screenSpaceAmbientOcclusion;
        var dynamicResolution = pipelineSettings.dynamicResolutionSettings;
        dynamicResolution.enabled = true;
        dynamicResolution.dynResType = DynamicResolutionType.Software;
        dynamicResolution.forceResolution = true;
        dynamicResolution.forcedPercentage = Mathf.Clamp(preset.renderScale, 0.25f, 1f) * 100f;
        pipelineSettings.dynamicResolutionSettings = dynamicResolution;
        var shadows = pipelineSettings.hdShadowInitParams;
        int shadowResolution = Mathf.NextPowerOfTwo(Mathf.Clamp(preset.shadowResolution, 256, 4096));
        shadows.maxDirectionalShadowMapResolution = shadowResolution;
        shadows.shadowResolutionDirectional = new IntScalableSetting(
            new[] { shadowResolution, shadowResolution, shadowResolution, shadowResolution },
            ScalableSettingSchemaId.With4Levels);
        pipelineSettings.hdShadowInitParams = shadows;
        runtimePipeline.currentPlatformRenderPipelineSettings = pipelineSettings;

        runtimeQualityProfile.TryGet<HDShadowSettings>(out var shadowSettings);
        shadowSettings.maxShadowDistance.Override(Mathf.Max(0f, preset.shadowDistance));
        shadowSettings.cascadeShadowSplitCount.Override(Mathf.Clamp(preset.shadowCascades, 1, 4));
        // When enabled, retain the scene's authored AO intensity.
        runtimeQualityProfile.TryGet<ScreenSpaceAmbientOcclusion>(out var ambientOcclusion);
        ambientOcclusion.intensity.value = 0f;
        ambientOcclusion.intensity.overrideState = !preset.screenSpaceAmbientOcclusion;
        QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(preset.textureMipmapLimit, 0, 3);
        QualitySettings.lodBias = Mathf.Clamp(preset.lodBias, 0.1f, 4f);
    }

    private static void EnableDynamicResolution(ScriptableRenderContext context, List<Camera> cameras)
    {
        foreach (var camera in cameras)
        {
            if (camera.cameraType != CameraType.Game ||
                !camera.TryGetComponent<HDAdditionalCameraData>(out var cameraData))
                continue;
            if (!originalDynamicResolutionStates.ContainsKey(cameraData))
                originalDynamicResolutionStates.Add(cameraData, cameraData.allowDynamicResolution);
            cameraData.allowDynamicResolution = true;
        }
    }

    private bool EnsureRuntimePipeline()
    {
        if (!runtimePipeline)
        {
            var source = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline :
                GraphicsSettings.defaultRenderPipeline) as HDRenderPipelineAsset;
            if (!source)
            {
                Debug.LogError("Quality presets require a High Definition Render Pipeline asset.", this);
                return false;
            }

            originalQualityPipeline = QualitySettings.renderPipeline;
            originalTextureMipmapLimit = QualitySettings.globalTextureMipmapLimit;
            originalLodBias = QualitySettings.lodBias;
            runtimePipeline = Instantiate(source);
            runtimePipeline.name = source.name + " (Runtime Quality)";
            runtimePipeline.hideFlags = HideFlags.DontSave;
            var volumeObject = new GameObject("Runtime HDRP Quality") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(volumeObject);
            runtimeQualityProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeQualityProfile.hideFlags = HideFlags.DontSave;
            runtimeQualityProfile.Add<HDShadowSettings>();
            runtimeQualityProfile.Add<ScreenSpaceAmbientOcclusion>();
            runtimeQualityVolume = volumeObject.AddComponent<Volume>();
            runtimeQualityVolume.isGlobal = true;
            runtimeQualityVolume.priority = float.MaxValue;
            runtimeQualityVolume.sharedProfile = runtimeQualityProfile;
            RenderPipelineManager.beginContextRendering -= EnableDynamicResolution;
            RenderPipelineManager.beginContextRendering += EnableDynamicResolution;
            Application.quitting -= ReleaseRuntimePipeline;
            Application.quitting += ReleaseRuntimePipeline;
        }

        if (QualitySettings.renderPipeline != runtimePipeline)
            QualitySettings.renderPipeline = runtimePipeline;

        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReleaseRuntimePipeline()
    {
        Application.quitting -= ReleaseRuntimePipeline;
        RenderPipelineManager.beginContextRendering -= EnableDynamicResolution;
        foreach (var entry in originalDynamicResolutionStates)
        {
            if (entry.Key)
                entry.Key.allowDynamicResolution = entry.Value;
        }
        originalDynamicResolutionStates.Clear();
        if (runtimeQualityVolume) Destroy(runtimeQualityVolume.gameObject);
        if (runtimeQualityProfile)
        {
            foreach (var component in runtimeQualityProfile.components)
                if (component) Destroy(component);
            Destroy(runtimeQualityProfile);
        }
        runtimeQualityVolume = null;
        runtimeQualityProfile = null;

        if (!runtimePipeline)
            return;

        if (QualitySettings.renderPipeline == runtimePipeline)
            QualitySettings.renderPipeline = originalQualityPipeline;

        QualitySettings.globalTextureMipmapLimit = originalTextureMipmapLimit;
        QualitySettings.lodBias = originalLodBias;
        Destroy(runtimePipeline);
        runtimePipeline = null;
        originalQualityPipeline = null;
    }

    #endregion

//     #region Upscaling
// 
//     private void OnUpscalingChanged(int index)
//     {
//         settings.upscalingMode =
//             (UpscalingMode)index;
// 
// //         ApplyUpscaling();
// 
//         SaveSettings();
//     }
// 
//     private void OnDLSSQualityChanged(int index)
//     {
//         settings.dlssQuality =
//             (DLSSQualityMode)index;
// 
// //         ApplyUpscaling();
// 
//         SaveSettings();
//     }
// 
//     private void ApplyUpscaling()
//     {
//         bool dlssSelected =
//             settings.upscalingMode ==
//             UpscalingMode.DLSS;
// 
//         UpdateDLSSUIState(dlssSelected);
// 
//         if (!dlssSelected)
//         {
//             dlssProvider?.DisableDLSS();
//             return;
//         }
// 
//         if (dlssProvider == null)
//         {
//             Debug.LogWarning(
//                 "DLSS is selected, but no IDLSSProvider " +
//                 "component is assigned."
//             );
// 
//             return;
//         }
// 
//         if (!dlssProvider.IsSupported())
//         {
//             Debug.LogWarning(
//                 "DLSS is not supported on this system."
//             );
// 
//             settings.upscalingMode =
//                 UpscalingMode.Off;
// 
//             if (upscalingDropdown != null)
//             {
//                 upscalingDropdown.SetValueWithoutNotify(
//                     (int)UpscalingMode.Off
//                 );
//             }
// 
//             UpdateDLSSUIState(false);
// 
//             dlssProvider.DisableDLSS();
// 
//             return;
//         }
// 
//         dlssProvider.EnableDLSS(
//             settings.dlssQuality
//         );
//     }
// 
//     private void UpdateDLSSUIState(
//         bool dlssSelected)
//     {
//         if (dlssQualityDropdown == null)
//             return;
// 
//         bool supported =
//             dlssProvider != null &&
//             dlssProvider.IsSupported();
// 
//         dlssQualityDropdown.interactable =
//             dlssSelected &&
//             supported;
//     }
// 
//     #endregion

    #region Audio

    private const float VolumeStep = 0.05f;

    public void DecreaseMasterVolume() => ChangeMasterVolume(-VolumeStep);
    public void IncreaseMasterVolume() => ChangeMasterVolume(VolumeStep);
    public void DecreaseMusicVolume() => ChangeMusicVolume(-VolumeStep);
    public void IncreaseMusicVolume() => ChangeMusicVolume(VolumeStep);
    public void DecreaseSFXVolume() => ChangeSFXVolume(-VolumeStep);
    public void IncreaseSFXVolume() => ChangeSFXVolume(VolumeStep);

    private static float StepVolume(float current, float delta)
    {
        return Mathf.Clamp01(Mathf.Round((current + delta) * 100f) / 100f);
    }

    private void ChangeMasterVolume(float delta)
    {
        if (!initialized || settings == null)
            return;

        float value = StepVolume(settings.masterVolume, delta);
        if (value != settings.masterVolume)
            OnMasterVolumeChanged(value);
    }

    private void ChangeMusicVolume(float delta)
    {
        if (!initialized || settings == null)
            return;

        float value = StepVolume(settings.musicVolume, delta);
        if (value != settings.musicVolume)
            OnMusicVolumeChanged(value);
    }

    private void ChangeSFXVolume(float delta)
    {
        if (!initialized || settings == null)
            return;

        float value = StepVolume(settings.effectsVolume, delta);
        if (value != settings.effectsVolume)
            OnSFXVolumeChanged(value);
    }

    private void UpdateAudioValues()
    {
        if (settings == null)
            return;

        UpdateVolumeUI(settings.masterVolume, decreaseMasterVolumeButton, increaseMasterVolumeButton,
            masterVolumeValueText, masterSlider);
        UpdateVolumeUI(settings.musicVolume, decreaseMusicVolumeButton, increaseMusicVolumeButton,
            musicVolumeValueText, musicSlider);
        UpdateVolumeUI(settings.effectsVolume, decreaseSFXVolumeButton, increaseSFXVolumeButton,
            sfxVolumeValueText, sfxSlider);
    }

    private void UpdateVolumeUI(float volume, Button decrease, Button increase, TMP_Text valueText, Slider slider)
    {
        if (decrease) decrease.interactable = initialized && volume > 0f;
        if (increase) increase.interactable = initialized && volume < 1f;
        if (valueText) valueText.text = Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f).ToString();
        if (slider) slider.SetValueWithoutNotify(volume);
    }

    private void OnMasterVolumeChanged(float value)
    {
        settings.masterVolume = Mathf.Clamp01(value);

        ApplyMasterVolume();
        UpdateAudioValues();

        settingsChanged = true;
    }
    
    private void OnMusicVolumeChanged(float value)
    {
        settings.musicVolume = Mathf.Clamp01(value);

        ApplyMusicVolume();
        UpdateAudioValues();

        settingsChanged = true;
    }

    private void OnSFXVolumeChanged(float value)
    {
        settings.effectsVolume = Mathf.Clamp01(value);

        ApplySFXVolume();
        UpdateAudioValues();

        settingsChanged = true;
    }

    private void ApplyMasterVolume()
    {
        SoundManager.instance?.VolumeMaster(settings.masterVolume);
    }
    
    private void ApplyMusicVolume()
    {
        SoundManager.instance?.VolumeMusic(settings.musicVolume);
    }

    private void ApplySFXVolume()
    {
        SoundManager.instance?.VolumeEffects(settings.effectsVolume);
    }

    #endregion

    #region Aim Sensitivity

    private const float SensitivityStep = 0.05f;
    private const float MinSensitivity = 0.1f;
    private const float MaxSensitivity = 5f;

    public void DecreaseSensitivity() => ChangeSensitivity(-SensitivityStep);
    public void IncreaseSensitivity() => ChangeSensitivity(SensitivityStep);

    private void ChangeSensitivity(float delta)
    {
        if (!initialized || settings == null)
            return;

        float value = Mathf.Clamp(Mathf.Round((settings.aimSensitivity + delta) * 100f) / 100f,
            MinSensitivity, MaxSensitivity);
        OnAimSensitivityChangedInternal(value);
    }

    private void UpdateSensitivityValues()
    {
        if (settings == null)
            return;

        if (decreaseSensitivityButton)
            decreaseSensitivityButton.interactable = initialized && settings.aimSensitivity > MinSensitivity;
        if (increaseSensitivityButton)
            increaseSensitivityButton.interactable = initialized && settings.aimSensitivity < MaxSensitivity;
        if (aimSensitivitySlider)
            aimSensitivitySlider.SetValueWithoutNotify(settings.aimSensitivity);
        if (aimSensitivityValueText)
            aimSensitivityValueText.text = settings.aimSensitivity.ToString("0.00");
    }

    private void OnAimSensitivityChangedInternal(
        float value)
    {
        if (!initialized || settings == null)
            return;

        value = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        if (Mathf.Approximately(settings.aimSensitivity, value))
            return;

        settings.aimSensitivity = value;

        ApplyAimSensitivity();

        settingsChanged = true;
    }

    private void ApplyAimSensitivity()
    {
        AimSensitivity =
            settings.aimSensitivity;
        UpdateSensitivityValues();

        OnAimSensitivityChanged?.Invoke(
            AimSensitivity
        );
    }

    #endregion

    #region Settings Data

    private void SaveSettings()
    {
        if (!initialized || !settingsChanged || !SaveSystem.savesLoaded || settings == null)
            return;

        SaveSystem.SaveData();
        settingsChanged = false;
    }

    private SettingsData CreateDefaultSettings()
    {
        Resolution display = Screen.currentResolution;
        int displayIndex = Array.FindIndex(availableResolutions,
            r => r.width == display.width && r.height == display.height);
        if (displayIndex < 0)
            displayIndex = FindCurrentResolutionIndex();

        return new SettingsData
        {
            resolutionIndex = displayIndex,
            resolutionWidth = availableResolutions[displayIndex].width,
            resolutionHeight = availableResolutions[displayIndex].height,
            windowModeIndex = 0,
            displayIndex = FindCurrentDisplayIndex(),
            displayName = availableDisplays.Count > 0 ? availableDisplays[FindCurrentDisplayIndex()].name : null,

            vSync = false,

            frameRateIndex = 2,

            qualityIndex =
                Mathf.Max(
                    0,
                    QualityPresetCount - 1
                ),

//             upscalingMode =
//                 UpscalingMode.Off,
// 
//             dlssQuality =
//                 DLSSQualityMode.Quality,

            musicVolume = 1f,

            effectsVolume = 1f,

            aimSensitivity = 1f
        };
    }

    private void ValidateSettings()
    {
        settings.windowModeIndex = Mathf.Clamp(settings.windowModeIndex, 0, 1);
        if (availableDisplays.Count > 0)
        {
            int displayIndex = settings.displayName == null ? FindCurrentDisplayIndex() :
                availableDisplays.FindIndex(display => display.name == settings.displayName);
            if (displayIndex < 0) displayIndex = FindCurrentDisplayIndex();
            if (settings.displayIndex != displayIndex || settings.displayName != availableDisplays[displayIndex].name)
                settingsChanged = true;
            settings.displayIndex = displayIndex;
            settings.displayName = availableDisplays[displayIndex].name;
        }

        if (settings.resolutionIndex < 0)
            settings.resolutionIndex = FindCurrentResolutionIndex();

        if (settings.qualityIndex < 0)
            settings.qualityIndex = Mathf.Max(0, QualityPresetCount - 1);

        if (availableResolutions != null &&
            availableResolutions.Length > 0)
        {
            settings.resolutionIndex =
                Mathf.Clamp(
                    settings.resolutionIndex,
                    0,
                    availableResolutions.Length - 1
                );
        }

        settings.frameRateIndex =
            Mathf.Clamp(
                settings.frameRateIndex,
                0,
                frameRateValues.Length - 1
            );

        settings.qualityIndex =
            Mathf.Clamp(
                settings.qualityIndex,
                0,
                Mathf.Max(
                    0,
                    QualityPresetCount - 1
                )
            );

        settings.masterVolume = Mathf.Clamp01(settings.masterVolume);

        settings.musicVolume =
            Mathf.Clamp01(
                settings.musicVolume
            );

        settings.effectsVolume =
            Mathf.Clamp01(
                settings.effectsVolume
            );

        settings.aimSensitivity =
            Mathf.Clamp(
                settings.aimSensitivity,
                MinSensitivity,
                MaxSensitivity
            );
    }

    private int FindCurrentResolutionIndex()
    {
        if (availableResolutions == null ||
            availableResolutions.Length == 0)
            return 0;

        for (int i = 0;
             i < availableResolutions.Length;
             i++)
        {
            Resolution resolution =
                availableResolutions[i];

            if (resolution.width == Screen.width &&
                resolution.height == Screen.height)
            {
                return i;
            }
        }

        return availableResolutions.Length - 1;
    }

    #endregion

    #region Update UI

    private void UpdateUI()
    {
        UpdateVideoValues();
        UpdateAudioValues();

//         if (upscalingDropdown != null)
//         {
//             upscalingDropdown.SetValueWithoutNotify(
//                 (int)settings.upscalingMode
//             );
//         }
// 
//         if (dlssQualityDropdown != null)
//         {
//             dlssQualityDropdown.SetValueWithoutNotify(
//                 (int)settings.dlssQuality
//             );
//         }

        if (masterSlider != null)
        {
            masterSlider.SetValueWithoutNotify(
                settings.masterVolume
            );
        }
        
        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(
                settings.musicVolume
            );
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(
                settings.effectsVolume
            );
        }

        if (aimSensitivitySlider != null)
        {
            aimSensitivitySlider.SetValueWithoutNotify(
                settings.aimSensitivity
            );
        }

        if (aimSensitivityValueText != null)
        {
            aimSensitivityValueText.text =
                settings.aimSensitivity.ToString("0.00");
        }

//         UpdateDLSSUIState(
//             settings.upscalingMode ==
//             UpscalingMode.DLSS
//         );
    }

    #endregion

    private void UpdateVideoValues()
    {
        if (settings == null)
            return;

        UpdateVSyncButton(vSyncOffButton, !settings.vSync);
        UpdateVSyncButton(vSyncOnButton, settings.vSync);

        int resolutionCount = availableResolutions?.Length ?? 0;
        if (resolutionValueText && settings.resolutionIndex >= 0 && settings.resolutionIndex < resolutionCount)
        {
            Resolution resolution = availableResolutions[settings.resolutionIndex];
            resolutionValueText.text = $"{resolution.width} x {resolution.height}";
        }

        if (windowModeValueText)
            windowModeValueText.text = settings.windowModeIndex == 1 ? "Windowed" : "Full Screen";
        if (displayValueText)
            displayValueText.text = "Display " + (availableDisplays.Count > 0 ? settings.displayIndex + 1 : 1);

        if (frameRateValueText && settings.frameRateIndex >= 0 && settings.frameRateIndex < frameRateLabels.Length)
            frameRateValueText.text = frameRateLabels[settings.frameRateIndex];

        if (qualityValueText)
        {
            QualityPreset preset = settings.qualityIndex >= 0 && settings.qualityIndex < QualityPresetCount
                ? qualityPresets[settings.qualityIndex] : null;
            qualityValueText.text = string.IsNullOrWhiteSpace(preset?.name)
                ? "Unavailable" : preset.name;
        }

        if (previousResolutionButton) previousResolutionButton.interactable = initialized && resolutionCount > 1;
        if (nextResolutionButton) nextResolutionButton.interactable = initialized && resolutionCount > 1;
        if (previousWindowModeButton) previousWindowModeButton.interactable = initialized;
        if (nextWindowModeButton) nextWindowModeButton.interactable = initialized;
        if (previousDisplayButton) previousDisplayButton.interactable = initialized && availableDisplays.Count > 1;
        if (nextDisplayButton) nextDisplayButton.interactable = initialized && availableDisplays.Count > 1;
        if (previousFrameRateButton) previousFrameRateButton.interactable = initialized && frameRateValues.Length > 1;
        if (nextFrameRateButton) nextFrameRateButton.interactable = initialized && frameRateValues.Length > 1;
        if (previousQualityButton) previousQualityButton.interactable = initialized && QualityPresetCount > 1;
        if (nextQualityButton) nextQualityButton.interactable = initialized && QualityPresetCount > 1;
    }

    #region Apply Everything

    private void ApplyAllSettings()
    {
        ApplyResolution();

        ApplyQuality();

        ApplyVSync();

//         ApplyUpscaling();

        ApplyMasterVolume();

        ApplyMusicVolume();

        ApplySFXVolume();

        ApplyAimSensitivity();
    }

    #endregion

    #region Reset

    public void ResetToDefaults()
    {
        if (!initialized || !SaveSystem.savesLoaded || settings == null)
            return;

        SettingsData defaults = CreateDefaultSettings();
        ResetInputBindings();
        settings.resolutionIndex = defaults.resolutionIndex;
        settings.resolutionWidth = defaults.resolutionWidth;
        settings.resolutionHeight = defaults.resolutionHeight;
        settings.windowModeIndex = defaults.windowModeIndex;
        settings.displayIndex = defaults.displayIndex;
        settings.displayName = defaults.displayName;
        settings.vSync = defaults.vSync;
        settings.frameRateIndex = defaults.frameRateIndex;
        settings.qualityIndex = defaults.qualityIndex;
//         settings.upscalingMode = defaults.upscalingMode;
//         settings.dlssQuality = defaults.dlssQuality;
        settings.masterVolume = defaults.masterVolume;
        settings.musicVolume = defaults.musicVolume;
        settings.effectsVolume = defaults.effectsVolume;
        settings.aimSensitivity = defaults.aimSensitivity;

        UpdateUI();

        ApplyAllSettings();

        settingsChanged = true;
    }

    #endregion
}

// public enum UpscalingMode
// {
//     Off = 0,
//     DLSS = 1
// }
// 
// public enum DLSSQualityMode
// {
//     Quality = 0,
//     Balanced = 1,
//     Performance = 2,
//     UltraPerformance = 3
// }
// 
// public interface IDLSSProvider
// {
//     bool IsSupported();
// 
//     void EnableDLSS(
//         DLSSQualityMode qualityMode
//     );
// 
//     void DisableDLSS();
// }
// 
