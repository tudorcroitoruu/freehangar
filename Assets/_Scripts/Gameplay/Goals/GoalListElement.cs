using TMPro;
using UnityEngine;

public class GoalListElement : MonoBehaviour
{
    [SerializeField] private GameObject checkerObj;
    [SerializeField] private TMP_Text goalText;

    public void SetGoal(GoalSettings goal, float progress)
    {
        if (checkerObj != null) checkerObj.SetActive(progress >= goal.goalAmount);
        if (goalText == null) return;
        string suffix;
        bool showProgress = true;
        switch (goal.goalType)
        {
            case GoalType.MoneyEarned: suffix = " $"; break;
            case GoalType.HangarCleared: suffix = "%"; break;
            case GoalType.GrainCollected:
            case GoalType.SackFilled:
            case GoalType.GrainLoaded:
            case GoalType.GrainDelivered:
            case GoalType.GrainMoved: suffix = " kg"; break;
            default:
                suffix = "";
                showProgress = goal.goalAmount > 1f;
                break;
        }
        goalText.richText = true;
        goalText.fontStyle &= ~FontStyles.Bold;
        goalText.text = $"<b>{goal.goalDescription}</b>";
        if (showProgress)
            goalText.text += $"\n{progress:0.##} / {goal.goalAmount:0.##}{suffix}";
    }
}
