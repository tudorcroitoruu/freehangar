using TMPro;
using UnityEngine;

public class GoalListElement : MonoBehaviour
{
    [SerializeField] private GameObject checkerObj;
    [SerializeField] private TMP_Text goalText;
    [SerializeField] private TMP_Text statusText;
    public void SetGoal(GoalSettings goal, float progress)
    {
        if (goalText != null) goalText.text = goal.goalDescription;
        if (checkerObj != null) checkerObj.SetActive(progress >= goal.goalAmount);
        if (statusText == null) return;
        string suffix;
        switch (goal.goalType)
        {
            case GoalType.MoneyEarned: suffix = " $"; break;
            case GoalType.HangarCleared: suffix = "%"; break;
            case GoalType.GrainCollected:
            case GoalType.SackFilled:
            case GoalType.GrainLoaded:
            case GoalType.GrainDelivered:
            case GoalType.GrainMoved: suffix = " kg"; break;
            default: suffix = ""; break;
        }
        statusText.text = $"{progress:0.##} / {goal.goalAmount:0.##}{suffix}";
    }
}
