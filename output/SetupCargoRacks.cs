using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class SetupCargoRacks
{
 static void Configure(GameObject root)
 {
  var rig=root.GetComponent<BicycleController>(); bool scooter=root.GetComponent<MotorollerController>()!=null;
  var rack=root.GetComponent<VehicleCargoRack>()??root.AddComponent<VehicleCargoRack>();
  var platform=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Rear cargo rack");
  if(platform==null && scooter)
  {
   var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name="Rear cargo rack"; platform=obj.transform;platform.SetParent(root.transform,false);
   platform.localPosition=new Vector3(0,.77f,-.65f);platform.localScale=new Vector3(.46f,.025f,.5f);
   const string path="Assets/NewMap/Materials/Vehicle rack dark metal.mat";
   var material=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(material==null){material=new Material(Shader.Find("HDRP/Lit"));material.SetColor("_BaseColor",new Color(.045f,.05f,.055f));material.SetFloat("_Metallic",.6f);material.SetFloat("_Smoothness",.3f);AssetDatabase.CreateAsset(material,path);}
   obj.GetComponent<Renderer>().sharedMaterial=material;
  }
  if(platform==null)throw new System.Exception("Missing rack mesh: "+root.name);
  var surface=rack.surface;
  if(surface==null){surface=new GameObject("Cargo rack surface").transform;surface.SetParent(platform.parent,false);rack.surface=surface;}
  var mesh=platform.GetComponent<MeshFilter>(); var localTop=mesh.sharedMesh.bounds.center+Vector3.up*mesh.sharedMesh.bounds.extents.y;
  surface.position=platform.TransformPoint(localTop)+root.transform.up*.003f;surface.rotation=root.transform.rotation;surface.localScale=Vector3.one;
  EditorUtility.SetDirty(rack);
 }
 public static object Run()
 {
  if(EditorApplication.isPlaying)throw new System.Exception("Edit mode required");
  foreach(var path in new[]{"Assets/Models/bicycle/Optimized/Bicycle_Rideable.prefab","Assets/Models/motorcycle-old/Optimized/Motoroller_Rideable.prefab"})
  {var root=PrefabUtility.LoadPrefabContents(path);Configure(root);PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);}
  var rigs=Object.FindObjectsByType<BicycleController>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(b=>b.name=="Bicycle - red rideable"||b.name=="Motoroller - rideable").ToArray();
  foreach(var b in rigs)Configure(b.gameObject);
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return rigs.Select(b=>b.name).ToArray();
 }
}
