using UnityEngine;

[CreateAssetMenu(menuName="Freehangar/Steam Achievements",fileName="SteamAchievementSettings")]
public sealed class SteamAchievementSettings : ScriptableObject
{
    [Tooltip("The game's real Steam App ID, shared by achievements and standalone license checks. IDs 0 and 480 block standalone builds; Editor playtesting remains available.")]
    public uint appId;
    [Tooltip("Explicit opt-in for live Steam unlocks during Editor gameplay tests.")]
    public bool syncInEditor;
    [Tooltip("Stable index-to-Steam API name mapping. Publish these exact names in Steamworks.")]
    public string[] apiNames={"ACH_0","ACH_1","ACH_2","ACH_3","ACH_4","ACH_5","ACH_6","ACH_7","ACH_8","ACH_9","ACH_10"};
    public bool HasRealAppId => appId>0 && appId!=480;
    public string ApiName(int index) => apiNames!=null && index>=0 && index<apiNames.Length?apiNames[index]:null;
}
