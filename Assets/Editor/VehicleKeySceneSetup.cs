using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VehicleKeySceneSetup
{
    private const string RootName="Vehicle keys - Hangars 2-4";
    [InitializeOnLoadMethod]
    private static void InstallAfterImport()
    {
        EditorApplication.delayCall+=()=>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            Scene scene=SceneManager.GetActiveScene();
            if(scene.name!="Gameplay")return;
            foreach(var root in scene.GetRootGameObjects()) if(root.name==RootName)return;
            Apply();
        };
    }
    [MenuItem("Tools/Grain/Place vehicle keys in Hangars 2-4")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Place keys outside Play Mode.");
        var scene=SceneManager.GetActiveScene();if(scene.name!="Gameplay")throw new InvalidOperationException("Open Gameplay first.");
        var settings=Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
        if(settings==null)throw new InvalidOperationException("Vehicle upgrade settings are missing.");
        bool wasDirty=scene.isDirty;
        var report=new System.Text.StringBuilder();
        // Validate every location before changing the scene.
        var points=new List<Vector3>();var piles=new List<GrainPile>();var prefabs=new List<GameObject>();
        foreach(var plan in VehicleKeyBurial.Plan)
        {
            var pile=VehicleKeyBurial.FindPile(scene,plan.hangar);
            var prefab=Resources.Load<GameObject>("VehicleKeys/"+plan.vehicleId);
            var profile=settings.Find(plan.vehicleId);
            if(prefab==null || profile==null || !profile.requiresKey || prefab.GetComponent<BuriedFind>()==null)
                throw new InvalidOperationException("Missing key prefab/profile: "+plan.vehicleId);
            var filter=prefab.GetComponent<MeshFilter>();
            if(filter==null || filter.sharedMesh==null || filter.sharedMesh.vertexCount==0)throw new InvalidOperationException("Key mesh missing: "+plan.vehicleId);
            if(!VehicleKeyBurial.TryPlacement(pile,VehicleKeyBurial.FindEntrance(scene,plan.hangar,pile),plan.progress,out var point))
                throw new InvalidOperationException("No buried location in Hangar "+plan.hangar);
            points.Add(point);piles.Add(pile);prefabs.Add(prefab);
        }
        GameObject group=null;
        foreach(var root in scene.GetRootGameObjects())if(root.name==RootName)group=root;
        if(group==null){group=new GameObject(RootName);SceneManager.MoveGameObjectToScene(group,scene);Undo.RegisterCreatedObjectUndo(group,"Place vehicle keys");}
        for(int i=0;i<VehicleKeyBurial.Plan.Length;i++)
        {
            var plan=VehicleKeyBurial.Plan[i];var profile=settings.Find(plan.vehicleId);
            var find=VehicleKeyBurial.FindKey(scene,profile.RequiredKeyId);
            if(find==null)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[i],scene);
                Undo.RegisterCreatedObjectUndo(instance,"Place vehicle key");find=instance.GetComponent<BuriedFind>();
            }
            Undo.RecordObject(find,"Configure vehicle key");Undo.RecordObject(find.transform,"Bury vehicle key");
            Undo.SetTransformParent(find.transform,group.transform,"Organize vehicle keys");
            find.name=$"Vehicle key - {profile.displayName} - HANGAR {plan.hangar} ({plan.progress:P0} from door)";
            find.transform.SetPositionAndRotation(points[i],Quaternion.Euler(0,27+plan.hangar*41,0));
            find.transform.localScale=Vector3.one;
            find.ConfigureVehicleKey(profile.RequiredKeyId,profile.displayName,piles[i]);
            find.gameObject.SetActive(true);
            // Scene view shows physical key geometry; Play Mode hides it until excavated.
            foreach(var renderer in find.GetComponentsInChildren<Renderer>(true))renderer.enabled=true;
            EditorUtility.SetDirty(find);PrefabUtility.RecordPrefabInstancePropertyModifications(find);
            PrefabUtility.RecordPrefabInstancePropertyModifications(find.transform);
            float cover=piles[i].SurfaceHeight(points[i])-points[i].y;
            report.AppendLine($"PASS {find.name}: {cover:F2} m of grain above key; pile={piles[i].name}; mesh={find.GetComponent<MeshFilter>().sharedMesh.vertexCount} vertices; saved ID={profile.RequiredKeyId}.");
        }
        CleanupUnusedKeyBows(scene,report);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!wasDirty)
        {
            EditorSceneManager.SaveScene(scene);
            report.AppendLine("Saved Gameplay with authored vehicle keys.");
        }
        else report.AppendLine("Gameplay already had unsaved edits; keys added to the open scene. Save Gameplay to persist them with those edits.");
        Directory.CreateDirectory("output/VehicleKeys");File.WriteAllText("output/VehicleKeys/scene-placement.txt",report.ToString());
        Debug.Log(report.ToString(),group);SceneView.RepaintAll();
    }
    private static void CleanupUnusedKeyBows(Scene scene,System.Text.StringBuilder report)
    {
        var referenced=new HashSet<UnityEngine.Object>();
        foreach(var behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(behaviour==null || behaviour.gameObject.scene!=scene)continue;
            var data=new SerializedObject(behaviour);var iterator=data.GetIterator();
            while(iterator.Next(true))
                if(iterator.propertyType==SerializedPropertyType.ObjectReference && iterator.propertyPath!="m_GameObject" && iterator.objectReferenceValue!=null)
                    referenced.Add(iterator.objectReferenceValue);
        }
        int removed=0,kept=0;
        foreach(var node in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(node==null || node.gameObject.scene!=scene || !node.name.Equals("Key bow",StringComparison.OrdinalIgnoreCase))continue;
            bool used=referenced.Contains(node) || referenced.Contains(node.gameObject) || node.GetComponentInParent<Animator>()!=null || node.GetComponentInParent<Animation>()!=null;
            foreach(var filter in node.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh!=null)used=true;
            foreach(var skin in node.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(skin.sharedMesh!=null)used=true;
            if(node.GetComponentsInChildren<Collider>(true).Length>0 || node.GetComponentsInChildren<MonoBehaviour>(true).Length>0 || node.childCount>0)used=true;
            if(used){kept++;continue;}
            Undo.DestroyObjectImmediate(node.gameObject);removed++;
        }
        report.AppendLine($"Key bow audit: removed {removed} unused empty objects; retained {kept} geometry/anchors/referenced key parts.");
    }
}
