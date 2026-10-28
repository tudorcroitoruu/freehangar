using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateWeighStation
{
    static List<GameObject> created = new List<GameObject>();
    static List<string> checks = new List<string>();
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add("PASS "+text);}
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    static GrainContainer Bag(Vector3 position,float kg)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);created.Add(g);g.name="Validation filled box";g.transform.position=position;g.transform.localScale=Vector3.one*.5f;
        var b=g.AddComponent<GrainContainer>();Set(b,"kilograms",kg);return b;
    }
    static async Task Wait(int ms){Physics.SyncTransforms();await Task.Delay(ms);}
    public static async Task<string> Run()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        var authored=UnityEngine.Object.FindFirstObjectByType<GrainWeighStation>();authored.enabled=false;
        var data=SaveSystem.Data;
        var keys=new[]{"/save","/save_Settings","levelsCompleted"};var values=new string[3];var exists=new bool[3];
        for(int i=0;i<3;i++){exists[i]=PlayerPrefs.HasKey(keys[i]);values[i]=PlayerPrefs.GetString(keys[i]);}
        float oldTime=Time.timeScale;bool background=Application.runInBackground;
        try
        {
            Time.timeScale=1;Application.runInBackground=true;SaveSystem.Data=new PlayerData();SaveSystem.Data.cash=100;
            var rig=new GameObject("Validation scale");created.Add(rig);rig.transform.position=new Vector3(1000,1000,1000);
            var s=rig.AddComponent<GrainWeighStation>();Set(s,"clearDelay",.3f);Set(s,"saleDuration",.6f);
            var center=rig.transform.position+Vector3.up*.4f;
            var empty=Bag(center,0);await Wait(650);Check(empty!=null && s.CompletedSales==0,"Empty containers are not sold");empty.gameObject.SetActive(false);
            var b=Bag(center,10);b.SetCarried(true);await Wait(650);Check(!s.IsSelling && SaveSystem.Money==100,"Held bags are not sold");
            b.SetCarried(false);b.transform.position=center+Vector3.right*2.5f;await Wait(650);Check(!s.IsSelling,"Cargo outside center is not sold");
            b.transform.position=center;
            var player=new GameObject("Validation player");created.Add(player);player.transform.position=center;player.AddComponent<CharacterController>();
            await Wait(700);Check(s.IsBlocked && b!=null && SaveSystem.Money==100,"Player blocks sale and cash");
            player.transform.position=center+Vector3.right*20;await Wait(450);Check(s.IsSelling && !b.CanCarry,"Clear platform starts animation and locks pickup");
            Check(b.transform.position.y>center.y && b.transform.localScale.x>0,"Sale lifts cargo before shrinking to zero");
            player.transform.position=center;await Wait(100);Check(!s.IsSelling && b.enabled && Vector3.Distance(b.transform.position,center)<.001f && SaveSystem.Money==100,"Entering during animation restores cargo without payout");
            player.transform.position=center+Vector3.right*20;
            var source=UnityEngine.Object.FindFirstObjectByType<BicycleController>();
            var vehicle=UnityEngine.Object.Instantiate(source.gameObject,center,Quaternion.identity);created.Add(vehicle);vehicle.name="Validation vehicle";vehicle.GetComponent<BicycleController>().enabled=false;
            await Wait(700);Check(s.IsBlocked && SaveSystem.Money==100,"Vehicle blocks sale even without rider");
            vehicle.transform.position=center+Vector3.right*20;await Wait(1200);
            Check(b==null && s.CompletedSales==1 && SaveSystem.Money==120,"Filled box disappears and pays exactly its grain value");
            await Wait(600);Check(SaveSystem.Money==120,"Sold cargo cannot pay twice");
            var a=Bag(center,5);var c=Bag(center+Vector3.right,7);await Wait(2200);
            Check(a==null && c==null && SaveSystem.Money==144,"Multiple containers sell once each in sequence");
            var missing=Bag(center,10);SaveSystem.Data=null;await Wait(1000);Check(missing!=null && !s.IsSelling,"Missing save profile preserves cargo");
            SaveSystem.Data=new PlayerData();SaveSystem.Data.cash=int.MaxValue;await Wait(1000);Check(missing!=null && !s.IsSelling,"Cash overflow preserves cargo");
            SaveSystem.Data.cash=0;await Wait(450);Check(s.IsSelling,"Sale recovers after account becomes available");s.enabled=false;
            Check(missing.enabled && Vector3.Distance(missing.transform.position,center)<.001f,"Disabling station restores pending cargo");
            var holder=new GameObject("Inactive sack setup");created.Add(holder);holder.SetActive(false);
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/SmallBag.prefab");
            var sackObject=UnityEngine.Object.Instantiate(prefab,holder.transform);created.Add(sackObject);
            foreach(var outline in sackObject.GetComponentsInChildren<Outline>(true))UnityEngine.Object.DestroyImmediate(outline);
            var sack=sackObject.GetComponent<GrainContainer>();Set(sack,"kilograms",20f);
            sackObject.transform.position=authored.transform.position+Vector3.up*.2f;
            holder.SetActive(true);sackObject.transform.SetParent(null,true);
            var sackBody=sackObject.GetComponent<Rigidbody>();if(sackBody!=null)sackBody.isKinematic=true;
            authored.enabled=true;await Wait(3000);
            Check(sack==null && SaveSystem.Money==40,"Real filled sack sells on authored station for exact cash");
            string report=string.Join("\n",checks);System.IO.File.WriteAllText("output/WeighStation/Validation.txt",report);return report;
        }
        finally
        {
            foreach(var g in created)if(g!=null)UnityEngine.Object.DestroyImmediate(g);
            SaveSystem.Data=data;
            for(int i=0;i<3;i++){if(exists[i])PlayerPrefs.SetString(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}PlayerPrefs.Save();
            if(DataHolderUI.Instance!=null)DataHolderUI.Instance.RefreshDataPlayerInstant();
            Time.timeScale=oldTime;Application.runInBackground=background;authored.enabled=true;
        }
    }
}
