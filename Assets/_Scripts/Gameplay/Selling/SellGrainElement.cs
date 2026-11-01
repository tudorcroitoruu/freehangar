using UnityEngine;
using UnityEngine.UI;

public class SellGrainElement : MonoBehaviour
{
    [SerializeField] private Image grainImage;
    [SerializeField] private Text grainName;

    [SerializeField] private Text grainQuantity;
    [SerializeField] private Text grainPrice;

    [SerializeField] private Text grainValue;
    public void SetData(GrainElementSettings settings, float kilograms, int multiplier = 1)
    {
        if (grainImage != null) grainImage.sprite = settings.grainSprite;
        if (grainName != null) grainName.text = settings.grainType.ToString();
        if (grainQuantity != null) grainQuantity.text = $"{EconomyNumberFormat.Accounting(kilograms)} kg";
        // Unit rates retain their configured precision; sale proceeds are whole dollars.
        if (grainPrice != null) grainPrice.text = "$ " + (settings.grainPrice * multiplier).ToString("N2", System.Globalization.CultureInfo.InvariantCulture) + " / kg";
        if (grainValue != null) grainValue.text = $"$ {EconomyNumberFormat.Accounting(PlayerPerks.SellCash(EconomyNumberFormat.CropValue(kilograms, settings.grainPrice),multiplier))}";
    }
}
