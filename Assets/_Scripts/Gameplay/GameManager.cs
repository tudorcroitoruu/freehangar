using System;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [Tooltip("Bypass the introductory cutscene and start normal gameplay immediately.")]
    public bool skipTutorial = false;
    [Tooltip("Skip the arrival intro cutscene and start gameplay immediately.")]
    public bool skipIntroCutscene = false;
    [Tooltip("Allow testing the final electrical room without clearing the hangar or opening its door first.")]
    public bool bypassFinalRoomRestrictions = false;
    [SerializeField] private GameIntroduction introduction;
    public bool IsIntroPlaying => introduction != null && introduction.IsPlaying;

    private void Awake()
    {
        if (!Instance)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        if (Instance != this || skipTutorial || skipIntroCutscene || introduction == null) return;
        introduction.Play();
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Instance != this || !Input.GetKeyDown(KeyCode.Keypad1) ||
            !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) return;

        var settings = Resources.Load<ShovelUpgradeSettings>(ShovelUpgradeSettings.ResourcePath);
        if (SaveSystem.Data == null || settings == null || !settings.IsConfigured) return;
        int level = SaveSystem.Data.shovelUpgradeLevel;
        if (level < 0 || level >= settings.MaxUpgrades) return;

        // Use the shop transaction, including its price, level limit and save behavior.
        int price = settings.Price(level);
        if (!SaveSystem.TryPurchaseShovelUpgrade(price, level, settings.MaxUpgrades))
        {
            Debug.Log("Shovel upgrade: not enough cash for the next shop upgrade.", this);
            return;
        }
        foreach (var screen in FindObjectsByType<UpgradeShovel>(FindObjectsSortMode.None))
            screen.Refresh();
    }
#endif

    private void OnDestroy() { if (Instance == this) Instance = null; }
}

