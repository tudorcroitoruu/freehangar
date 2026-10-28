using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateIntroduction
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static async Task<string> Run()
    {
        var intro=UnityEngine.Object.FindFirstObjectByType<GameIntroduction>();var manager=UnityEngine.Object.FindFirstObjectByType<GameManager>();var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        Check(!manager.skipTutorial,"Default skipTutorial must be false");
        intro.Cancel();typeof(GameIntroduction).GetField("<Completed>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(intro,false);
        Check(intro.Play(),"Introduction starts");
        var shots=new HashSet<string>();var saved=new HashSet<string>();string stage="";float stageStart=Time.realtimeSinceStartup;float start=stageStart;
        while(intro.IsPlaying&&Time.realtimeSinceStartup-start<65)
        {
            if(stage!=intro.CurrentShot){stage=intro.CurrentShot;stageStart=Time.realtimeSinceStartup;shots.Add(stage);}
            Check(!player.enabled&&!player.characterController.enabled,"Gameplay input must stay disabled");
            Check(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).All(c=>!c.enabled||c.name=="Introduction fade"),"HUD must stay hidden in every shot");
            if(Time.realtimeSinceStartup-stageStart>1.7f&&!saved.Contains(stage))
            {
                ScreenCapture.CaptureScreenshot("output/Intro/"+stage.Replace(" ","")+".png");saved.Add(stage);
            }
            await Task.Delay(80);
        }
        Check(intro.Completed&&!intro.IsPlaying,"Sequence must finish");
        Check(player.enabled&&player.characterController.enabled,"Normal controller restored");
        Check(player.characterCamera.transform.parent==player.transform,"Camera returned to player");
        Check(player.IsShovelEquipped,"Shovel equipped for gameplay");
        Check(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Any(c=>c.name=="GameCanvas"&&c.enabled),"HUD restored only after finish");
        Check(shots.Contains("Farm panorama")&&shots.Contains("Bicycle approach")&&shots.Contains("Purchase paper")&&shots.Contains("Grain reveal")&&shots.Contains("First shovel"),"All requested shots played");
        Check(!intro.Play(),"Completed intro does not replay accidentally");
        ScreenCapture.CaptureScreenshot("output/Intro/GameplayHandoff.png");await Task.Delay(200);
        string report="PASS: default false; all five shots; input/HUD suppressed throughout; full completion; camera/controller/HUD restored; shovel equipped; replay prevented.\nShots: "+string.Join(", ",shots);
        File.WriteAllText("output/Intro/Validation.txt",report);return report;
    }
}
