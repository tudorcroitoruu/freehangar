using System;
using UnityEngine;
using UnityEngine.Serialization;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class GoalsManager : MonoBehaviour
{
    [SerializeField] private CanvasGroup ownCanvasGroup;
    [SerializeField] private GoalListElement goalListElementPrefab;
    [SerializeField] private Transform goalListElementParent;

    [Tooltip("Goals are played one at a time, in this array's order.")]
    [SerializeField] private GoalSettings[] allGoalSettings;
    [SerializeField] private GoalSettings currentGoal;
    [SerializeField] private AudioSource goalAchievedSource;
    [SerializeField, Min(0f)] private float goalAchievedDelay = 1.5f;

    private readonly System.Collections.Generic.Dictionary<string, GoalSettingsSave> progress =
        new System.Collections.Generic.Dictionary<string, GoalSettingsSave>();
    private readonly System.Collections.Generic.Queue<GoalAction> pendingActions =
        new System.Collections.Generic.Queue<GoalAction>();
    private PlayerData loadedData;
    private GoalListElement currentElement;
    private int currentIndex;
    private bool initialized, processing, dirty;
    private bool awaitingNextGoal;
    private float goalDelayRemaining;

    public static GoalsManager Instance { get; private set; }
    public GoalSettings CurrentGoal => currentGoal;
    public float CurrentProgress => currentGoal != null && progress.TryGetValue(currentGoal.goalId, out var state)
        ? state.currentAmount : 0f;
    public float NormalizedProgress => currentGoal == null ? 1f : Mathf.Clamp01(CurrentProgress / currentGoal.goalAmount);
    public bool AllGoalsCompleted => initialized && currentGoal == null;
    public event Action GoalChanged;
    public event Action<GoalSettings> GoalCompleted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one GoalsManager can listen to player actions.", this);
            enabled = false;
            return;
        }
        Instance = this;
        initialized = false;
        GoalChecker.ActionReported += ProcessAction;
        RefreshNavigationIndicators();
    }

    private void Update()
    {
        if (!SaveSystem.savesLoaded || SaveSystem.Data == null) return;
        if (!initialized || loadedData != SaveSystem.Data)
        {
            try { Initialize(SaveSystem.Data); }
            catch (Exception error) { Debug.LogException(error, this); enabled = false; return; }
            return;
        }
        AdvanceGoalTransition(Time.deltaTime);
    }

    /// <summary>Bind saved player progress. Called automatically after SaveSystem finishes loading.</summary>
    public void Initialize(PlayerData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        var ids = new System.Collections.Generic.HashSet<string>();
        foreach (var goal in allGoalSettings ?? Array.Empty<GoalSettings>())
        {
            if (goal == null || string.IsNullOrWhiteSpace(goal.goalId) || !ids.Add(goal.goalId) ||
                float.IsNaN(goal.goalAmount) || float.IsInfinity(goal.goalAmount) || goal.goalAmount <= 0 ||
                goal.rewardAmount < 0)
                throw new InvalidOperationException("Each goal needs a unique ID, a positive target and a non-negative reward.");
        }

        loadedData = data;
        awaitingNextGoal = false;
        goalDelayRemaining = 0f;
        progress.Clear();
        foreach (var saved in data.goals ?? Array.Empty<GoalSettingsSave>())
            if (saved != null && !string.IsNullOrEmpty(saved.goalId) && !progress.ContainsKey(saved.goalId))
                progress.Add(saved.goalId, saved);
        foreach (var goal in allGoalSettings ?? Array.Empty<GoalSettings>())
        {
            if (!progress.TryGetValue(goal.goalId, out var saved))
                progress.Add(goal.goalId, saved = new GoalSettingsSave { goalId = goal.goalId });
            if (goal.progressMode != GoalProgressMode.SinceActivation &&
                GoalChecker.TryGetSavedValue(goal, data, out float savedValue))
                saved.currentAmount = goal.progressMode == GoalProgressMode.CurrentValue
                    ? savedValue : Mathf.Max(saved.currentAmount, savedValue);
            if (!float.IsFinite(saved.currentAmount)) saved.currentAmount = 0;
            saved.currentAmount = saved.completed ? goal.goalAmount : Mathf.Clamp(saved.currentAmount, 0, goal.goalAmount);
            if (!saved.completed) saved.rewardClaimed = false;
        }
        data.goals = new System.Collections.Generic.List<GoalSettingsSave>(progress.Values).ToArray();
        currentIndex = 0;
        while (currentIndex < (allGoalSettings?.Length ?? 0))
        {
            var saved = progress[allGoalSettings[currentIndex].goalId];
            if (!saved.completed || !saved.rewardClaimed) break;
            currentIndex++;
        }
        initialized = true;
        SelectCurrentGoal();
        MarkDirty();
        CompleteReadyGoals();
        RefreshGoal();
        DrainActions();
    }

    /// <summary>Also accepts actions arriving while the save is still loading.</summary>
    public void ProcessAction(GoalAction action)
    {
        if (!GoalChecker.IsValid(action)) return;
        pendingActions.Enqueue(action);
        DrainActions();
    }

    private void DrainActions()
    {
        if (!initialized || processing) return;
        processing = true;
        try
        {
            while (pendingActions.Count > 0)
            {
                var action = pendingActions.Dequeue();
                bool changed = false;
                for (int i = currentIndex; i < (allGoalSettings?.Length ?? 0); i++)
                {
                    var goal = allGoalSettings[i];
                    changed |= GoalChecker.TryApply(goal, progress[goal.goalId], action, i == currentIndex);
                }
                if (!changed) continue;
                MarkDirty();
                CompleteReadyGoals();
                RefreshGoal();
            }
        }
        finally { processing = false; }
    }

    private void CompleteReadyGoals()
    {
        if (awaitingNextGoal) return;
        var completed = new System.Collections.Generic.List<GoalSettings>();
        while (currentGoal != null)
        {
            var state = progress[currentGoal.goalId];
            if (!state.completed && state.currentAmount < currentGoal.goalAmount) break;
            // Reward and flags share one save. Rewards do not report MoneyEarned.
            if (!state.rewardClaimed)
            {
                if ((long)loadedData.cash + currentGoal.rewardAmount > int.MaxValue) break;
                loadedData.cash += currentGoal.rewardAmount;
                state.rewardClaimed = true;
            }
            state.completed = true;
            state.currentAmount = currentGoal.goalAmount;
            completed.Add(currentGoal);
            if (goalAchievedSource != null) goalAchievedSource.Play();
            if (goalAchievedDelay > 0f)
            {
                awaitingNextGoal = true;
                goalDelayRemaining = goalAchievedDelay;
                break;
            }
            currentIndex++;
            SelectCurrentGoal();
        }
        if (completed.Count == 0) return;
        MarkDirty();
        FlushSave();
        if (awaitingNextGoal) RefreshGoal();
        foreach (var goal in completed) GoalCompleted?.Invoke(goal);
    }

    private void AdvanceGoalTransition(float deltaTime)
    {
        if (!initialized || !awaitingNextGoal) return;
        goalDelayRemaining -= Mathf.Max(0f, deltaTime);
        if (goalDelayRemaining > 0f) return;
        awaitingNextGoal = false;
        currentIndex++;
        SelectCurrentGoal();
        CompleteReadyGoals();
        RefreshGoal();
    }

    private void SelectCurrentGoal() =>
        currentGoal = currentIndex < (allGoalSettings?.Length ?? 0) ? allGoalSettings[currentIndex] : null;

    private void RefreshGoal()
    {
        RefreshNavigationIndicators();
        if (Application.isPlaying && currentElement == null && goalListElementPrefab != null)
            currentElement = Instantiate(goalListElementPrefab,
                goalListElementParent != null ? goalListElementParent : transform, false);
        if (currentElement != null)
        {
            currentElement.gameObject.SetActive(currentGoal != null);
            if (currentGoal != null) currentElement.SetGoal(currentGoal, CurrentProgress);
        }
        GoalChanged?.Invoke();
    }

    private void RefreshNavigationIndicators()
    {
        var activeIndicator = isActiveAndEnabled && initialized ? currentGoal?.navigationIndicator : null;
        foreach (var goal in allGoalSettings ?? Array.Empty<GoalSettings>())
        {
            var indicator = goal?.navigationIndicator;
            if (indicator != null && indicator.activeSelf != (indicator == activeIndicator))
                indicator.SetActive(indicator == activeIndicator);
        }
    }

    private void MarkDirty()
    {
        dirty = true;
    }

    private void FlushSave()
    {
        if (!dirty || loadedData == null || loadedData != SaveSystem.Data || !SaveSystem.savesLoaded) return;
        SaveSystem.SaveData();
        dirty = false;
    }

    private void OnDisable()
    {
        GoalChecker.ActionReported -= ProcessAction;
        if (Instance == this)
        {
            RefreshNavigationIndicators();
            Instance = null;
        }
    }
}

