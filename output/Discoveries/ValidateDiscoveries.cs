using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public static class ValidateDiscoveries
{
    static List<GameObject> created=new List<GameObject>();
    static List<string> checks=new List<string>();
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add("PASS "+message);}
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    static object Get(object o,string name)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static async Task Wait(int ms){Physics.SyncTransforms();await Task.Delay(ms);}
    static BuriedFind Clone(BuriedFind source,string id,Vector3 position)
    {
        var g=UnityEngine.Object.Instantiate(source.gameObject,position,source.transform.rotation);created.Add(g);g.name="Validation "+id;
        var f=g.GetComponent<BuriedFind>();Set(f,"findId",id);Set(f,"coveringPile",null);Set(f,"settleOnReveal",false);return f;
    }
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        var originalData=SaveSystem.Data;bool loaded=SaveSystem.savesLoaded;float time=Time.timeScale;bool background=Application.runInBackground;
        var keys=new[]{"/save","/save_Settings","levelsCompleted"};var prefs=keys.Select(PlayerPrefs.GetString).ToArray();var exists=keys.Select(PlayerPrefs.HasKey).ToArray();
        var suspend=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b=>b is GoalsManager || b is GrainProgression || b is HangarKeyDoor).ToArray();var enabled=suspend.Select(b=>b.enabled).ToArray();
        try
        {
            foreach(var b in suspend)b.enabled=false;
            SaveSystem.savesLoaded=true;SaveSystem.Data=new PlayerData{cash=100,foundSecrets=Array.Empty<string>(),infiniteStaminaUntilUtc=0};Time.timeScale=1;Application.runInBackground=true;
            var rig=new GameObject("Validation discovery player");created.Add(rig);rig.transform.position=new Vector3(1000,1000,1003);
            var camObject=new GameObject("Validation camera");camObject.transform.SetParent(rig.transform,false);var cam=camObject.AddComponent<Camera>();cam.depth=-100;
            var actor=rig.AddComponent<PlayerInteractor>();
            var all=UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var pos=new Vector3(1000,1000,1000);
            var chest=Clone(all.First(f=>f.FindKind==BuriedFind.Kind.CashChest),"test.chest",pos);
            Set(chest,"requiredKeyId","test.chest.key");Set(chest,"cashReward",500);await Wait(400);
            SaveSystem.Data.foundSecrets=new[]{HangarKeyDoor.KeyFor(2)};
            Check(chest.TryGetPrompt(actor,out var p)&&!p.ShowKey&&!chest.TryInteract(actor),"Hangar key cannot open a chest");
            SaveSystem.Data.foundSecrets=new[]{"test.chest.key"};
            Check(chest.TryInteract(actor)&&actor.DiscoveryBusy,"Chest begins insertion and reserves interaction");
            Check(!chest.TryInteract(actor)&&SaveSystem.Money==100,"Repeated input cannot duplicate chest reward");
            await Wait(450);Check(Get(chest,"insertionKey")!=null&&!BuriedFind.WasFound(SaveSystem.Data,"test.chest"),"Visible insertion key precedes reward");
            chest.enabled=false;await Wait(100);
            Check(!actor.DiscoveryBusy&&SaveSystem.Money==100&&!BuriedFind.WasFound(SaveSystem.Data,"test.chest"),"Interrupted insertion releases input and grants no reward");
            chest.enabled=true;await Wait(100);Check(chest.TryInteract(actor),"Interrupted chest can be retried");
            await Wait(1500);Check(SaveSystem.Money==600&&BuriedFind.WasFound(SaveSystem.Data,"test.chest"),"Cash commits once when chest opens");
            var lid=(Transform)Get(chest,"lidPivot");Check(Quaternion.Angle(lid.localRotation,(Quaternion)Get(chest,"closedLid"))>20,"Chest lid animates open");
            Check(UnityEngine.Object.FindObjectsByType<DiscoveryBurst>(FindObjectsSortMode.None).Length>0,"Opening spawns treasure VFX");
            await Wait(1900);Check(!chest.gameObject.activeSelf&&!actor.DiscoveryBusy&&SaveSystem.Money==600,"Chest finishes and restores input without second payment");
            var drink=Clone(all.First(f=>f.FindKind==BuriedFind.Kind.InfiniteStamina),"test.drink",pos);Set(drink,"staminaDurationSeconds",30);await Wait(400);
            Check(drink.TryInteract(actor)&&SaveSystem.Data.infiniteStaminaUntilUtc==0,"Bottle starts drinking before activating perk");
            await Wait(800);Check(Vector3.Distance(drink.transform.position,cam.transform.position)<.6f&&SaveSystem.Data.infiniteStaminaUntilUtc==0,"Bottle lifts to first-person mouth position before sip");
            drink.enabled=false;await Wait(100);Check(!actor.DiscoveryBusy&&Vector3.Distance(drink.transform.position,pos)<.001f&&SaveSystem.Data.infiniteStaminaUntilUtc==0,"Interrupted drink restores bottle and grants no stamina");
            drink.enabled=true;await Wait(100);Check(drink.TryInteract(actor),"Interrupted drink can be retried");
            await Wait(1400);Check(SaveSystem.Data.infiniteStaminaUntilUtc>DateTimeOffset.UtcNow.ToUnixTimeSeconds(),"Sip starts saved infinite-stamina timer");
            var blend=UnityEngine.Object.FindFirstObjectByType<InfiniteStaminaVolume>();Check(blend.Blend>0&&blend.Blend<1,"Stamina volume blends gradually after sip");
            await Wait(1200);Check(blend.Blend>.99f&&!drink.gameObject.activeSelf&&!actor.DiscoveryBusy,"Full stamina look and restored input after drinking");
            SaveSystem.Data.infiniteStaminaUntilUtc=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-1;await Wait(1400);Check(blend.Blend<.01f,"Stamina look fades out on expiry");
            var pickup=Clone(all.First(f=>f.FindKind==BuriedFind.Kind.UpgradeVoucher),"test.voucher",pos);await Wait(400);int vouchers=SaveSystem.Data.freeUpgradeVouchers;
            Check(pickup.TryInteract(actor)&&SaveSystem.Data.freeUpgradeVouchers==vouchers+1&&!pickup.TryInteract(actor),"Other hidden pickups award once and disappear");
            Check(UnityEngine.Object.FindObjectsByType<DiscoveryBurst>(FindObjectsSortMode.None).Length>0,"Other hidden pickups spawn VFX");
            var hangarKey=Clone(all.First(f=>f.FindKind==BuriedFind.Kind.HangarKey&&f.TargetHangar==2),"test.hangar.key",pos);Set(hangarKey,"findId",HangarKeyDoor.KeyFor(2));await Wait(400);
            Check(hangarKey.TryGetPrompt(actor,out p)&&p.Action.Contains("HANGAR 2"),"Hangar key prompt names its destination");
            Check(hangarKey.TryInteract(actor)&&BuriedFind.WasFound(SaveSystem.Data,HangarKeyDoor.KeyFor(2)),"Numbered key ownership is saved separately from chest keys");
            var go=new GameObject("Validation hangar");created.Add(go);go.SetActive(false);var grain=new GameObject("Validation grain");created.Add(grain);grain.SetActive(false);
            var visibility=go.AddComponent<HangarGrainVisibility>();Set(visibility,"grainRoot",grain);
            var left=new GameObject("Left").transform;left.SetParent(go.transform);var right=new GameObject("Right").transform;right.SetParent(go.transform);
            var door=go.AddComponent<HangarKeyDoor>();Set(door,"hangarNumber",3);Set(door,"grainVisibility",visibility);Set(door,"leftDoor",left);Set(door,"rightDoor",right);go.SetActive(true);await Wait(100);
            Check(!door.TryInteract(actor)&&!grain.activeSelf,"Wrong numbered key leaves hangar locked");
            Set(door,"hangarNumber",2);Check(door.TryInteract(actor)&&door.IsUnlocked&&grain.activeSelf,"Matching key unlocks hangar and enables grain");
            await Wait(1800);Check(left.localPosition.x < -5 && right.localPosition.x >5,"Both hangar leaves slide open");
            Check(!door.TryInteract(actor)&&BuriedFind.WasFound(SaveSystem.Data,"farm.hangar.open.2"),"Door unlock persists and cannot be repeated");
            Set(door,"loadedProfile",null);await Wait(100);Check(door.IsUnlocked&&left.localPosition.x < -5,"Saved unlocked door restores open state");
            await Wait(2400);Check(UnityEngine.Object.FindObjectsByType<DiscoveryBurst>(FindObjectsSortMode.None).Length==0,"Discovery effects clean up after their lifetime");
            string report=string.Join("\n",checks);System.IO.File.WriteAllText("output/Discoveries/Validation.txt",report);return report;
        }
        finally
        {
            foreach(var g in created)if(g!=null)UnityEngine.Object.DestroyImmediate(g);
            foreach(var effect in UnityEngine.Object.FindObjectsByType<DiscoveryBurst>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(effect.gameObject);
            SaveSystem.Data=originalData;SaveSystem.savesLoaded=loaded;
            for(int i=0;i<keys.Length;i++){if(exists[i])PlayerPrefs.SetString(keys[i],prefs[i]);else PlayerPrefs.DeleteKey(keys[i]);}PlayerPrefs.Save();
            for(int i=0;i<suspend.Length;i++)if(suspend[i]!=null)suspend[i].enabled=enabled[i];
            Time.timeScale=time;Application.runInBackground=background;
            if(DataHolderUI.Instance!=null)DataHolderUI.Instance.RefreshDataPlayerInstant();
        }
    }
}
