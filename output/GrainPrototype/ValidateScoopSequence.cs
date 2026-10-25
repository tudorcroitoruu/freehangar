using System;
using System.Reflection;
using UnityEngine;
public static class ValidateScoopSequence
{
    public static string Main()
    {
        if(!Application.isPlaying)throw new Exception("Play mode required");
        const BindingFlags f=BindingFlags.Instance|BindingFlags.NonPublic;
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        var tool=player.GetComponentInChildren<ShovelTool>();
        var physics=player.GetComponentInChildren<ShovelGrainPhysics>();
        var pile=UnityEngine.Object.FindFirstObjectByType<GrainPile>();
        var field=(GrainHeightField)typeof(GrainPile).GetField("field",f).GetValue(pile);
        player.enabled=false; player.characterController.enabled=false;
        Vector3 point=default;float score=0;
        for(int z=2;z<field.Resolution-2;z++)for(int x=2;x<field.Resolution-2;x++)
        {
            int i=z*field.Resolution+x;float slope=field.Heights[i+1]-field.Heights[i];
            Vector3 p=pile.transform.TransformPoint(new Vector3(x*field.Spacing-field.Size*.5f,field.Heights[i],z*field.Spacing-field.Size*.5f));
            if(field.Heights[i]>.2f && slope>score && pile.IsInsideBoundary(p,.6f)){score=slope;point=p;}
        }
        player.characterCamera.transform.position=point+Vector3.left*.75f+Vector3.up*.45f;
        player.characterCamera.transform.LookAt(point);Physics.SyncTransforms();
        tool.CancelAction();
        if(!tool.TryUse(player.characterCamera,player.transform))throw new Exception("No action");
        if((GrainPile)typeof(ShovelTool).GetField("pendingPile",f).GetValue(tool)!=pile)throw new Exception("Aim did not select pile");
        float duration=(float)typeof(ShovelTool).GetField("actionDuration",f).GetValue(tool);
        int before=physics.ScoopEmittedCount;
        tool.Tick(duration*.37f);
        if(tool.Kilograms!=0)throw new Exception("Load before insertion");
        tool.Tick(duration*.1f);Vector3 held=tool.PositionOffset;
        if(tool.Kilograms<=0 || physics.ScoopEmittedCount!=before)throw new Exception("Buried fill/emission timing");
        tool.Tick(duration*.08f);
        if(Vector3.Distance(held,tool.PositionOffset)>.0001f)throw new Exception("Blade withdrew during filling");
        tool.Tick(duration*.02f);float filled=tool.Kilograms;
        tool.Tick(duration*.27f);
        if(filled<6.49f || tool.Kilograms!=filled || physics.ScoopEmittedCount<=before)throw new Exception("Lift load/debris timing");
        tool.Tick(duration*.2f);
        if(tool.IsBusy)throw new Exception("Action did not finish");
        return "PASS: empty insertion, progressive buried load, stationary cutting phase, 6.5 kg carried on lift, debris only after withdrawal, action completion";
    }
}
