using UnityEngine;

/// <summary>Source metadata for goal reports. A hangar may hold any number of different grain types.</summary>
[DisallowMultipleComponent]
public sealed class GoalContext : MonoBehaviour
{
    [SerializeField, Range(0, 5), Tooltip("Source hangar. Zero inherits from a parent context, or remains unknown.")]
    private int hangarId;
    [SerializeField, Tooltip("Enable only when this source has a known grain type.")]
    private bool hasGrainType;
    [SerializeField] private GrainType grainType;

    public static void Resolve(Component source, out int hangar, out GrainType? grain)
    {
        hangar = 0;
        grain = null;
        for (var node = source != null ? source.transform : null; node != null; node = node.parent)
        {
            if (!node.TryGetComponent<GoalContext>(out var context)) continue;
            if (hangar == 0 && context.hangarId > 0) hangar = context.hangarId;
            if (!grain.HasValue && context.hasGrainType) grain = context.grainType;
        }
    }

    public static void ReportProgress(Component source, GoalType type, float amount = 1f)
    {
        Resolve(source, out int hangar, out var grain);
        GoalChecker.ReportProgress(type, amount, hangar, grain);
    }

    public static void ReportValue(Component source, GoalType type, float value)
    {
        Resolve(source, out int hangar, out var grain);
        GoalChecker.ReportValue(type, value, hangar, grain);
    }
}

