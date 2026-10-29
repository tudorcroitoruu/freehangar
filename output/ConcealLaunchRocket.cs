using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System;
public static class ConcealLaunchRocket {
 const string Folder="Assets/LaunchRoom/";
 public static object Run(){if(EditorApplication.isPlaying)throw new Exception("Edit mode required");var s=UnityEngine.Object.FindFirstObjectByType<HangarLaunchSequence>();var root=s.transform;
 var wood=CopyMat("Correct scale utility boards","Assets/NewMap/TallHangar/Weathered grey plank walls.mat");var steel=CopyMat("Utility door painted steel","Assets/NewMap/FiveHangars/Galvanized door steel.mat");var concrete=CopyMat("Utility concrete tiles","Assets/NewMap/Materials/Warm dusty concrete.mat");wood.SetFloat("_NormalScale",.22f);wood.SetFloat("_Smoothness",.12f);steel.SetFloat("_NormalScale",.25f);steel.SetFloat("_Smoothness",.24f);
 // A permanent utility wall hides the hull, fins and nose. Only the hatch is exposed.
 Add(root,"Concealed bay left",new Vector3(-3.85f,6,12.25f),new Vector3(6.3f,12,.28f),wood);
 Add(root,"Concealed bay right",new Vector3(3.85f,6,12.25f),new Vector3(6.3f,12,.28f),wood);
 Add(root,"Concealed bay header",new Vector3(0,7.8f,12.25f),new Vector3(1.4f,8.4f,.28f),wood);
 var trim=AssetDatabase.LoadAssetAtPath<Material>("Assets/LaunchRoom/Graphite steel.mat");
 Add(root,"Service hatch left jamb",new Vector3(-.63f,1.8f,12.05f),new Vector3(.12f,3.6f,.4f),trim);Add(root,"Service hatch right jamb",new Vector3(.63f,1.8f,12.05f),new Vector3(.12f,3.6f,.4f),trim);Add(root,"Service hatch lintel",new Vector3(0,3.55f,12.05f),new Vector3(1.38f,.13f,.4f),trim);
 s.revealDoor.localPosition=new Vector3(0,1.8f,11.75f);s.revealDoor.localScale=new Vector3(1.4f,3.6f,.12f);s.revealLift=3.8f;
 foreach(var label in s.revealDoor.GetComponentsInChildren<TextMesh>()){label.text="SERVICE ACCESS";label.characterSize=.045f;label.transform.localScale=new Vector3(1/1.4f,1/3.6f,1/.12f);label.transform.localPosition=new Vector3(0,.15f,-.6f);}
 s.revealView.localPosition=new Vector3(-1.4f,1.65f,7.7f);s.revealView.rotation=Quaternion.LookRotation(root.TransformPoint(new Vector3(0,2.1f,12.3f))-s.revealView.position,root.up);
 s.approachView.localPosition=new Vector3(0,2.25f,11.1f);s.approachView.rotation=Quaternion.LookRotation(root.TransformPoint(new Vector3(0,1.9f,13))-s.approachView.position,root.up);
 var cockpit=new GameObject("Cockpit realization after lock").transform;cockpit.SetParent(root,false);cockpit.SetPositionAndRotation(s.cabinView.position,s.cabinView.rotation);s.cockpitRevealView=cockpit;
 s.cabinView.rotation=Quaternion.LookRotation(root.TransformPoint(new Vector3(0,1.2f,16))-s.cabinView.position,root.up);
 int index=0;foreach(var f in root.GetComponentsInChildren<MeshFilter>(true)){
 var n=f.name;bool wall=n=="Left wall"||n=="Right wall"||n=="Rear wall"||n.StartsWith("Entry ")||n.StartsWith("Exterior cladding")||n.StartsWith("Concealed bay");bool door=n=="Electrical room door"||f.transform==s.revealDoor;bool baseFloor=n=="Concrete base";bool roof=n=="Roof";
 if(!wall&&!door&&!baseFloor&&!roof)continue;
 var m=UnityEngine.Object.Instantiate(f.sharedMesh);var vertices=m.vertices;var normals=m.normals;var uv=new Vector2[vertices.Length];var scale=f.transform.lossyScale;for(int i=0;i<vertices.Length;i++){var p=Vector3.Scale(vertices[i],scale);var nn=normals[i];uv[i]=(Mathf.Abs(nn.y)>.5f?new Vector2(p.x,p.z):Mathf.Abs(nn.x)>.5f?new Vector2(p.z,p.y):new Vector2(p.x,p.y))/2f;}m.uv=uv;m.RecalculateTangents();AssetDatabase.CreateAsset(m,Folder+"Scaled surface "+index+++".asset");f.sharedMesh=m;f.GetComponent<Renderer>().sharedMaterial=baseFloor?concrete:door||roof?steel:wood;EditorUtility.SetDirty(f);
 }
 // Give the closed hatch a subdued metal surface without changing the glass or rim.
 s.rocketDoor.Find("Entrance Door").GetComponent<Renderer>().sharedMaterial=steel;
 EditorUtility.SetDirty(s);EditorUtility.SetDirty(wood);EditorUtility.SetDirty(steel);EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();return "Rocket hidden behind service wall; world-sized texture UVs; cockpit reveal delayed until after lock";
 }
 static Material CopyMat(string name,string path){var m=new Material(AssetDatabase.LoadAssetAtPath<Material>(path));m.name=name;foreach(var texture in new[]{"_BaseColorMap","_NormalMap","_MaskMap"})m.SetTextureScale(texture,Vector2.one);AssetDatabase.CreateAsset(m,Folder+name+".mat");return m;}
 static void Add(Transform parent,string name,Vector3 p,Vector3 scale,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=m;}
}
