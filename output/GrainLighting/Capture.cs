var pile=UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsSortMode.None).First(p=>p.name.StartsWith("Corn pile"));
var go=new GameObject("Temporary grain lighting capture"){hideFlags=HideFlags.HideAndDontSave};
var camera=go.AddComponent<Camera>();camera.enabled=false;go.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();camera.fieldOfView=65;
go.transform.position=new Vector3(8,5,8);go.transform.LookAt(new Vector3(5,1.2f,2));
var rt=RenderTexture.GetTemporary(1000,700,24);var previous=RenderTexture.active;var tex=new Texture2D(1000,700,TextureFormat.RGB24,false);
try {camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1000,700),0,0);tex.Apply();System.IO.File.WriteAllBytes("output/GrainLighting/After.png",tex.EncodeToPNG());}
finally {camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);}
return "Captured corn pile";

