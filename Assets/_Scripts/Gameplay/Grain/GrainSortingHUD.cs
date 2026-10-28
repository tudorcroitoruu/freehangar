using System.Text;
using TMPro;
using UnityEngine;

public static class GrainNames
{
    public static string Display(GrainType type) => type == GrainType.SunflowerSeeds ? "Sunflower seeds" : type.ToString();
}

/// <summary>Shows each portion of the current load and the next bagging milestone.</summary>
public sealed class GrainSortingHUD : MonoBehaviour
{
    [SerializeField] private ShovelTool tool;
    [SerializeField] private GrainProgression progression;
    [SerializeField] private TMP_Text description;
    private readonly StringBuilder text = new StringBuilder(256);
    private float refreshAt;
    private void Update()
    {
        if (description == null || Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + .15f;
        text.Clear();
        if (progression != null)
        {
            text.Append(progression.StageTitle).Append("  •  ").Append(progression.BaggedKilograms.ToString("0.0")).Append(" kg bagged");
            if (progression.NextThreshold >= 0) text.Append(" / ").Append(progression.NextThreshold.ToString("0")).Append(" kg next crop");
        }
        if (tool != null && tool.Kilograms > .00001f)
        {
            text.Append("\nShovel: ");
            bool first = true;
            for (int i = 0; i < GrainCargo.TypeCount; i++)
            {
                float kg = tool.Cargo[(GrainType)i];
                if (kg <= .00001f) continue;
                if (!first) text.Append("  ·  "); first = false;
                text.Append(GrainNames.Display((GrainType)i)).Append(' ').Append(kg.ToString("0.00")).Append(" kg");
            }
            text.Append("\nAim at a bag and click to pour its crop. Other crops stay on the shovel. Right-click ground to drop the load.");
        }
        description.SetText(text);
    }
}
