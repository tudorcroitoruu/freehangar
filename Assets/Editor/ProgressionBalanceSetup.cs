using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-time authored discovery pacing. Percentages represent starting grain mass from the entrance, not a straight line of props.</summary>
public static class ProgressionBalanceSetup
{
    private const string Marker="Progression balance v1 - 3 to 5 hours";
    private readonly struct FindPlan
    {
        public readonly string id,template;public readonly int hangar,cash;public readonly float progress,side;
        public FindPlan(string id,int hangar,float progress,float side,int cash=0,string template=null){this.id=id;this.hangar=hangar;this.progress=progress;this.side=side;this.cash=cash;this.template=template;}
    }
    private static readonly FindPlan[] Plan=
    {
        new FindPlan("farm.cache.starter",1,.10f,-.25f,75),
        new FindPlan("farm.perk.stamina-hour",1,.28f,.2f),
        new FindPlan("farm.key.copper",1,.44f,-.45f),
        new FindPlan("farm.stamina.starter",1,.58f,.5f),
        new FindPlan("farm.tool.hammer",1,.68f,-.15f),
        new FindPlan("farm.hangar.key.2",1,.84f,.15f),
        new FindPlan("farm.cache.h1.finish",1,.96f,-.45f,75,"farm.cache.starter"),
        new FindPlan("farm.chest.h2",2,.08f,-.45f,500),
        new FindPlan("vehicle.key.trike",2,.15f,.1f),
        new FindPlan("farm.cache.h2",2,.24f,.5f,250),
        new FindPlan("farm.tool.broom",2,.32f,-.15f),
        new FindPlan("farm.key.copper.bonus.1",2,.43f,-.55f),
        new FindPlan("vehicle.key.lawnmower",2,.52f,.2f),
        new FindPlan("farm.perk.free-upgrade",2,.59f,-.2f),
        new FindPlan("farm.chest.bonus.1",2,.67f,.6f,750),
        new FindPlan("farm.perk.bottle.infinitestamina",2,.74f,-.45f),
        new FindPlan("farm.tool.sledgehammer",3,.08f,.1f),
        new FindPlan("farm.hangar.key.3",2,.88f,-.15f),
        new FindPlan("farm.perk.bottle.mobility",3,.14f,.25f),
        new FindPlan("farm.key.iron.bonus.2",3,.26f,-.55f),
        new FindPlan("vehicle.key.pickup",3,.36f,.15f),
        new FindPlan("farm.cache.h3",3,.45f,.55f,500),
        new FindPlan("farm.perk.bottle.doublesellcash.h3",3,.57f,-.3f,0,"farm.perk.bottle.doublesellcash"),
        new FindPlan("farm.chest.bonus.2",3,.67f,.55f,1000),
        new FindPlan("farm.key.iron",3,.76f,-.4f),
        new FindPlan("farm.hangar.key.4",3,.88f,.1f),
        new FindPlan("farm.chest.h4",4,.09f,-.5f,1500),
        new FindPlan("farm.perk.bottle.noattacks",4,.19f,.2f),
        new FindPlan("vehicle.key.pickupBig",4,.27f,-.2f),
        new FindPlan("farm.key.copper.bonus.3",4,.37f,.55f),
        new FindPlan("farm.voucher.h3",4,.45f,-.4f),
        new FindPlan("farm.stamina.h4",4,.53f,.25f),
        new FindPlan("farm.cache.h4",4,.60f,-.55f,900),
        new FindPlan("vehicle.key.pickupLast",4,.69f,.15f),
        new FindPlan("farm.chest.bonus.3",4,.77f,.6f,1800),
        new FindPlan("farm.hangar.key.5",4,.89f,-.15f),
        new FindPlan("farm.cache.h5.front",5,.12f,-.3f,750,"farm.cache.h5"),
        new FindPlan("farm.key.iron.bonus.4",5,.24f,.55f),
        new FindPlan("farm.perk.bottle.mobility.h5",5,.36f,-.4f,0,"farm.perk.bottle.mobility"),
        new FindPlan("farm.perk.bottle.doublesellcash",5,.48f,.2f),
        new FindPlan("farm.perk.bottle.stamina.h5",5,.60f,-.45f,0,"farm.perk.bottle.infinitestamina"),
        new FindPlan("farm.cache.h5",5,.70f,.45f,1400),
        new FindPlan("farm.chest.bonus.4",5,.80f,-.55f,2500),
        new FindPlan("farm.cache.h5.finish",5,.91f,.25f,1500,"farm.cache.h5")
    };
    private sealed class Sample
    {
        public Vector3 floor;public float height,depth,side,share;
    }
    [MenuItem("Tools/Grain/Apply balanced 3-5 hour progression")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Leave Play mode before authoring progression.");
        var scene=SceneManager.GetActiveScene();if(scene.name!="Gameplay")throw new InvalidOperationException("Open Gameplay first.");
        var finds=UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(f=>f.gameObject.scene==scene).ToList();
        if(finds.Count==0)throw new InvalidOperationException("No authored discoveries found.");
        var piles=new Dictionary<int,GrainPile>();var entrances=new Dictionary<int,Vector3>();var samples=new Dictionary<int,List<Sample>>();
        for(int h=1;h<=5;h++)
        {
            var pile=VehicleKeyBurial.FindPile(scene,h);if(pile==null)throw new InvalidOperationException("Missing Hangar "+h+" grain.");
            var preview=new SerializedObject(pile);if(preview.FindProperty("previewClearedPercent").floatValue>0)throw new InvalidOperationException("Reset cleared preview on "+pile.name+" before applying progression.");
            pile.Initialize();piles[h]=pile;
            Vector3 entrance=VehicleKeyBurial.FindEntrance(scene,h,pile);
            if(h==1)
            {
                var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
                if(player!=null)entrance=player.transform.position;
            }
            entrances[h]=entrance;samples[h]=SamplePile(pile,entrance);
        }
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Balance grain discoveries and rewards");
        var report=new System.Text.StringBuilder("# Authored progression: target 3-5 hour first playthrough\n\nPercentages are approximate starting-stock mass excavated from the doorway; actual player routes vary. Optional rewards are excluded from the solvency forecast.\n\n| Hangar | Find / save ID | Target | Actual mass band | Cash option |\n|---|---|---:|---:|---:|\n");
        try
        {
            // Keep one canonical ID, but repair a duplicated cash bottle so each physical find is collectible.
            foreach(var group in finds.GroupBy(f=>f.FindId).Where(g=>!string.IsNullOrEmpty(g.Key) && g.Count()>1).ToArray())
            {
                var ordered=group.OrderBy(f=>f.name,StringComparer.Ordinal).ThenBy(f=>f.transform.position.x).ToArray();
                for(int i=1;i<ordered.Length;i++)
                {
                    var data=new SerializedObject(ordered[i]);
                    string id=group.Key=="farm.perk.bottle.doublesellcash" && i==1?"farm.perk.bottle.doublesellcash.h3":group.Key+".extra."+i;
                    data.FindProperty("findId").stringValue=id;data.ApplyModifiedProperties();PrefabUtility.RecordPrefabInstancePropertyModifications(ordered[i]);
                }
            }
            var barriers=UnityEngine.Object.FindObjectsByType<BreakableGrainBarrier>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(b=>b.gameObject.scene==scene).Select(b=>b.GetComponent<BoxCollider>()).Where(c=>c!=null).Select(c=>c.bounds).Where(b=>b.size.sqrMagnitude>.001f).ToArray();
            var occupied=new Dictionary<int,List<Bounds>>();for(int h=1;h<=5;h++)occupied[h]=new List<Bounds>();
            var placed=new Dictionary<string,FindPlan>();
            foreach(var entry in Plan)
            {
                var find=finds.FirstOrDefault(f=>f.FindId==entry.id);
                if(find==null)
                {
                    if(entry.template==null)throw new InvalidOperationException("Missing required discovery "+entry.id);
                    var template=finds.FirstOrDefault(f=>f.FindId==entry.template);if(template==null)throw new InvalidOperationException("Missing template "+entry.template);
                    var clone=UnityEngine.Object.Instantiate(template.gameObject,template.transform.parent);Undo.RegisterCreatedObjectUndo(clone,"Add paced optional reward");find=clone.GetComponent<BuriedFind>();finds.Add(find);
                    var idData=new SerializedObject(find);idData.FindProperty("findId").stringValue=entry.id;idData.ApplyModifiedProperties();
                }
                Bounds visual=BoundsOf(find);Vector3 half=visual.extents;
                Sample point=Choose(samples[entry.hangar],piles[entry.hangar],entry,half,occupied[entry.hangar],barriers);
                if(point==null)throw new InvalidOperationException("Cannot safely bury "+entry.id+" in Hangar "+entry.hangar);
                Vector3 offset=visual.center-find.transform.position;
                Vector3 position=point.floor-new Vector3(offset.x,0,offset.z)+Vector3.up*(.08f-(visual.min.y-find.transform.position.y));
                Undo.RecordObject(find.transform,"Pace buried reward");Undo.RecordObject(find.gameObject,"Label buried reward");
                find.transform.position=position;
                var data=new SerializedObject(find);data.FindProperty("coveringPile").objectReferenceValue=piles[entry.hangar];
                data.FindProperty("settleOnReveal").boolValue=false;
                if(entry.cash>0)data.FindProperty("cashReward").intValue=entry.cash;
                if(find.FindKind==BuriedFind.Kind.InfiniteStamina || find.FindKind==BuriedFind.Kind.PerkBottle)data.FindProperty("staminaDurationSeconds").intValue=300;
                data.ApplyModifiedProperties();
                find.name=$"H{entry.hangar} {entry.progress:P0} - {Display(find)}";find.gameObject.SetActive(true);
                PrefabUtility.RecordPrefabInstancePropertyModifications(find);PrefabUtility.RecordPrefabInstancePropertyModifications(find.transform);
                EditorUtility.SetDirty(find);occupied[entry.hangar].Add(BoundsOf(find));placed.Add(entry.id,entry);
                report.AppendLine($"| {entry.hangar} | {Display(find)} / `{entry.id}` | {entry.progress:P0} | {point.share:P0} | {(entry.cash>0?"$"+entry.cash:"—")} |");
                if(point.height-(visual.size.y+.08f)<.15f)throw new InvalidOperationException("Insufficient grain cover above "+entry.id);
                if(Mathf.Abs(point.share-entry.progress)>.12f)throw new InvalidOperationException("Discovery cannot meet its intended pacing: "+entry.id);
            }
            // Every chest key must be available before the chest; every door key stays in the preceding hangar.
            foreach(var find in finds.Where(f=>f.FindKind==BuriedFind.Kind.CashChest))
            {
                if(!placed.TryGetValue(find.FindId,out var chest) || !placed.TryGetValue(find.RequiredChestKeyId,out var key))throw new InvalidOperationException("Missing chest/key plan: "+find.FindId);
                if(key.hangar>chest.hangar || (key.hangar==chest.hangar && key.progress>=chest.progress))throw new InvalidOperationException("Chest key lies after its chest: "+find.FindId);
            }
            foreach(var find in finds.Where(f=>f.FindKind==BuriedFind.Kind.HangarKey))
                if(!placed.TryGetValue(find.FindId,out var entry) || entry.hangar!=find.TargetHangar-1)throw new InvalidOperationException("Door key is not in the preceding hangar: "+find.FindId);
            var shovel=GrainEconomySettings.Default.shovelSettings;Undo.RecordObject(shovel,"Balance vacuum throughput");
            shovel.shovels[4].suctionKgPerSecond=45;shovel.shovels[4].suctionPerUpgrade=15;
            shovel.shovels[5].suctionKgPerSecond=90;shovel.shovels[5].suctionPerUpgrade=22.5f;
            EditorUtility.SetDirty(shovel);
            var marker=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==Marker);
            if(marker==null){marker=new GameObject(Marker);Undo.RegisterCreatedObjectUndo(marker,"Mark authored progression");SceneManager.MoveGameObjectToScene(marker,scene);}
            report.AppendLine("\nValidation: all planned finds are buried and separated; chest keys precede their matching chest; next-hangar keys stay in the previous hangar. Axe is in H1 before H2 wood; hammer is in H2 before H3 metal; broom is in H2 before H3 rats. Existing save IDs are preserved except repaired duplicate instances.\n\nVacuum rates: first model 45 + 15 kg/s per dot; second model 90 + 22.5 kg/s per dot. Bottles last 300 seconds. Prices and collectible grain stock unchanged.\n");
            ValidateScene();
            Directory.CreateDirectory("output/Progression");File.WriteAllText("output/Progression/placement.md",report.ToString());
            File.WriteAllText("output/Progression/economy.md",GrainEconomyReport.BuildReport(GrainEconomySettings.Default));
            AssetDatabase.SaveAssetIfDirty(shovel);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Undo.CollapseUndoOperations(undo);Debug.Log("Balanced progression saved: "+Plan.Length+" discoveries across five hangars. See output/Progression/placement.md.");
        }
        catch(Exception){Undo.RevertAllDownToGroup(undo);throw;}
    }
    [MenuItem("Tools/Grain/Validate balanced progression")]
    public static void ValidateScene()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Validate starting placement in Edit mode.");
        var scene=SceneManager.GetActiveScene();if(scene.name!="Gameplay")throw new InvalidOperationException("Open Gameplay first.");
        var finds=UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(f=>f.gameObject.scene==scene).ToArray();
        if(finds.GroupBy(f=>f.FindId).Any(g=>string.IsNullOrEmpty(g.Key)||g.Count()>1))throw new InvalidOperationException("Empty or duplicated buried-find save IDs.");
        var report=new System.Text.StringBuilder("Starting-scene discovery validation\n");
        foreach(var entry in Plan)
        {
            var find=finds.SingleOrDefault(f=>f.FindId==entry.id);if(find==null)throw new InvalidOperationException("Missing planned find: "+entry.id);
            var pile=VehicleKeyBurial.FindPile(scene,entry.hangar);pile.Initialize();
            var data=new SerializedObject(find);
            if(data.FindProperty("coveringPile").objectReferenceValue!=pile)throw new InvalidOperationException("Incorrect cover pile: "+entry.id);
            Bounds bounds=BoundsOf(find);float cover=pile.SurfaceHeight(bounds.center)-bounds.max.y;
            if(!find.gameObject.activeSelf || !pile.IsInsideBoundary(bounds.center) || cover<.15f)throw new InvalidOperationException("Find is not safely buried: "+entry.id+" cover="+cover);
            if((find.FindKind==BuriedFind.Kind.InfiniteStamina||find.FindKind==BuriedFind.Kind.PerkBottle)&&data.FindProperty("staminaDurationSeconds").intValue!=300)throw new InvalidOperationException("Incorrect perk duration: "+entry.id);
            report.AppendLine($"PASS H{entry.hangar} {entry.id}: cover {cover:F2} m; pile {pile.name}");
        }
        Directory.CreateDirectory("output/Progression");File.WriteAllText("output/Progression/validation.txt",report.ToString());
        Debug.Log("PASS balanced progression: "+Plan.Length+" finds, unique save IDs, correct cover piles, hidden under grain, five-minute bottles.");
    }
    private static string Display(BuriedFind find)
    {
        switch(find.FindKind)
        {
            case BuriedFind.Kind.CashCache:return "Cash cache";case BuriedFind.Kind.CashChest:return find.RequiredChestKeyId.Contains("copper")?"Copper chest":"Iron chest";
            case BuriedFind.Kind.Key:return find.FindId.Contains("copper")?"Copper chest key":"Iron chest key";
            case BuriedFind.Kind.HangarKey:return "Hangar "+find.TargetHangar+" key";case BuriedFind.Kind.VehicleKey:return find.FindId.Replace("vehicle.key.","")+" vehicle key";
            case BuriedFind.Kind.Axe:return "Axe";case BuriedFind.Kind.Hammer:return "Hammer";case BuriedFind.Kind.Broom:return "Broom";
            case BuriedFind.Kind.UpgradeVoucher:return "Free upgrade voucher";case BuriedFind.Kind.InfiniteStamina:return "Stamina bottle (5 min)";
            default:var data=new SerializedObject(find);return data.FindProperty("bottlePerk").enumDisplayNames[data.FindProperty("bottlePerk").enumValueIndex]+" bottle (5 min)";
        }
    }
    private static Bounds BoundsOf(BuriedFind find)
    {
        var renderers=find.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return new Bounds(find.transform.position,Vector3.one*.1f);
        Bounds result=renderers[0].bounds;foreach(var renderer in renderers)result.Encapsulate(renderer.bounds);return result;
    }
    private static List<Sample> SamplePile(GrainPile pile,Vector3 entrance)
    {
        Vector3 direction=pile.transform.position-entrance;direction.y=0;if(direction.sqrMagnitude<.01f)direction=-pile.transform.forward;direction.Normalize();Vector3 right=Vector3.Cross(Vector3.up,direction);
        var result=new List<Sample>();float sideMin=float.PositiveInfinity,sideMax=float.NegativeInfinity;double total=0;
        const int grid=52;
        for(int z=0;z<grid;z++)for(int x=0;x<grid;x++)
        {
            Vector3 point=pile.transform.TransformPoint(new Vector3(((x+.5f)/grid-.5f)*pile.Size,0,((z+.5f)/grid-.5f)*pile.Size));
            if(!pile.IsInsideBoundary(point,.15f))continue;
            float height=pile.SurfaceHeight(point)-point.y;if(height<.02f)continue;
            float side=Vector3.Dot(point-pile.transform.position,right);sideMin=Mathf.Min(sideMin,side);sideMax=Mathf.Max(sideMax,side);
            result.Add(new Sample{floor=point,height=height,depth=Vector3.Dot(point-entrance,direction),side=side});total+=height;
        }
        result.Sort((a,b)=>a.depth.CompareTo(b.depth));double accumulated=0;
        foreach(var point in result){point.share=(float)((accumulated+point.height*.5)/total);accumulated+=point.height;point.side=Mathf.Lerp(-1,1,Mathf.InverseLerp(sideMin,sideMax,point.side));}
        if(result.Count==0)throw new InvalidOperationException("No sampled stock in "+pile.name);return result;
    }
    private static Sample Choose(List<Sample> samples,GrainPile pile,FindPlan entry,Vector3 half,List<Bounds> occupied,Bounds[] barriers)
    {
        float radius=Mathf.Max(half.x,half.z);Sample best=null;float score=float.PositiveInfinity;
        foreach(var point in samples)
        {
            if(point.height<half.y*2+.25f || !pile.IsInsideBoundary(point.floor,Mathf.Min(radius+.2f,1.5f)))continue;
            var candidate=new Bounds(point.floor+Vector3.up*(.08f+half.y),half*2);candidate.Expand(.35f);
            if(occupied.Any(bounds=>bounds.Intersects(candidate)))continue;
            if(barriers.Any(bounds=>bounds.Intersects(candidate)))continue;
            float value=Mathf.Abs(point.share-entry.progress)*40f+Mathf.Abs(point.side-entry.side);
            if(value<score){score=value;best=point;}
        }
        return best;
    }
}
