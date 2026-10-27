using UnityEngine;

/// <summary>Physical handlebar gauge; meters per second are displayed as km/h.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class MotorollerSpeedometer : MonoBehaviour
{
    [SerializeField] private MotorollerController controller;
    [SerializeField] private Transform needle;
    [SerializeField, Min(1)] private float maximumKph = 60;
    [SerializeField] private float zeroAngle = 135;
    [SerializeField] private float maximumAngle = -135;
    [SerializeField, Range(0,60), Tooltip("Preview only; Play Mode uses the controller speed.")]
    private float previewKph;
    public float DisplayedKph { get; private set; }
    public Transform Needle => needle;
    public void Configure(MotorollerController motor, Transform pointer)
    {
        controller = motor;
        needle = pointer;
        Refresh();
    }
    private void OnEnable() => Refresh();
    private void LateUpdate() => Refresh();
    public void Refresh()
    {
        DisplayedKph = Mathf.Clamp(Application.isPlaying ? controller != null ? Mathf.Abs(controller.Speed) * 3.6f : 0 : previewKph, 0, Mathf.Max(1,maximumKph));
        if (needle != null)
            needle.localRotation = Quaternion.Euler(0,0,Mathf.Lerp(zeroAngle,maximumAngle,DisplayedKph / Mathf.Max(1,maximumKph)));
    }
}
