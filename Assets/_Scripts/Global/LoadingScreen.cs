using UIPopups;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
    public BasePopupUI popupUI;
    [Space]
    public bool animating;
    public static bool isLoadingOn;
    private CanvasGroup canvasGroup;
    public static LoadingScreen Instance;
    [SerializeField] private Image loadingImage;
    private Material originalLoadingMaterial, loadingMaterial;
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int UseFillId = Shader.PropertyToID("_UseFill");

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (!Instance)
        {
            Instance = this;
            LoadingOff();
        }
    }

    public void LoadingOn()
    {
        SetProgress(0f);
        animating = true;
        isLoadingOn = true;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        if (popupUI)
        {
            popupUI.ShowInstant(true, closePreviousPopup: false);
        }
    }

    public void SetProgress(float progress)
    {
        if (loadingImage == null) return;
        if (loadingMaterial == null)
        {
            originalLoadingMaterial = loadingImage.material;
            if (originalLoadingMaterial == null || !originalLoadingMaterial.HasProperty(FillAmountId)) return;
            // Each loading bar owns its progress; never modify the shared material asset.
            loadingMaterial = new Material(originalLoadingMaterial) { hideFlags = HideFlags.DontSave };
            loadingImage.material = loadingMaterial;
        }

        float amount = Mathf.Clamp01(progress);
        loadingMaterial.SetFloat(FillAmountId, amount);
        if (loadingMaterial.HasProperty(UseFillId)) loadingMaterial.SetFloat(UseFillId, 1f);
        // UI masks may render a cached stencil copy instead of the assigned material.
        var renderedMaterial = loadingImage.materialForRendering;
        if (renderedMaterial != null && renderedMaterial != loadingMaterial && renderedMaterial.HasProperty(FillAmountId))
        {
            renderedMaterial.SetFloat(FillAmountId, amount);
            if (renderedMaterial.HasProperty(UseFillId)) renderedMaterial.SetFloat(UseFillId, 1f);
        }
        loadingImage.SetMaterialDirty();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (loadingMaterial == null) return;
        if (loadingImage != null && loadingImage.material == loadingMaterial)
            loadingImage.material = originalLoadingMaterial;
        if (Application.isPlaying) Destroy(loadingMaterial);
        else DestroyImmediate(loadingMaterial);
    }

    public void LoadingOff()
    {
        animating = false;
        isLoadingOn = false;
        if (popupUI)
        {
            popupUI.ShowInstant(false);
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
