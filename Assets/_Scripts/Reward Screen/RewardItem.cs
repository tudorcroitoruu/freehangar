using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardItem : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Text countGiveText;
    [SerializeField] private TextMeshProUGUI countGiveTextTMPpro;
    [SerializeField] private Text titleText;

    public void SetupItem(Sprite spriteIcon, int countGive, string title)
    {
        iconImage.sprite = spriteIcon;
        iconImage.enabled = spriteIcon != null;
        countGiveText.text = "" + Mathf.Max(0, countGive);
        if (countGiveTextTMPpro)
        {
            countGiveTextTMPpro.text = "" + Mathf.Max(0, countGive);
        }

        if (titleText)
        {
            titleText.text = title;
        }
    }
}
