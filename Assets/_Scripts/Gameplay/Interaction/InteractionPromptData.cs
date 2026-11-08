using UnityEngine;

/// <summary>Presentation data shared by all interactions, including tool progress.</summary>
public struct InteractionPromptData
{
    public Vector3 Position;
    public string Action;
    public PlayerCombatTools.Tool RequiredTool;
    public string SecondaryDescription;
    public bool ShowKey;
    public bool ShowKeyboardKey;
    public KeyCode InteractionKey;
    public InteractionPromptAction[] AdditionalActions;
    public bool RequiresFreeHands;
    public bool VehicleFull;
    public bool NotFitInWheelBarrow;
    public bool CannotLoadInWheelbarrow;
    public bool CannotLoadHalfEmptyBags;
    public bool ContainerLevelTooHigh;
    public bool NeedKeyForChest;
    public bool NotHungry;
    public bool WeighingEmpty;
    public bool WeighingWrong;
    public bool UseForkliftToMove;
    public bool DogResting;
    public bool ShowPrimaryButton;
    public bool PrimaryIsAttack;
    public bool ShowProgress;
    public float Progress;
    public string ProgressText;
    public bool HasAvailableInput => !RequiresFreeHands && (ShowKey || ShowPrimaryButton || (AdditionalActions != null && AdditionalActions.Length > 0));

    public InteractionPromptData(Vector3 position, string action, bool showKey = true)
    {
        Position = position;
        Action = action;
        RequiredTool = default;
        SecondaryDescription = "";
        ShowKey = showKey;
        ShowKeyboardKey = false;
        InteractionKey = KeyCode.E;
        AdditionalActions = null;
        RequiresFreeHands = false;
        VehicleFull = false;
        NotFitInWheelBarrow = false;
        CannotLoadInWheelbarrow = false;
        CannotLoadHalfEmptyBags = false;
        ContainerLevelTooHigh = false;
        NeedKeyForChest = false;
        NotHungry = false;
        WeighingEmpty = false;
        WeighingWrong = false;
        UseForkliftToMove = false;
        DogResting = false;
        ShowPrimaryButton = false;
        PrimaryIsAttack = false;
        ShowProgress = false;
        Progress = 0f;
        ProgressText = "";
    }
}

public struct InteractionPromptAction
{
    public string Action;
    public string SecondaryDescription;
    public KeyCode Key;
    public bool ShowProgress;
    public float Progress;
    public string ProgressText;
    public InteractionPromptAction(string action, KeyCode key)
    {
        Action = action;
        Key = key;
        SecondaryDescription = "";
        ShowProgress = false;
        Progress = 0f;
        ProgressText = "";
    }
}
