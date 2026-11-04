using UnityEngine;

// Workshop displays and held tools share the same authored finish textures.
public sealed class CombatToolFinish : MonoBehaviour
{
    [SerializeField] private PlayerCombatTools.Tool tool;
    [SerializeField] private Texture2D[] finishes;
    private Renderer[] parts;
    private MaterialPropertyBlock block;
    private int shown = -1;
    private static readonly int BaseMap = Shader.PropertyToID("_TextureSample");

    private void OnEnable()
    {
        parts ??= GetComponentsInChildren<MeshRenderer>(true);
        block ??= new MaterialPropertyBlock();
        shown = -1;
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        int level = CombatToolUpgrades.Level(SaveSystem.Data, tool);
        if (level == shown) return;
        shown = level;
        foreach (var part in parts)
        {
            var material = part.sharedMaterial;
            if (!material || !material.HasProperty(BaseMap)) continue;
            Texture texture = level > 0 && finishes != null && level <= finishes.Length ? finishes[level - 1] : null;
            texture = texture ? texture : material.GetTexture(BaseMap);
            if (!texture) continue;
            part.GetPropertyBlock(block);
            block.SetTexture(BaseMap, texture);
            part.SetPropertyBlock(block);
        }
    }
}
