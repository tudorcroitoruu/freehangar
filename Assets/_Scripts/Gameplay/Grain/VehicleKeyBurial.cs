using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shared scene-authoring and runtime fallback for vehicle keys in excavation order.</summary>
public sealed class VehicleKeyBurial : MonoBehaviour
{
    public readonly struct Placement
    {
        public readonly string vehicleId;
        public readonly int hangar;
        public readonly float progress;
        public Placement(string id,int number,float depth) { vehicleId=id;hangar=number;progress=depth; }
    }
    public static readonly Placement[] Plan =
    {
        new Placement("trike",2,.25f), new Placement("lawnmower",2,.75f),
        new Placement("pickup",3,.25f), new Placement("pickupBig",3,.75f),
        new Placement("pickupLast",4,.65f)
    };
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded-=OnSceneLoaded;SceneManager.sceneLoaded+=OnSceneLoaded;
    }
    private static void OnSceneLoaded(Scene scene,LoadSceneMode mode)
    {
        if(scene.name!="Gameplay") return;
        var host=new GameObject("Vehicle key fallback (missing authored keys only)");
        SceneManager.MoveGameObjectToScene(host,scene);host.AddComponent<VehicleKeyBurial>();
    }
    private System.Collections.IEnumerator Start()
    {
        yield return null;
        var settings=Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
        if(settings==null) yield break;
        foreach(var item in Plan)
        {
            var profile=settings.Find(item.vehicleId);
            if(profile==null || !profile.requiresKey || FindKey(gameObject.scene,profile.RequiredKeyId)!=null) continue;
            var pile=FindPile(gameObject.scene,item.hangar);
            if(!TryPlacement(pile,FindEntrance(gameObject.scene,item.hangar,pile),item.progress,out var point))
            { Debug.LogWarning($"No suitable buried position for {profile.displayName} in Hangar {item.hangar}.",this);continue; }
            var prefab=Resources.Load<GameObject>("VehicleKeys/"+item.vehicleId);
            if(prefab==null)continue;
            var key=Instantiate(prefab,point,Quaternion.Euler(0,27+item.hangar*41,0),transform);
            key.name=$"Vehicle key - {profile.displayName} - HANGAR {item.hangar} ({item.progress:P0} from door)";
            key.GetComponent<BuriedFind>().ConfigureVehicleKey(profile.RequiredKeyId,profile.displayName,pile);
        }
        if(transform.childCount==0)Destroy(gameObject);
    }
    public static BuriedFind FindKey(Scene scene,string id)
    {
        foreach(var find in FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(find.gameObject.scene==scene && find.FindId==id)return find;
        return null;
    }
    public static GrainPile FindPile(Scene scene,int number)
    {
        foreach(var pile in FindObjectsByType<GrainPile>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(pile.gameObject.scene!=scene)continue;
            GoalContext.Resolve(pile,out int hangar,out _);
            if(hangar==number || pile.name.StartsWith("Hangar "+number+" -",System.StringComparison.OrdinalIgnoreCase))return pile;
        }
        return null;
    }
    public static Vector3 FindEntrance(Scene scene,int number,GrainPile pile)
    {
        foreach(var door in FindObjectsByType<HangarKeyDoor>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if(door.gameObject.scene==scene && door.HangarNumber==number)return door.EntrancePosition;
        return pile!=null?pile.transform.position-pile.transform.forward*pile.Size:Vector3.zero;
    }
    public static bool TryPlacement(GrainPile pile,Vector3 entrance,float progress,out Vector3 position)
    {
        position=default;if(pile==null)return false;
        pile.Initialize();if(pile.HeightField==null)return false;
        Vector3 forward=pile.transform.position-entrance;forward.y=0;
        if(forward.sqrMagnitude<.001f)forward=pile.transform.forward;
        forward.Normalize();Vector3 right=Vector3.Cross(Vector3.up,forward);
        var points=new List<Vector3>();var heights=new List<float>();
        float min=float.PositiveInfinity,max=float.NegativeInfinity;
        for(int z=0;z<=40;z++)for(int x=0;x<=40;x++)
        {
            Vector3 point=pile.transform.TransformPoint(new Vector3((x/40f-.5f)*pile.Size,0,(z/40f-.5f)*pile.Size));
            if(!pile.IsInsideBoundary(point,.3f))continue;
            float height=pile.SurfaceHeight(point)-point.y;if(height<.35f)continue;
            float distance=Vector3.Dot(point-entrance,forward);
            min=Mathf.Min(min,distance);max=Mathf.Max(max,distance);points.Add(point);heights.Add(height);
        }
        if(points.Count==0)return false;
        float target=Mathf.Lerp(min,max,Mathf.Clamp01(progress));float best=float.PositiveInfinity;
        for(int i=0;i<points.Count;i++)
        {
            float depth=Vector3.Dot(points[i]-entrance,forward);
            float lateral=Mathf.Abs(Vector3.Dot(points[i]-pile.transform.position,right));
            float score=Mathf.Abs(depth-target)*10f+lateral*.1f;
            if(score>=best)continue;
            best=score;position=points[i];position.y+=.08f+heights[i]*.12f;
        }
        return true;
    }
}
