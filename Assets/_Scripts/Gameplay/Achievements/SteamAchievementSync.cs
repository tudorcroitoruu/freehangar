using System.Collections.Generic;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX
using Steamworks;
#endif

/// <summary>Retries saved local unlocks against the configured app. No Steam calls for an unset/test App ID.</summary>
public sealed class SteamAchievementSync : MonoBehaviour
{
    private SteamAchievementSettings settings;
    public string Status { get; private set; }="Steam not configured (local achievements active)";
    private float nextAttempt;
    public void RequestSync()=>nextAttempt=0;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX
    private Callback<UserStatsStored_t> stored;
    private readonly HashSet<int> pending=new HashSet<int>();
    private readonly HashSet<int> inFlight=new HashSet<int>();
    private readonly HashSet<string> warned=new HashSet<string>();
    private bool storing;
    private float storeStarted;
#endif
    private void Awake()=>settings=Resources.Load<SteamAchievementSettings>("SteamAchievementSettings");
    private void Update()
    {
        if(settings==null || !settings.HasRealAppId)return;
#if UNITY_EDITOR
        if(!settings.syncInEditor){Status="Editor Steam syncing disabled (local achievements active)";return;}
#endif
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX
        if(Time.unscaledTime<nextAttempt || !SaveSystem.savesLoaded || SaveSystem.Data==null)return;
        nextAttempt=Time.unscaledTime+10f;
        if(!SteamManager.Initialized){Status="Steam unavailable; local unlocks will sync on a later session";return;}
        uint runningApp=SteamUtils.GetAppID().m_AppId;
        if(runningApp!=settings.appId){Status=$"Steam app mismatch: expected {settings.appId}, running {runningApp}";WarnOnce(Status);return;}
        if(!SteamUser.BLoggedOn()){Status="Steam offline; unlocks queued locally";return;}
        if(stored==null)stored=Callback<UserStatsStored_t>.Create(OnStored);
        if(storing && Time.unscaledTime-storeStarted>30f){storing=false;inFlight.Clear();}
        var state=AchievementRules.State(SaveSystem.Data);
        foreach(var definition in AchievementRules.Definitions)
        {
            int index=definition.index;string api=settings.ApiName(index);
            if(string.IsNullOrWhiteSpace(api)){WarnOnce("Missing Steam API name for index "+index);continue;}
            // SDK 1.65 preloads current user stats before launch; RequestCurrentStats was removed from this SDK.
            if(!SteamUserStats.GetAchievement(api,out bool achieved)){WarnOnce("Steam achievement is unavailable or unpublished: "+api);continue;}
            if(achieved)
            {
                if(!state.unlocked.Contains(index))GameAchievements.ImportSteamUnlock(index);
                continue;
            }
            if(state.unlocked.Contains(index) && SteamUserStats.SetAchievement(api))pending.Add(index);
        }
        if(pending.Count>0 && !storing)
        {
            inFlight.Clear();foreach(int index in pending)inFlight.Add(index);
            storing=SteamUserStats.StoreStats();storeStarted=Time.unscaledTime;
            Status=storing?"Steam unlock upload pending":"Steam store failed; retry queued";
        }
        else if(!storing)Status="Steam connected; local achievements synchronized";
#else
        Status="Steam achievements unsupported on this platform (local tracking active)";
#endif
    }
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX
    private void OnStored(UserStatsStored_t result)
    {
        if(settings==null || result.m_nGameID!=settings.appId)return;
        storing=false;
        if(result.m_eResult==EResult.k_EResultOK)
        {
            foreach(int index in inFlight)pending.Remove(index);
            Status="Steam achievements saved";
        }
        else{Status="Steam store failed ("+result.m_eResult+"); retry queued";nextAttempt=Time.unscaledTime+15f;}
        inFlight.Clear();
    }
    private void WarnOnce(string message){if(warned.Add(message))Debug.LogWarning("[Steam achievements] "+message);}
    private void OnDestroy(){stored?.Dispose();}
#endif
}
