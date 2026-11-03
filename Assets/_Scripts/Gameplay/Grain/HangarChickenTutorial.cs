using UnityEngine;

/// <summary>One saved first encounter, unlocked by opening Hangar 2.</summary>
public sealed class HangarChickenTutorial : MonoBehaviour
{
    public FarmChicken chicken;
    private float nextCheck;
    public static bool ShouldAppear(PlayerData data) => data != null && data.IsHangarOpen(2) && !data.hangar2ChickenDefeated;
    private void OnEnable() { if(chicken)chicken.Died+=Complete; }
    private void OnDisable() { if(chicken)chicken.Died-=Complete; }
    private void Update()
    {
        if(Time.time<nextCheck || !SaveSystem.savesLoaded || !chicken || chicken.IsDead)return;
        nextCheck=Time.time+.25f;
        bool show=ShouldAppear(SaveSystem.Data);
        if(chicken.gameObject.activeSelf!=show)chicken.gameObject.SetActive(show);
    }
    private void Complete()
    {
        if(SaveSystem.Data==null)return;
        SaveSystem.Data.hangar2ChickenDefeated=true;
        SaveSystem.SaveData();
    }
}
