using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
public static class SetupBicycle
{
 public static object Run()
 {
  if(EditorApplication.isPlaying)throw new System.Exception("Stop play mode before setup");
  AssetDatabase.Refresh();
  var old=GameObject.Find("bicycle");
  var oldBounds=old.GetComponentInChildren<Renderer>().bounds;
  var root=new GameObject("Bicycle - red rideable");root.transform.position=new Vector3(oldBounds.center.x,oldBounds.min.y+.51f,oldBounds.center.z);
  // The imported scene bike was tipped and partly underground; place new bike on the terrain.
  var terrain=Terrain.activeTerrains.FirstOrDefault(t=>root.transform.position.x>=t.transform.position.x && root.transform.position.x<=t.transform.position.x+t.terrainData.size.x && root.transform.position.z>=t.transform.position.z && root.transform.position.z<=t.transform.position.z+t.terrainData.size.z);
  if(terrain!=null)root.transform.position=new Vector3(root.transform.position.x,terrain.SampleHeight(root.transform.position)+terrain.transform.position.y+.51f,root.transform.position.z);
  var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/bicycle/Optimized/Bicycle_Red.fbx"));model.transform.SetParent(root.transform,false);
  var bike=root.AddComponent<BicycleController>();
  System.Func<string,Transform> part=n=>model.GetComponentsInChildren<Transform>().First(t=>t.name==n);
  bike.steering=part("Steering");bike.frontWheel=part("FrontWheel");bike.rearWheel=part("RearWheel");bike.crank=part("Crank");bike.leftPedal=part("LeftPedal");bike.rightPedal=part("RightPedal");
  // Import with baked axis conversion for predictable +Z forward mesh space.
  var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/Models/bicycle/Optimized/Bicycle_Red.fbx");
  // Materials use existing baked detail maps and a newly generated red frame albedo.
  foreach(var renderer in model.GetComponentsInChildren<Renderer>())
  {
   var mats=renderer.sharedMaterials;
   for(int i=0;i<mats.Length;i++)
   {
    string n=mats[i].name;string path="Assets/Models/bicycle/Optimized/"+n+".mat";
    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
    if(mat==null)
    {
     mat=new Material(Shader.Find("HDRP/Lit"));mat.name=n;
     string texname=n=="frame"?"Assets/Models/bicycle/Optimized/Bicycle_Red_BaseColor.png":"Assets/Models/bicycle/texture/bicycle_"+n+"_BaseColor.png";
     mat.SetTexture("_BaseColorMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texname));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",n=="frame"?.45f:.25f);mat.SetFloat("_Metallic",n=="frame"?.5f:0);
     AssetDatabase.CreateAsset(mat,path);
    }
    mats[i]=mat;
   }
   renderer.sharedMaterials=mats;
  }
  var motor=root.GetComponent<CharacterController>();motor.height=1.25f;motor.radius=.28f;motor.center=new Vector3(0,.12f,0);motor.stepOffset=.18f;motor.slopeLimit=45;
  Transform Anchor(string n,Vector3 pos){var a=new GameObject(n).transform;a.SetParent(root.transform,false);a.localPosition=pos;return a;}
  bike.riderView=Anchor("First person rider view",new Vector3(0,1.05f,-.03f));
  bike.cargoSlots=new[]{Anchor("Rear cargo left",new Vector3(-.21f,.39f,-.58f)),Anchor("Rear cargo right",new Vector3(.21f,.39f,-.58f))};
  var rack=GameObject.CreatePrimitive(PrimitiveType.Cube);rack.name="Rear cargo rack";rack.transform.SetParent(root.transform,false);rack.transform.localPosition=new Vector3(0,.17f,-.56f);rack.transform.localScale=new Vector3(.54f,.04f,.5f);Object.DestroyImmediate(rack.GetComponent<Collider>());rack.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/bicycle/Optimized/detail.mat");
  var player=Object.FindFirstObjectByType<PlayerController>();var interaction=player.GetComponent<PlayerBicycleInteraction>()??player.gameObject.AddComponent<PlayerBicycleInteraction>();interaction.player=player;interaction.bicycle=bike;
  var so=new SerializedObject(player);interaction.instrument=so.FindProperty("heldInstrument").objectReferenceValue as Transform;
  var crate=GameObject.CreatePrimitive(PrimitiveType.Cube);crate.name="Bicycle cargo box";crate.transform.position=root.transform.position+new Vector3(.85f,-.25f,-.6f);crate.transform.localScale=new Vector3(.32f,.40f,.36f);crate.AddComponent<Rigidbody>().mass=3;crate.AddComponent<BicycleCargoBox>();
  var wood=AssetDatabase.FindAssets("t:Material").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.Contains("Shop")&&p.EndsWith(".mat"));if(wood!=null)crate.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(wood);
  old.SetActive(false);EditorUtility.SetDirty(player);EditorUtility.SetDirty(interaction);
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(root.scene);
  return new {triangles=model.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3),bounds=model.GetComponentsInChildren<Renderer>().Select(r=>r.bounds.ToString()).ToArray(),front=bike.frontWheel.position,rear=bike.rearWheel.position};
 }
}
