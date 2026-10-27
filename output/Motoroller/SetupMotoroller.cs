using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System.Linq;
public static class SetupMotoroller {
 const string Dir="Assets/Models/motorcycle-old/Optimized/";
 public static string Main(){
 if(EditorApplication.isPlaying)throw new System.Exception("Leave Play Mode before setup");
 var old=GameObject.Find("Motoroller_001");if(old==null)throw new System.Exception("Original scooter not found");
 if(GameObject.Find("Motoroller - rideable")!=null)throw new System.Exception("Rideable scooter already exists");
 var root=new GameObject("Motoroller - rideable");root.SetActive(false);Undo.RegisterCreatedObjectUndo(root,"Prepare Motoroller");
 root.transform.SetParent(old.transform.parent);root.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation*Quaternion.Euler(0,180,0));
 var terrain=Terrain.activeTerrains.FirstOrDefault(t=>root.transform.position.x>=t.transform.position.x&&root.transform.position.x<=t.transform.position.x+t.terrainData.size.x&&root.transform.position.z>=t.transform.position.z&&root.transform.position.z<=t.transform.position.z+t.terrainData.size.z);
 if(terrain!=null)root.transform.position=new Vector3(root.transform.position.x,terrain.SampleHeight(root.transform.position)+terrain.transform.position.y+.02f,root.transform.position.z);
 var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Motoroller_Rigged.fbx"));source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,180,0));
 Transform Part(string n){var mf=source.GetComponentsInChildren<MeshFilter>().First(f=>f.name==n);var matrix=mf.transform.localToWorldMatrix;var pivot=mf.transform.position;var mesh=Object.Instantiate(mf.sharedMesh);mesh.name="Motoroller_"+n;
 var v=mesh.vertices;for(int i=0;i<v.Length;i++)v[i]=matrix.MultiplyPoint3x4(v[i])-pivot;mesh.vertices=v;
 var normals=mesh.normals;for(int i=0;i<normals.Length;i++)normals[i]=matrix.inverse.transpose.MultiplyVector(normals[i]).normalized;mesh.normals=normals;
 mesh.RecalculateBounds();mesh.RecalculateTangents();MeshUtility.Optimize(mesh);AssetDatabase.CreateAsset(mesh,Dir+n+".asset");
 var part=new GameObject(n);part.transform.SetParent(root.transform,false);part.transform.localPosition=pivot;part.AddComponent<MeshFilter>().sharedMesh=mesh;
 var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterials=mf.GetComponent<Renderer>().sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/motorcycle-old/source/Materials/"+(m.name.Contains("Wheels")?"map_WheelsM05_BaseColor.mat":"map_BodyM05_BaseColor.mat"))).ToArray();return part.transform;}
 var body=Part("Body");var assembly=Part("SteeringAssembly");var front=Part("FrontWheel");var rear=Part("RearWheel");Object.DestroyImmediate(source);
 Transform Anchor(string n,Vector3 p){var a=new GameObject(n).transform;a.SetParent(root.transform,false);a.localPosition=p;return a;}
 var pivot=Anchor("Steering pivot - inclined headset",assembly.localPosition);pivot.localRotation=Quaternion.FromToRotation(Vector3.up,new Vector3(0,.903f,-.43f).normalized);assembly.SetParent(pivot,true);front.SetParent(pivot,true);
 // Correct attachment placement: part coordinates were authored in root space.
 var rig=root.AddComponent<BicycleController>();rig.motorDriven=true;rig.steering=pivot;rig.frontWheel=front;rig.rearWheel=rear;rig.maximumSteeringAngle=30;rig.steeringSmoothTime=.16f;
 rig.riderView=Anchor("Rider view",new Vector3(0,1.48f,-.15f));rig.SetRidingCamera(Vector3.zero,8);
 rig.cargoSlots=new[]{Anchor("Rear cargo mount",new Vector3(0,.68f,-.64f))};
 var controller=root.AddComponent<MotorollerController>();controller.Configure();
 var motor=root.GetComponent<CharacterController>();motor.height=1.3f;motor.radius=.34f;motor.center=new Vector3(0,.68f,0);motor.stepOffset=.15f;motor.slopeLimit=40;motor.skinWidth=.025f;
 // Interaction coverage follows the full silhouette; disabled by the shared rig while riding.
 var box=root.AddComponent<BoxCollider>();box.center=new Vector3(0,.65f,0);box.size=new Vector3(.7f,1.3f,1.94f);
 Undo.RecordObject(old,"Replace scooter with prepared rig");old.SetActive(false);root.SetActive(true);
 var player=Object.FindFirstObjectByType<PlayerController>();var interaction=player.GetComponent<PlayerBicycleInteraction>()??player.gameObject.AddComponent<PlayerBicycleInteraction>();interaction.player=player;
 if(interaction.instrument==null){using(var so=new SerializedObject(player))interaction.instrument=so.FindProperty("heldInstrument").objectReferenceValue as Transform;}
 EditorUtility.SetDirty(interaction);
 PrefabUtility.SaveAsPrefabAssetAndConnect(root,Dir+"Motoroller_Rideable.prefab",InteractionMode.AutomatedAction);
 AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.scene);EditorSceneManager.SaveScene(root.scene);Selection.activeGameObject=root;
 return "Motoroller prefab and scene saved; triangles="+root.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)+"; front="+root.transform.InverseTransformPoint(front.position)+"; rear="+root.transform.InverseTransformPoint(rear.position);
 }
}
