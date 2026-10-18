using System.Collections;
using System.Collections.Generic;
using UIPopups;
using UnityEngine;
using UnityEngine.UI;

public class RewardedScreen : MonoBehaviour
{
    [Header("Ref")] 
    [SerializeField] private BasePopupUI  basePopupUI;
    [SerializeField] private RewardItem rewardItemPrefab;
    [SerializeField] private Transform rewardItemsParent;
    [SerializeField] private List<Image> addonsImages;
    [Space] 
    [SerializeField] private Sprite perk1Icon;
    [SerializeField] private Sprite perk2Icon;
    [SerializeField] private Sprite perk3Icon;
    [SerializeField] private Sprite perk4Icon;
    
    private bool claimingReward;
    private readonly List<RewardItem> spawnedRewardItems = new List<RewardItem>();
    
    public static RewardedScreen Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
            return;

        Instance = this;
        ResolveRewardItemsParent();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowRewards()
    {
        claimingReward = true;
        basePopupUI.Show(true);
    }

    public void ShowReward(Sprite icon, int count, string title = "")
    {
        ClearRewardItems();
        ResolveRewardItemsParent();

        if (rewardItemPrefab != null && rewardItemsParent != null)
        {
            RewardItem rewardItem = Instantiate(rewardItemPrefab, rewardItemsParent, false);
            rewardItem.SetupItem(icon, Mathf.Max(1, count), title);
            spawnedRewardItems.Add(rewardItem);
        }

        ShowRewards();
    }

    public void ShowPerkReward(int perkIndex, int count)
    {
        Sprite perkIcon = GetPerkIcon(perkIndex);
        ShowReward(perkIcon, count, GetPerkTitle(perkIndex));
        for (int i = 0; i < addonsImages.Count; i++)
        {
            addonsImages[i].sprite = perkIcon;
        }
    }

    private string GetPerkTitle(int perkIndex)
    {
        return perkIndex switch
        {
            0 => "MAGNET",
            1 => "AUTO REPAIR",
            2 => "FREEZE",
            _ => ""
        };
    }

    private Sprite GetPerkIcon(int perkIndex)
    {
        return perkIndex switch
        {
            0 => perk1Icon,
            1 => perk2Icon,
            2 => perk3Icon,
            3 => perk4Icon,
            _ => null
        };
    }
    
    public void ClaimAllRewards()
    {
        if (!claimingReward)
            return;

        claimingReward = false;
        basePopupUI.Show(false);
        StartCoroutine(ClearAfterPopupCloses());
    }

    private IEnumerator ClearAfterPopupCloses()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        ClearRewardItems();
    }

    private void ResolveRewardItemsParent()
    {
        if (rewardItemsParent != null || basePopupUI == null)
            return;

        GridLayoutGroup grid = basePopupUI.GetComponentInChildren<GridLayoutGroup>(true);
        rewardItemsParent = grid != null ? grid.transform : basePopupUI.transform;
    }

    private void ClearRewardItems()
    {
        for (int i = spawnedRewardItems.Count - 1; i >= 0; i--)
        {
            if (spawnedRewardItems[i] != null)
                Destroy(spawnedRewardItems[i].gameObject);
        }
        spawnedRewardItems.Clear();
    }
}
