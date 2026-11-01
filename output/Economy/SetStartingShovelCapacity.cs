using System;
using UnityEditor;
using UnityEngine;
public static class SetStartingShovelCapacity
{
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Exit Play Mode before changing the starting shovel");
        var settings=GrainEconomySettings.Default.shovelSettings;
        Undo.RecordObject(settings,"Set starting shovel capacity to 2.5 kg");
        using(var so=new SerializedObject(settings))
        {
            so.FindProperty("shovels").GetArrayElementAtIndex(0).FindPropertyRelative("capacityKg").floatValue=2.5f;
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
        var freeBag=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab").GetComponent<GrainContainer>();
        return new{shovel=settings.shovels[0].name,startingKg=settings.Capacity(0),freeBagKg=freeBag.CapacityKg,fullScoops=freeBag.CapacityKg/settings.Capacity(0),nextUpgradeKg=settings.Capacity(1)};
    }
}
