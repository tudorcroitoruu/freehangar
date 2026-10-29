using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.Linq;
public static class FinishUtilityRoom {
static Transform root; static Material metal;
static Transform Box(string n,Transform p,Vector3 pos,Vector3 size,Material mat){
 var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;return g.transform;
}
static Material Mat(string name, string source,Color color,float smooth){
 var path="Assets/LaunchRoom/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
 if(m==null){m=new Material(AssetDatabase.LoadAssetAtPath<Material>(source));AssetDatabase.CreateAsset(m,path);}
 m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
}
public static object Run(){
 if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
 var s=UnityEngine.Object.FindFirstObjectByType<HangarLaunchSequence>();root=s.transform;
 if(root.Find("Utility room finishing")!=null)throw new Exception("Already applied");
 var detail=new GameObject("Utility room finishing").transform;detail.SetParent(root,false);
 metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/LaunchRoom/Graphite steel.mat");
 foreach(var t in root.GetComponentsInChildren<TextMesh>(true).Where(t=>t.text.Contains("ELECTRICAL")||t.text.Contains("SERVICE ACCESS")||t.text.Contains("MAIN POWER")).ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
 s.hatchOpenAngle=90;
 // Narrow front portal with a wider recessed swing pocket behind it.
 foreach(string side in new[]{"left","right"}){
  float sign=side=="left"?-1:1;var t=root.Find("Concealed bay "+side);
  var mat=t.GetComponent<Renderer>().sharedMaterial;
  t.localPosition=new Vector3(sign*3.85f,6,11.05f);t.localScale=new Vector3(6.3f,12,.6f);
  Box("Recessed hatch pocket "+side,detail,new Vector3(sign*4.2f,6,12.55f),new Vector3(5.6f,12,2.4f),mat);
 }
 var steel=Mat("Brushed blue utility steel","Assets/NewMap/FiveHangars/Galvanized door steel.mat",new Color(.3f,.39f,.44f),.32f);
 var entry=s.roomDoor.GetComponentInChildren<LaunchRoomInteractable>().transform;
 entry.GetComponent<Renderer>().sharedMaterial=steel;s.revealDoor.GetComponent<Renderer>().sharedMaterial=steel;
 s.rocketDoor.Find("Entrance Door").GetComponent<Renderer>().sharedMaterial=steel;
 // Face-mounted inset panels and reinforcing edges follow each door.
 foreach(var door in new[]{entry,s.revealDoor}){
  var panel=Box("Recessed steel panel",door,new Vector3(0,0,-.54f),new Vector3(.82f,.83f,.07f),metal);
  for(int i=0;i<6;i++)Box("Vent slat",door,new Vector3(0,-.24f+i*.025f,-.60f),new Vector3(.46f,.009f,.04f),steel);
  foreach(float x in new[]{-.44f,.44f})Box("Door edge rail",door,new Vector3(x,0,-.58f),new Vector3(.025f,.94f,.06f),metal);
 }
 // Replace sandy ground with muted industrial floor slabs and dark joints.
 var floor=Mat("Grey workshop floor","Assets/NewMap/Materials/Warm dusty concrete.mat",new Color(.25f,.29f,.30f),.08f);
 root.Find("Concrete base").GetComponent<Renderer>().sharedMaterial=metal;
 for(int x=0;x<7;x++)for(int z=0;z<10;z++)Box("Concrete floor slab",detail,new Vector3(-6+x*2,.012f,1+z*2),new Vector3(1.975f,.035f,1.975f),floor);
 // Use selected individual pieces instead of the miniature pack demonstration layout.
 var pack=root.Find("Electrical cabinet");
 string[] names={"SM_ElectricalBox_Large_A.mo","SM_ElectricalBox_Large_B.mo","SM_WallCounters_A.mo","SM_ElectricalBox_Group_A.mo","SM_ElectricBox_C.mo"};
 for(int i=0;i<names.Length;i++){
  var source=pack.GetComponentsInChildren<Transform>(true).First(t=>t.name==names[i]);
  var obj=UnityEngine.Object.Instantiate(source.gameObject,detail);obj.name="Mounted "+names[i];obj.SetActive(true);
  obj.transform.localPosition=Vector3.zero;obj.transform.localRotation=Quaternion.Euler(0,-90,0);obj.transform.localScale=Vector3.one;
  var renderers=obj.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var rr in renderers)bounds.Encapsulate(rr.bounds);
  obj.transform.localScale*= (i<2?1.9f:1.05f)/bounds.size.y;
  bounds=renderers[0].bounds;foreach(var rr in renderers)bounds.Encapsulate(rr.bounds);
  var center=root.InverseTransformPoint(bounds.center);obj.transform.localPosition+=new Vector3(6.6f,i<2?1.35f:2.55f,3.8f+i*1.45f)-center;
 }
 pack.gameObject.SetActive(false);
 foreach(var t in root.Cast<Transform>().Where(t=>t.name.StartsWith("Conduit ")).ToArray())t.gameObject.SetActive(false);
 for(int i=0;i<3;i++){
  float y=3.45f+i*.13f;Box("Wall mounted conduit",detail,new Vector3(6.85f,y,5.1f),new Vector3(.045f,.045f,7.8f),metal);
 }
 for(int i=0;i<5;i++){
  float z=2+i*1.45f;Box("Electrical conduit drop",detail,new Vector3(6.85f,2.7f,z),new Vector3(.045f,1.5f,.045f),metal);
  Box("Conduit wall bracket",detail,new Vector3(6.88f,3.6f,z),new Vector3(.12f,.49f,.09f),steel);
 }
 // Matching hangar pendant, suspended from the roof instead of a floating box.
 root.Find("Hangar style lamp housing").gameObject.SetActive(false);root.Find("Lamp diffuser").gameObject.SetActive(false);
 var pendant=UnityEngine.Object.Instantiate(GameObject.Find("Reference pendant lamps").transform.GetChild(0).gameObject,detail);
 pendant.name="Hangar type pendant";pendant.transform.localPosition=new Vector3(0,5.6f,5);pendant.transform.localRotation=Quaternion.identity;pendant.transform.localScale=Vector3.one;
 var cable=pendant.transform.Find("Suspension cable");cable.localPosition=new Vector3(0,3.5f,0);cable.localScale=new Vector3(.03f,5.7f,.03f);
 foreach(var light in pendant.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light.gameObject);
 s.lampGlow=pendant.transform.Find("Warm luminous glass underside").GetComponent<Renderer>();s.lampGlow.enabled=false;
 s.roomLight.transform.localPosition=new Vector3(0,5.35f,5);s.roomLight.enabled=false;
 // Low service bench beside transformer and insulated floor mat at controls.
 var timber=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/TallHangar/Rich oak framing.mat");
 Box("Maintenance bench top",detail,new Vector3(-5.7f,.95f,6.5f),new Vector3(1.5f,.14f,3),timber);
 foreach(float x in new[]{-6.25f,-5.15f})foreach(float z in new[]{5.3f,7.7f})Box("Bench steel leg",detail,new Vector3(x,.46f,z),new Vector3(.09f,.92f,.09f),metal);
 Box("Insulating switch mat",detail,new Vector3(5.6f,.055f,2.4f),new Vector3(1.5f,.035f,2.6f),metal);
 EditorUtility.SetDirty(s);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveOpenScenes();
 return "Finished: 90 degree recessed hatch, unlabelled detailed steel doors, grey slab floor, full-size equipment, wall conduits and matching pendant.";
}
}
