using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SelectedUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private bool selectOnHover;
    [Tooltip("Use hoverImage when hovering an unselected item. Selected items keep selectedImage.")]
    [SerializeField] private bool useDifferentHover;
    [SerializeField] private GameObject hoverImage;
    [Tooltip("Keep the hover object visible while hovering a selected item, alongside selectedImage.")]
    [SerializeField] private bool showHoverWhenSelected;

    [Header("Canvas Group")]
    [SerializeField] private CanvasGroup canvasGroup;
    [Range(0f, 1f)]
    [SerializeField] private float selectedAlpha = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float unselectedAlpha = 0.5f;
    private bool isSelected;

    [Header("Hover Events")]
    [SerializeField] private UnityEvent onHoverOn;
    [SerializeField] private UnityEvent onHoverOff;
    public UnityEvent HoverOn => onHoverOn ??= new UnityEvent();
    public UnityEvent HoverOff => onHoverOff ??= new UnityEvent();
    private bool isHovered;

    public event System.Action<SelectedUI> PointerEntered;

    private void OnEnable()
    {
        UpdateSelectionVisuals();
    }

    private void UpdateCanvasGroup()
    {
        if (canvasGroup)
            canvasGroup.alpha = isSelected || (selectOnHover && isHovered) ? selectedAlpha : unselectedAlpha;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || isHovered)
            return;

        isHovered = true;
        onHoverOn?.Invoke();

        if (selectOnHover && isHovered && isActiveAndEnabled)
            PointerEntered?.Invoke(this);

        if (selectOnHover && isHovered && isActiveAndEnabled)
            UpdateSelectionVisuals();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        EndHover();
    }

    private void OnDisable()
    {
        EndHover();
    }

    private void EndHover()
    {
        if (!isHovered)
            return;

        isHovered = false;
        if (selectOnHover)
            UpdateSelectionVisuals();
        onHoverOff?.Invoke();
    }

    [SerializeField] private Text buttonText;
    [SerializeField] private GameObject selectedImage;
    [SerializeField] private GameObject unselectedImage;
    [Header("Scale")]
    [SerializeField] private bool shouldScale;
    [Tooltip("Object scaled for selection and hover. Defaults to this object's transform.")]
    [SerializeField] private Transform scaleTarget;
    [Min(0.01f)]
    [SerializeField] private float scaleSize = 1.1f;
    private Transform scaledTarget;
    private Vector3 originalScale;

    [Space, Header("Move selected")] 
    [SerializeField] private bool moveSelected;
    [SerializeField] private RectTransform selectedImageRect;
    [SerializeField] private Vector2 moveSelectedPos;

    [Space, Header("Increase size")] 
    [SerializeField] private RectTransform resizeObject;
    [SerializeField] private Vector2 originalSize;
    [SerializeField] private Vector2 newSize;
    
    public void Select()
    {
        isSelected = true;
        UpdateSelectionVisuals();
    }

    public void SetHoverWhenSelected(bool enabled)
    {
        showHoverWhenSelected = enabled;
        UpdateSelectionVisuals();
    }

    private void UpdateSelectionVisuals()
    {
        bool highlighted = isSelected || (selectOnHover && isHovered);
        bool showHoverImage = (!isSelected || showHoverWhenSelected) && selectOnHover && isHovered && useDifferentHover &&
            hoverImage && hoverImage != selectedImage;
        UpdateCanvasGroup();
        Transform target = scaleTarget ? scaleTarget : transform;
        if (scaledTarget != target)
        {
            if (scaledTarget)
                scaledTarget.localScale = originalScale;
            scaledTarget = target;
            originalScale = target.localScale;
        }
        scaledTarget.localScale = shouldScale && highlighted
            ? originalScale * Mathf.Max(0.01f, scaleSize)
            : originalScale;

        if (moveSelected && selectedImageRect)
        {
            selectedImageRect.anchoredPosition = highlighted ? moveSelectedPos : Vector2.zero;
        }

        if (selectedImage)
        {
            selectedImage.SetActive(highlighted && (!showHoverImage || (isSelected && showHoverWhenSelected)));
        }

        if (hoverImage && hoverImage != selectedImage)
            hoverImage.SetActive(showHoverImage);

        if (unselectedImage)
        {
            unselectedImage.SetActive(!highlighted);
        }

        if (resizeObject)
        {
            resizeObject.sizeDelta = highlighted ? newSize : originalSize;
        }
    }

    public void DeselectMovableObject()
    {
        selectedImageRect.anchoredPosition = Vector2.zero;
    }

    public void Deselect()
    {
        isSelected = false;
        UpdateSelectionVisuals();
    }

    public void SetButton(string text)
    {
        buttonText.text = text;
    }
}
