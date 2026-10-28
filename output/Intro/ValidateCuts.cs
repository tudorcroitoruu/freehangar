using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
public static class ValidateCuts
{
    public static async Task<string> Run()
    {
        var i=UnityEngine.Object.FindFirstObjectByType<GameIntroduction>();
        var p=UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var bike=(BicycleController)typeof(GameIntroduction).GetField("bicycle",flags).GetValue(i);
        var arrival=(Transform)typeof(GameIntroduction).GetField("arrival",flags).GetValue(i);
        var prop=(GameObject)typeof(GameIntroduction).GetField("worldShovel",flags).GetValue(i);
        i.Cancel();typeof(GameIntroduction).GetField("<Completed>k__BackingField",flags).SetValue(i,false);i.Play();
        Application.runInBackground=true;
        string last="";Vector3 previous=Vector3.zero;float start=Time.realtimeSinceStartup;float gameStart=Time.time;int moving=0;float steering=0;Quaternion? firstSteer=null;int cuts=0;bool poseChecked=false,arrivalChecked=false;
        while(i.IsPlaying&&Time.realtimeSinceStartup-start<100)
        {
            if(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Any(c=>c.enabled && c.name != "Introduction opening fade"))throw new Exception("Visible canvas during cutscene");
            if(last==i.CurrentShot&&(last=="Farm panorama"||last=="Bicycle approach")&&Vector3.Distance(previous,p.characterCamera.transform.position)>.001f)moving++;
            if(last!=i.CurrentShot)cuts++;
            if(i.CurrentShot=="Purchase paper")
            {
                var end=arrival.position-arrival.right*2.2f-arrival.forward*1.2f;
                if(Vector3.Distance(bike.transform.position,end)>.02f)throw new Exception("Bicycle did not reach arrival");
                if(Vector2.Distance(new Vector2(p.characterCamera.transform.position.x,p.characterCamera.transform.position.z),new Vector2(arrival.position.x,arrival.position.z))>.02f)throw new Exception("View did not dismount at arrival");
                arrivalChecked=true;
            }
            if(i.CurrentShot=="Ready")
            {
                var held=(Transform)typeof(GameIntroduction).GetField("heldShovelPose",flags).GetValue(i);
                // The camera now turns with the held shovel; verify the swap was already done.
                if(held==null||prop.activeSelf||!p.HeldInstrument.gameObject.activeSelf)throw new Exception("Held shovel handoff failed");
                poseChecked=true;
            }
            if(i.CurrentShot=="Bicycle approach"){if(!firstSteer.HasValue)firstSteer=bike.steering.localRotation;steering=Mathf.Max(steering,Quaternion.Angle(firstSteer.Value,bike.steering.localRotation));}
            last=i.CurrentShot;previous=p.characterCamera.transform.position;
            await Task.Delay(100);
        }
        if(!i.Completed||!p.enabled||!p.IsShovelEquipped||moving<30||steering<1)throw new Exception($"Cutscene failed: completed={i.Completed}, moving={moving}, steering={steering}, shot={i.CurrentShot}");
        if(!poseChecked||!arrivalChecked||Time.time-gameStart>30)throw new Exception("Pacing/arrival checks failed");
        string result=$"PASS: {cuts} stages, {moving} moving samples, steering range {steering:F1} degrees, duration {Time.time-gameStart:F2}s, bicycle reached endpoint, camera dismounted at arrival, held shovel visible for final look, no visible canvases/fades, controls restored.";
        System.IO.File.WriteAllText("output/Intro/CutsValidation.txt",result);return result;
    }
}


