using UnityEngine;

/// <summary>Presentation data shared by all interactions, including tool progress.</summary>
public struct InteractionPromptData
{
    public Vector3 Position;
    public string Action;
    public bool ShowKey;
    public bool ShowPrimaryButton;
    public bool ShowProgress;
    public float Progress;
    public string ProgressText;

    public InteractionPromptData(Vector3 position, string action, bool showKey = true)
    {
        Position = position;
        Action = action;
        ShowKey = showKey;
        ShowPrimaryButton = false;
        ShowProgress = false;
        Progress = 0f;
        ProgressText = "";
    }
}
