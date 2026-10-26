using UnityEngine;

/// <summary>Defers the grain pile's renderers, physics and simulation until its hangar opens.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class HangarGrainVisibility : MonoBehaviour
{
    [SerializeField, Tooltip("Set by hangar progression when this hangar is opened or closed.")]
    private bool hangarOpen;
    [SerializeField, Tooltip("Entire grain-pile GameObject, including kernels, collision and simulation.")]
    private GameObject grainRoot;

    private bool applyRequested = true;
    public bool HangarOpen => hangarOpen;
    public GameObject GrainRoot => grainRoot;

    private void OnEnable() => ApplyState();
    private void OnValidate() => applyRequested = true;
    private void Update()
    {
        if (applyRequested) ApplyState();
    }

    public void SetHangarOpen(bool open)
    {
        hangarOpen = open;
        ApplyState();
    }

    [ContextMenu("Apply hangar grain visibility")]
    public void ApplyState()
    {
        applyRequested = false;
        if (!grainRoot || transform.IsChildOf(grainRoot.transform)) return;
        if (grainRoot.activeSelf != hangarOpen) grainRoot.SetActive(hangarOpen);
    }
}
