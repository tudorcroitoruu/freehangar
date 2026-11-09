using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class FixPerimeterBarriers
{
    public static object Run()
    {
        if(Application.isPlaying) throw new InvalidOperationException("Edit mode required.");
        var root=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name=="Continuous map boundary - collision");
        var boxes=root.GetComponentsInChildren<BoxCollider>();
        if(boxes.Length!=4) throw new InvalidOperationException("Expected four map barriers.");
        var before=boxes.Select(c=>new {position=c.transform.position.ToString(),size=c.size.ToString()}).ToArray();
        foreach(var c in boxes)
        {
            Undo.RecordObject(c.transform,"Align perimeter collision with outer fences");
            Undo.RecordObject(c,"Align perimeter collision with outer fences");
            bool side=Mathf.Abs(c.transform.position.x)>100;
            c.transform.position=side ? new Vector3(Mathf.Sign(c.transform.position.x)*164,38,122) : new Vector3(0,38,c.transform.position.z<122 ? -41 : 285);
            c.size=side ? new Vector3(1,100,328) : new Vector3(330,100,1);
            c.enabled=true;
            c.isTrigger=false;
        }
        Physics.SyncTransforms();
        int checks=0;
        foreach(float y in new[]{0f,3f,8f,15f,25f,50f,85f})
        foreach(var c in boxes)
        {
            bool side=Mathf.Abs(c.transform.position.x)>100;
            var normal=side ? Vector3.right*Mathf.Sign(c.transform.position.x) : Vector3.forward*(c.transform.position.z<122 ? -1:1);
            for(int i=0;i<=100;i++)
            {
                float along=Mathf.Lerp(-163.9f,163.9f,i/100f);
                var point=side ? new Vector3(c.transform.position.x,y,122+along*326/328) : new Vector3(along,y,c.transform.position.z);
                if(!c.Raycast(new Ray(point-normal*2,normal),out var hit,4)) throw new Exception("Gap at "+point);
                checks++;
            }
        }
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if(player!=null && boxes.Any(c=>Physics.GetIgnoreLayerCollision(player.gameObject.layer,c.gameObject.layer))) throw new Exception("Player layer ignores barrier.");
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        if(!EditorSceneManager.SaveScene(root.gameObject.scene))throw new Exception("Scene save failed.");
        return new {before,after=boxes.Select(c=>new {position=c.transform.position.ToString(),size=c.size.ToString(),bottom=c.bounds.min.y,top=c.bounds.max.y}).ToArray(),passedRaycasts=checks,scene=root.gameObject.scene.path};
    }
}
