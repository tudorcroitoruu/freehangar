using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Persistent achievement accounting from successful gameplay actions; independent of the active tutorial goal.</summary>
public sealed class GameAchievements : MonoBehaviour
{
    public static GameAchievements Instance { get; private set; }
    private PlayerData profile;
    private bool scanNeeded=true;
    private float nextScan,catalogReadyAt;
    private readonly List<GrainContainer> weighed=new List<GrainContainer>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatic()=>Instance=null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] private static void Bootstrap()
    {
        if(Instance==null)new GameObject("Achievements").AddComponent<GameAchievements>();
    }
    private void Awake()
    {
        if(Instance!=null && Instance!=this){Destroy(gameObject);return;}
        Instance=this;DontDestroyOnLoad(gameObject);
        if(GetComponent<SteamAchievementSync>()==null)gameObject.AddComponent<SteamAchievementSync>();
    }
    private void OnEnable()
    {
        if(Instance==null)Instance=this;
        if(Instance!=this)return;
        GoalChecker.ActionReported-=OnGoalAction;GoalChecker.ActionReported+=OnGoalAction;
        SceneManager.sceneLoaded-=OnSceneLoaded;SceneManager.sceneLoaded+=OnSceneLoaded;
    }
    private void OnDisable()
    {
        if(Instance!=this)return;
        GoalChecker.ActionReported-=OnGoalAction;SceneManager.sceneLoaded-=OnSceneLoaded;
    }
    private void OnDestroy()
    {
        if(Instance!=this)return;
        GoalChecker.ActionReported-=OnGoalAction;SceneManager.sceneLoaded-=OnSceneLoaded;Instance=null;
    }
    private void OnSceneLoaded(Scene scene,LoadSceneMode mode){catalogReadyAt=Time.unscaledTime+2f;nextScan=catalogReadyAt;scanNeeded=true;}
    private bool Ready()
    {
        if(!isActiveAndEnabled || !Application.isPlaying || !SaveSystem.savesLoaded || SaveSystem.Data==null)return false;
        if(profile!=SaveSystem.Data){profile=SaveSystem.Data;AchievementRules.State(profile);nextScan=0;scanNeeded=true;}
        return true;
    }
    private void Update()
    {
        if(!Ready())return;
        if(Time.unscaledTime>=nextScan)
        {
            nextScan=Time.unscaledTime+1f;
            if(scanNeeded && Time.unscaledTime>=catalogReadyAt && SceneManager.GetActiveScene().name=="Gameplay"){ScanDiscoveriesAndHangars();scanNeeded=false;}
            Evaluate();
        }
    }
    private void OnGoalAction(GoalAction action)
    {
        if(action.type!=GoalType.GrainCollected || action.isValue || !GoalChecker.IsValid(action) || !Ready())return;
        scanNeeded=true;
        var state=AchievementRules.State(profile);
        if(state.collectedKilograms>=5000)return;
        state.collectedKilograms=System.Math.Min(5000,state.collectedKilograms+action.amount);Evaluate();
    }
    private static int Hangar(GrainPile pile)
    {
        GoalContext.Resolve(pile,out int number,out _);if(number>0)return number;
        // Older authored grain piles carry their hangar number in their name.
        if(pile.name.StartsWith("Hangar ",System.StringComparison.OrdinalIgnoreCase))
        {
            string token=pile.name.Substring(7).Split(' ','-')[0];if(int.TryParse(token,out number))return number;
        }
        return 0;
    }
    private void ScanDiscoveriesAndHangars()
    {
        var state=AchievementRules.State(profile);
        foreach(var find in FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(string.IsNullOrWhiteSpace(find.FindId))continue;
            var list=find.FindKind==BuriedFind.Kind.VehicleKey?state.requiredVehicleKeys:find.FindKind==BuriedFind.Kind.CashChest?state.requiredChests:null;
            if(list!=null && !list.Contains(find.FindId)){list.Add(find.FindId);}
        }
        var piles=FindObjectsByType<GrainPile>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var pile in piles)
        {
            int number=Hangar(pile);if(number<=0)continue;
            if(!state.requiredHangars.Contains(number)){state.requiredHangars.Add(number);}
            if(state.clearedHangars.Contains(number))continue;
            // All piles belonging to this hangar must be cleared, including inactive grain.
            if(pile.ClearedPercent<99.95f)continue;
            bool all=true;
            foreach(var other in piles)
                if(Hangar(other)==number && other.ClearedPercent<99.95f){all=false;break;}
            if(all && !state.clearedHangars.Contains(number)){state.clearedHangars.Add(number);}
        }
    }
    private void Evaluate()
    {
        if(profile==null)return;
        bool unlocked=false;
        foreach(var definition in AchievementRules.Definitions)
            if((definition.index<3 || definition.index>5 || Time.unscaledTime>=catalogReadyAt) && AchievementRules.TryUnlock(profile,definition.index))
            {
                Debug.Log($"<color=#55FF77>[Achievement DONE] [{definition.index}] {definition.title} ({definition.SteamApiName})</color>");
                unlocked=true;
            }
        if(unlocked){GetComponent<SteamAchievementSync>()?.RequestSync();}
    }
    public static void BagClosed(GrainContainer bag)
    {
        var self=Instance;if(self==null || !self.Ready() || bag==null || !bag.IsFull || !bag.IsFinished)return;
        if(AchievementRules.AddUnique(AchievementRules.State(self.profile).closedBags,bag.AchievementIdentity,50)){self.Evaluate();}
    }
    public static void BroomPestScared(bool crow)
    {
        var self=Instance;if(self==null || !self.Ready())return;
        var state=AchievementRules.State(self.profile);
        if((crow && state.crowsScared>=25) || (!crow && state.ratsScared>=50))return;
        if(crow)state.crowsScared=Mathf.Min(25,state.crowsScared+1);else state.ratsScared=Mathf.Min(50,state.ratsScared+1);
        self.Evaluate();
    }
    public static void GrainSold(int paidCash)
    {
        var self=Instance;if(self==null || !self.Ready() || paidCash<=0)return;
        var state=AchievementRules.State(self.profile);if(state.grainSaleCash>=100000)return;state.grainSaleCash=System.Math.Min(100000,state.grainSaleCash+(long)paidCash);
        self.Evaluate();
    }
    public static void ForkliftDelivered(GrainContainer bag)
    {
        var self=Instance;if(self==null || !self.Ready() || bag==null || !bag.IsBigBag || !bag.IsFull)return;
        foreach(var zone in FindObjectsByType<SellingZone>(FindObjectsSortMode.None))
        {
            zone.CollectContainers(self.weighed);if(!self.weighed.Contains(bag))continue;
            if(AchievementRules.AddUnique(AchievementRules.State(self.profile).forkliftDeliveries,bag.AchievementIdentity,13)){self.Evaluate();}
            break;
        }
    }
    public static void ElectricalRoomPowered()
    {
        var self=Instance;if(self==null || !self.Ready())return;
        var state=AchievementRules.State(self.profile);if(state.electricalRoomPowered)return;
        state.electricalRoomPowered=true;self.Evaluate();
    }
    public static void ImportSteamUnlock(int index)
    {
        var self=Instance;if(self==null || !self.Ready() || index<0 || index>=AchievementRules.Definitions.Length)return;
        var state=AchievementRules.State(self.profile);if(state.unlocked.Contains(index))return;
        state.unlocked.Add(index);
        Debug.Log($"<color=#55FF77>[Achievement DONE] [{index}] {AchievementRules.Definitions[index].title} (restored from Steam)</color>");
    }
}
