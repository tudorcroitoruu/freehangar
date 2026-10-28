using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class CaptureDiscoveries
{
    static void Set(object o,string name,object v)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
    static object Get(object o,string name)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static async Task Shot(string name){ScreenCapture.CaptureScreenshot("output/Discoveries/"+name+".png");await Task.Delay(180);}
    public static async Task<string> Run()
    {
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();var camera=player.characterCamera;var actor=player.GetComponent<PlayerInteractor>();
        var oldData=SaveSystem.Data;bool loaded=SaveSystem.savesLoaded;float time=Time.timeScale;bool active=player.enabled;
        var pos=camera.transform.position;var rot=camera.transform.rotation;
        var keys=new[]{"/save","/save_Settings","levelsCompleted"};var prefs=keys.Select(PlayerPrefs.GetString).ToArray();var exists=keys.Select(PlayerPrefs.HasKey).ToArray();
        var managers=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(b=>b is GoalsManager || b is GrainProgression || b is HangarKeyDoor).ToArray();var enabled=managers.Select(b=>b.enabled).ToArray();
        var created=new List<GameObject>();
        try
        {
            foreach(var b in managers)b.enabled=false;player.enabled=false;Time.timeScale=1;
            SaveSystem.savesLoaded=true;SaveSystem.Data=new PlayerData{cash=100,foundSecrets=new[]{"farm.key.copper","farm.key.iron"}};
            camera.transform.position=new Vector3(0,1.5f,23.4f);camera.transform.LookAt(new Vector3(0,.65f,21));
            var finds=UnityEngine.Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var chestSource=finds.First(f=>f.FindKind==BuriedFind.Kind.CashChest);
            var chest=UnityEngine.Object.Instantiate(chestSource,new Vector3(0,.04f,21),chestSource.transform.rotation);created.Add(chest.gameObject);Set(chest,"coveringPile",null);Set(chest,"findId","capture.chest");await Task.Delay(400);
            if(!chest.TryInteract(actor))throw new Exception("Chest capture did not start");
            await Task.Delay(820);await Shot("KeyInsertion");await Task.Delay(440);await Shot("ChestOpening");await Task.Delay(1900);
            var bottleSource=finds.First(f=>f.FindKind==BuriedFind.Kind.InfiniteStamina);
            var bottle=UnityEngine.Object.Instantiate(bottleSource,new Vector3(0,.4f,21.5f),Quaternion.identity);created.Add(bottle.gameObject);Set(bottle,"coveringPile",null);Set(bottle,"findId","capture.bottle");Set(bottle,"settleOnReveal",false);await Task.Delay(400);
            if(!bottle.TryInteract(actor))throw new Exception("Bottle capture did not start");
            await Task.Delay(1030);await Shot("Drinking");await Task.Delay(1300);await Shot("StaminaLook");
            SaveSystem.Data.infiniteStaminaUntilUtc=0;await Task.Delay(1400);
            var keySource=finds.First(f=>f.FindKind==BuriedFind.Kind.HangarKey&&f.TargetHangar==2);
            var key=UnityEngine.Object.Instantiate(keySource,new Vector3(0,.55f,22),Quaternion.identity);created.Add(key.gameObject);Set(key,"coveringPile",null);Set(key,"findId","capture.hangar.key");Set(key,"settleOnReveal",false);
            key.transform.rotation=Quaternion.Euler(0,180,0);key.gameObject.SetActive(true);await Task.Delay(400);
            camera.transform.position=new Vector3(0,1.3f,23.4f);camera.transform.LookAt(new Vector3(0,1.2f,22));await Shot("HangarKey");
            return "Captured insertion, opening, drinking, stamina look and numbered hangar key.";
        }
        finally
        {
            foreach(var g in created)if(g!=null)UnityEngine.Object.DestroyImmediate(g);
            foreach(var fx in UnityEngine.Object.FindObjectsByType<DiscoveryBurst>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(fx.gameObject);
            SaveSystem.Data=oldData;SaveSystem.savesLoaded=loaded;
            for(int i=0;i<keys.Length;i++){if(exists[i])PlayerPrefs.SetString(keys[i],prefs[i]);else PlayerPrefs.DeleteKey(keys[i]);}PlayerPrefs.Save();
            for(int i=0;i<managers.Length;i++)if(managers[i]!=null)managers[i].enabled=enabled[i];
            camera.transform.SetPositionAndRotation(pos,rot);player.enabled=active;Time.timeScale=time;
            if(DataHolderUI.Instance!=null)DataHolderUI.Instance.RefreshDataPlayerInstant();
        }
    }
}