[Serializable]
public class GoalSettings
{
    [Tooltip("Unique, permanent ID used to match this goal with its saved progress.")]
    public string goalId;
    [TextArea]
    public string goalDescription;
    [Tooltip("Optional scene object with HUDNavigationElement. Active only while this goal is current.")]
    public GameObject navigationIndicator;
    public GoalStage stage;
    [Tooltip("Target hangar, numbered 1 through 5. Zero means any hangar. Ignored for GrainCollected, GrainLoaded, GrainSold and GrainMoved, which count global kg.")]
    [Range(0, 5)] public int hangarId;
    public GoalType goalType;
    [Tooltip("Disabled: count all grain types together, including mixed-grain hangars. Enabled: count only grainType.")]
    public bool filterByGrainType;
    [Tooltip("Optional goal filter. Ignored when filterByGrainType is disabled; does not define a hangar's contents.")]
    public GrainType grainType;
    [Tooltip("Upgrade required for an UpgradePurchased goal.")]
    public GoalUpgradeType upgradeType;
    public GoalProgressMode progressMode;
    [Tooltip("Target in kg, dollars, event count, or percentage points (0-100), depending on goalType.")]
    [Min(0.001f)] public float goalAmount = 1f;
    [Tooltip("Use displayGoalAmount for UI instead of goalAmount. Completion still uses goalAmount.")]
    public bool useDisplayGoalAmount;
    [Tooltip("Target shown in UI when useDisplayGoalAmount is enabled. Progress is scaled proportionally. Zero uses goalAmount.")]
    [Min(0f)] public float displayGoalAmount;
    public float DisplayGoalAmount => useDisplayGoalAmount && float.IsFinite(displayGoalAmount) && displayGoalAmount > 0f
        ? displayGoalAmount : goalAmount;
    public float GetDisplayProgress(float progress) => Mathf.Clamp01(progress / goalAmount) * DisplayGoalAmount;
    [Tooltip("Money awarded once for completing this goal.")]
    [Min(0)] public int rewardAmount;
}

