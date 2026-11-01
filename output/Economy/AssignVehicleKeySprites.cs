using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class AssignVehicleKeySprites
{
    public static object Main()
    {
        if(Application.isPlaying)throw new Exception("Edit Mode required");
        var settings=GrainEconomySettings.Default.vehicleSettings;
        string[] ids={"motoroller","trike","lawnmower","pickup","pickupBig","pickupLast"};
        Undo.RecordObject(settings,"Assign vehicle key sprites");
        using(var so=new SerializedObject(settings))
        {
            var vehicles=so.FindProperty("vehicles");
            for(int i=0;i<ids.Length;i++)
            {
                string path="Assets/Textures/Renders/Items/Vehicles/VehicleKeys_"+i.ToString("0000")+"_Layer-"+(i+2)+".png";
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if(sprite==null)throw new Exception("Missing key sprite: "+path);
                for(int j=0;j<vehicles.arraySize;j++)
                {
                    var entry=vehicles.GetArrayElementAtIndex(j);
                    if(entry.FindPropertyRelative("id").stringValue==ids[i])entry.FindPropertyRelative("keySprite").objectReferenceValue=sprite;
                }
            }
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
        return settings.vehicles.Select(v=>new{v.id,v.requiresKey,key=v.RequiredKeyId,sprite=v.keySprite==null?null:v.keySprite.name}).ToArray();
    }
}
