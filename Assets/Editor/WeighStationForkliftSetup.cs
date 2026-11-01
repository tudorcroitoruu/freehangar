using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WeighStationForkliftSetup
{
    private const string Name="Forklift - Weigh station";
    [InitializeOnLoadMethod] private static void AfterImport()=>EditorApplication.delayCall+=Apply;
    [MenuItem("Tools/Vehicles/Place forklift beside weigh station")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name!="Gameplay")return;
        var forks=Object.FindObjectsByType<ForkliftController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        foreach(var fork in forks)if(fork.name==Name)return;
        if(forks.Length==0)return;
        var zone=Object.FindFirstObjectByType<SellingZone>(FindObjectsInactive.Include);if(zone==null)return;
        var area=zone.GetComponent<BoxCollider>();Bounds bounds=area.bounds;
        Physics.SyncTransforms();
        for(int i=0;i<16;i++)
        {
            float angle=(i%8)*45*Mathf.Deg2Rad;Vector3 outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            float radius=Mathf.Max(bounds.extents.x,bounds.extents.z)+4.5f+(i/8)*3f;
            Vector3 point=bounds.center+outward*radius;
            if(!Physics.Raycast(point+Vector3.up*15,Vector3.down,out var ground,35,~0,QueryTriggerInteraction.Ignore) || ground.normal.y<.85f)continue;
            Quaternion rotation=Quaternion.LookRotation(-outward,Vector3.up);
            var hits=Physics.OverlapBox(ground.point+Vector3.up*1.35f,new Vector3(1.35f,1.2f,2.6f),rotation,~0,QueryTriggerInteraction.Ignore);
            if(hits.Length>0)continue;
            var host=Object.Instantiate(forks[0].gameObject);host.name=Name;Undo.RegisterCreatedObjectUndo(host,"Add weigh station forklift");
            host.transform.SetPositionAndRotation(ground.point+Vector3.up*.12f,rotation);
            EditorSceneManager.MarkSceneDirty(host.scene);
            Directory.CreateDirectory("output/Forklift");File.WriteAllText("output/Forklift/weigh-station.txt",$"PASS second forklift placed at {host.transform.position}, {Vector3.Distance(host.transform.position,bounds.center):F1}m from weigh zone center. Clear footprint verified; faces the scale. Original forklift retained.");
            return;
        }
        Debug.LogError("Could not find a clear parking spot beside the weigh station.");
    }
}
