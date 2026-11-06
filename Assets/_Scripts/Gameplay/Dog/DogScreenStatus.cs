using System;
using UnityEngine;
using UnityEngine.UI;

public class DogScreenStatus : MonoBehaviour
{
    [SerializeField] private FarmDog dog;
    [SerializeField] private DogStatusPreset[] presets;
    [Space, Header("Main")] 
    [SerializeField] private Image outlineFillImage;
    [SerializeField] private Image dogStatusImage;
    [SerializeField] private Image dogStatusSecondImage;
    [SerializeField] private Text statusNameText;

    private FarmDog.DogState? displayedState;
    private float nextDogSearch;
    private Material originalMaterial, runtimeMaterial;
    private static readonly int OutlineFillAmountId = Shader.PropertyToID("_OutlineFillAmount");
    private static readonly int UseOutlineFillId = Shader.PropertyToID("_UseOutlineFill");

    private void OnEnable()
    {
        ResetView();
        nextDogSearch = 0f;
    }

    private void LateUpdate()
    {
        if (!dog && Time.unscaledTime >= nextDogSearch)
        {
            dog = FindFirstObjectByType<FarmDog>();
            nextDogSearch = Time.unscaledTime + 1f;
        }
        if (!dog || !dog.isActiveAndEnabled || !SaveSystem.savesLoaded || SaveSystem.Data == null)
        {
            if (displayedState.HasValue) ResetView();
            return;
        }

        if (displayedState != dog.State) ApplyPreset(dog.State);
        float interval = FarmDog.HuntInterval(dog.Level);
        SetOutlineFill(interval > 0f ? Mathf.Clamp01(dog.RestRemaining / interval) : 0f);
    }

    private void ResetView()
    {
        displayedState = null;
        SetOutlineFill(0f);
    }

    private void ApplyPreset(FarmDog.DogState state)
    {
        displayedState = state;
        if (presets == null) return;
        foreach (var preset in presets)
        {
            if (preset == null || preset.state != state) continue;
            if (dogStatusImage) dogStatusImage.sprite = preset.statusImage;
            if (dogStatusSecondImage) dogStatusSecondImage.sprite = preset.statusSecondImage;
            if (statusNameText) statusNameText.text = preset.statusName;
            return;
        }
    }

    private void SetOutlineFill(float fill)
    {
        if (!outlineFillImage) return;
        if (runtimeMaterial == null)
        {
            originalMaterial = outlineFillImage.material;
            if (originalMaterial == null || !originalMaterial.HasProperty(OutlineFillAmountId)) return;
            // Each status owns its countdown; never modify the shared material asset.
            runtimeMaterial = new Material(originalMaterial) { hideFlags = HideFlags.DontSave };
            outlineFillImage.material = runtimeMaterial;
        }
        ApplyFill(runtimeMaterial, fill);
        var renderedMaterial = outlineFillImage.materialForRendering;
        if (renderedMaterial != null && renderedMaterial != runtimeMaterial) ApplyFill(renderedMaterial, fill);
        outlineFillImage.SetMaterialDirty();
    }

    private static void ApplyFill(Material material, float fill)
    {
        if (material.HasProperty(UseOutlineFillId)) material.SetFloat(UseOutlineFillId, 1f);
        if (material.HasProperty(OutlineFillAmountId)) material.SetFloat(OutlineFillAmountId, fill);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null) return;
        if (outlineFillImage != null && outlineFillImage.material == runtimeMaterial) outlineFillImage.material = originalMaterial;
        if (Application.isPlaying) Destroy(runtimeMaterial);
        else DestroyImmediate(runtimeMaterial);
    }
}

[Serializable]
public class DogStatusPreset
{
    public FarmDog.DogState state;
    public string statusName;
    public Sprite statusImage;
    public Sprite statusSecondImage;

}
