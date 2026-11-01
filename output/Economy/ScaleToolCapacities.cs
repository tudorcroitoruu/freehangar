using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class ScaleToolCapacities
{
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Exit Play Mode before editing tool progression");
        var settings=GrainEconomySettings.Default.shovelSettings;
        string backup="output/Economy/Backups/ShovelUpgradeSettings.before-capacity-progression.asset";
        if(!File.Exists(backup))File.Copy(AssetDatabase.GetAssetPath(settings),backup);
        float[] capacities={2.5f,8.5f,14.5f,20f,150f,300f};
        float[] increments={1f,1f,1f,1f,37.5f,75f};
        if(settings.shovels.Length!=capacities.Length)throw new Exception("Unexpected tool progression");
        Undo.RecordObject(settings,"Scale shovel and vacuum capacities");
        using(var so=new SerializedObject(settings))
        {
            var tools=so.FindProperty("shovels");
            for(int i=0;i<capacities.Length;i++)
            {
                var tool=tools.GetArrayElementAtIndex(i);
                tool.FindPropertyRelative("capacityKg").floatValue=capacities[i];
                tool.FindPropertyRelative("capacityPerUpgrade").floatValue=increments[i];
            }
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
        for(int level=1;level<=settings.MaxUpgrades;level++)
            if(settings.Capacity(level)<=settings.Capacity(level-1))throw new Exception("Capacity did not increase at upgrade "+level);
        return new{tools=settings.shovels.Select((s,i)=>new{s.name,s.capacityKg,s.capacityPerUpgrade,lastInTier=settings.Capacity(i*4+3)}).ToArray(),finalCapacityKg=settings.Capacity(settings.MaxUpgrades),monotonic=true};
    }
}
