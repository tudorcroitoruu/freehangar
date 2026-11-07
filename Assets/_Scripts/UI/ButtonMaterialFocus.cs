using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ButtonMaterialFocus : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    private Graphic graphic;
    private Material original, instance;
    private Color tint;
    private bool hovered, selected;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private void Awake()
    {
        if (GetComponentInParent<SelectedUI>() != null) return;
        graphic = GetComponent<Button>().targetGraphic;
        if (!graphic || !graphic.material || graphic.material.shader.name != "UI/Image Margin Fade") return;
        original = graphic.material;
        instance = new Material(original);
        tint = instance.GetColor(ColorId);
        graphic.material = instance;
    }
    public void OnSelect(BaseEventData data) { selected = true; Refresh(); }
    public void OnDeselect(BaseEventData data) { selected = false; Refresh(); }
    public void OnPointerEnter(PointerEventData data) { hovered = true; Refresh(); }
    public void OnPointerExit(PointerEventData data) { hovered = false; Refresh(); }
    private void Refresh()
    {
        if (!instance) return;
        bool highlightMaterial = GetComponentInParent<SelectedUI>() == null &&
            (selected || hovered) && GetComponent<Button>().IsInteractable();
        instance.SetColor(ColorId, highlightMaterial ? Color.white : tint);
        graphic.SetMaterialDirty();
    }
    private void OnDisable() { selected = hovered = false; Refresh(); }
    private void OnDestroy()
    {
        if (graphic && graphic.material == instance) graphic.material = original;
        if (instance) Destroy(instance);
    }
}
