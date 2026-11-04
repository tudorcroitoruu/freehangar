using System;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [Header("Gameplay")]
    [Tooltip("Bypass the introductory cutscene and start normal gameplay immediately.")]
    public bool skipTutorial = false;
    [Tooltip("Skip the arrival intro cutscene and start gameplay immediately.")]
    public bool skipIntroCutscene = false;
    [Tooltip("Allow testing the final electrical room without clearing the hangar or opening its door first.")]
    public bool bypassFinalRoomRestrictions = false;
    [SerializeField] private GameIntroduction introduction;
    public bool IsIntroPlaying => introduction != null && introduction.IsPlaying;
    private bool HasSavedIntroduction => SaveSystem.Data?.world?.introPlaying == true && SaveSystem.Data.world.scene == gameObject.scene.name;
    public bool IntroductionPending => (HasSavedIntroduction || (!skipTutorial && !skipIntroCutscene)) && introduction != null &&
        introduction.enabled && introduction.gameObject.activeInHierarchy && !introduction.Completed;

    public bool PrepareIntroductionForReveal() => IntroductionPending && introduction.PrepareOpening();

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (!Instance)
        {
            Instance = this;
        }
        if (Instance != this) return;
        WorldSaveRuntime.EnsureScene(gameObject.scene);

        var settingsManager = GameSettingsManager.Instance;
        if (settingsManager == null)
            settingsManager = FindFirstObjectByType<GameSettingsManager>(FindObjectsInactive.Include);
        if (settingsManager != null) settingsManager.Init();
        PrepareIntroductionForReveal();
    }

    private IEnumerator Start()
    {
        // Cutscene timing and audio start only after the loading curtain is gone.
        while (LoadingScreen.IsLoading) yield return null;
        while (!SaveSystem.savesLoaded || (WorldSaveRuntime.Instance != null && !WorldSaveRuntime.Instance.Ready)) yield return null;
        if (Instance != this || introduction == null) yield break;
        float resume = HasSavedIntroduction ? SaveSystem.Data.world.introTime : 0;
        if (((skipTutorial || skipIntroCutscene) && !HasSavedIntroduction) || !introduction.Play(resume)) introduction.Cancel();
    }

#if UNITY_EDITOR
    private static readonly System.Collections.Generic.HashSet<string> editorTestItems = new System.Collections.Generic.HashSet<string>();
    public static bool HasEditorTestItem(string id) => editorTestItems.Contains(id);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearEditorTestItems() => editorTestItems.Clear();

    public void GrantTestPistol()
    {
        if (!Application.isPlaying) return;
        editorTestItems.Add(PlayerCombatTools.PistolFindId);
        Debug.Log("Pistol available for this Play session. Press 4 to select it.", this);
    }

    public void TestTeleportToHangar(int number)
    {
        if (!Application.isPlaying) { Debug.LogWarning("Enter Play mode before using the hangar test buttons.", this); return; }
        if (!SaveSystem.savesLoaded || SaveSystem.Data == null) { Debug.LogWarning("Wait for the save to finish loading before using the test button.", this); return; }
        var player = FindFirstObjectByType<PlayerController>();
        var doors = FindObjectsByType<HangarKeyDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        HangarKeyDoor door = null;
        foreach (var candidate in doors) if (candidate.HangarNumber == number) { door = candidate; break; }
        if (player == null || door == null) { Debug.LogWarning("Player or hangar not found in this scene.", this); return; }
        var interactor = player.GetComponent<PlayerInteractor>();
        if (interactor != null && (interactor.HandsOccupied || interactor.DiscoveryBusy)) { Debug.LogWarning("Drop carried items and finish the current interaction before teleporting.", this); return; }
        var bicycle = player.GetComponent<PlayerBicycleInteraction>();
        if (bicycle != null && bicycle.IsRiding && !bicycle.TryDismount()) { Debug.LogWarning("Dismount the vehicle before teleporting.", this); return; }
        editorTestItems.Add(HangarKeyDoor.KeyFor(number));
        editorTestItems.Add(PlayerCombatTools.BroomFindId);
        if (number == 3)
        {
            var director = FindFirstObjectByType<GrainPestDirector>();
            if (director != null) director.ReplayIntroForTesting();
        }
        else if (number == 4)
        {
            var director = FindFirstObjectByType<GrainPestDirector>();
            if (director != null) director.ReplayCrowIntroForTesting();
        }
        Vector3 outward = Vector3.ProjectOnPlane(door.transform.forward, Vector3.up).normalized;
        Vector3 position = door.transform.position + outward * 3f;
        var building = door.GetComponentInParent<HangarGrainVisibility>();
        if (building != null)
        {
            Vector3 origin = building.transform.position;
            float frontDistance = Vector3.Dot(door.transform.position - origin,outward);
            position = origin + outward * (frontDistance + 3f);
        }
        position.y = door.transform.position.y - 1.5f;
        var controller = player.characterController;
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        player.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-outward, Vector3.up));
        if (wasEnabled) controller.enabled = true;
        player.ResetLook();
        Physics.SyncTransforms();
        Debug.Log("Teleported to Hangar " + number + " with temporary test access to its key and broom.", this);
    }

    private void Update()
    {
        if (Instance != this || !Input.GetKeyDown(KeyCode.Keypad1) ||
            !(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))) return;

        var settings = GrainEconomySettings.ResolveShovel(null);
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