public enum GoalType
{
    Custom = 0,
    MoneyEarned = 1,
    GrainCollected = 2,
    BagsFilled = 3,
    SackPickedUp = 4,
    SackPlaced = 5,
    ShovelEquipped = 6,
    SackFilled = 7,
    SackClosed = 8,
    SackLoaded = 9,
    GrainLoaded = 10,
    BuyerReached = 11,
    GrainSold = 12,
    UpgradePurchased = 13,
    // Value 14 is retired; keep subsequent IDs stable for serialized scenes and prefabs.
    GrainMoved = 15,
    HangarCleared = 16,
    HangarKeyCollected = 17,
    HangarUnlocked = 18,
    WarehouseReturned = 19
}

public enum GoalStage
{
    Tutorial = 0,
    Progress = 1,
    Hangar = 2,
    Upgrade = 3,
    Finale = 4
}

public enum GoalUpgradeType
{
    None = 0,
    LargerSack = 1,
    Wheelbarrow = 2,
    HandCart = 3,
    LargerTruck = 4,
    PalletJack = 5,
    Shovel = 6,
    Stamina = 7
}

public enum GoalProgressMode
{
    [Tooltip("Count only progress made after this goal becomes active.")]
    SinceActivation = 0,
    [Tooltip("Include earlier progress matching this goal's hangar and grain filters.")]
    LifetimeTotal = 1,
    [Tooltip("Check the current state, such as an owned upgrade or a hangar's cleared percentage.")]
    CurrentValue = 2
}

[Serializable]
public class GoalSettingsSave
{
    public string goalId;
    [Tooltip("Actual progress in the goal's units, not a normalized UI fill value.")]
    [FormerlySerializedAs("fillAmount")]
    [Min(0f)] public float currentAmount;
    public bool completed;
    public bool rewardClaimed;
}
