using UnityEngine;
using UnityEngine.UI;

public class SellGrainElement : MonoBehaviour
{
    [SerializeField] private Image grainImage;
    [SerializeField] private Text grainName;

    [SerializeField] private Text grainQuantity;
    [SerializeField] private Text grainPrice;

    [SerializeField] private Text grainValue;
    public void SetData(GrainElementSettings settings, float kilograms)
    {
        if (grainImage != null) grainImage.sprite = settings.grainSprite;
        if (grainName != null) grainName.text = settings.grainType.ToString();
        if (grainQuantity != null) grainQuantity.text = $"{kilograms:0.###} kg";
        if (grainPrice != null) grainPrice.text = $"$ {settings.grainPrice:0.##} / kg";
        if (grainValue != null) grainValue.text = $"$ {kilograms * (double)settings.grainPrice:0.##}";
    }
}
