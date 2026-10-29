using UnityEngine;

/// <summary>Supplies the authored HDRP sun to the imported Earth shaders.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class EarthSunLighting : MonoBehaviour
{
    public Light sun;
    public Renderer[] surfaces;
    private MaterialPropertyBlock properties;
    private static readonly int SunDirection = Shader.PropertyToID("_EarthSunDirection");
    private void OnEnable() => Apply();
    private void LateUpdate() => Apply();
    private void OnValidate() => Apply();
    public void Apply()
    {
        if (sun == null || surfaces == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        Vector3 direction = -sun.transform.forward;
        foreach (var surface in surfaces)
        {
            if (surface == null) continue;
            surface.GetPropertyBlock(properties);
            properties.SetVector(SunDirection, new Vector4(direction.x, direction.y, direction.z, 1));
            surface.SetPropertyBlock(properties);
        }
    }
}
