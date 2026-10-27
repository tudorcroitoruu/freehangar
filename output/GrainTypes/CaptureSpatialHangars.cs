using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
public static class CaptureSpatialHangars
{
    public static string Main()
    {
        var go=new GameObject("Temporary spatial grain capture"){hideFlags=HideFlags.HideAndDontSave};
        var camera=go.AddComponent<Camera>();camera.enabled=false;go.AddComponent<HDAdditionalCameraData>();camera.fieldOfView=85;
        var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        try
        {
            foreach(int number in new[]{3,4,5})
            {
                var pile=Object.FindObjectsByType<GrainPile>(FindObjectsSortMode.None).First(p=>p.name.StartsWith("Hangar "+number));
                go.transform.position=pile.transform.TransformPoint(new Vector3(0,7,-pile.Size*.48f));
                var target=pile.transform.TransformPoint(new Vector3(0,2,pile.Size*.12f));go.transform.LookAt(target);
                camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
                System.IO.File.WriteAllBytes("output/GrainTypes/Hangar"+number+"Spatial.png",image.EncodeToPNG());
                if(number==5)
                {
                    UnityEditor.Selection.activeGameObject=pile.gameObject;
                    if(UnityEditor.SceneView.lastActiveSceneView!=null)UnityEditor.SceneView.lastActiveSceneView.AlignViewToObject(go.transform);
                }
            }
            return "Captured actual hangars 3, 4 and 5 with spatial grain layouts";
        }
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);}
    }
}
