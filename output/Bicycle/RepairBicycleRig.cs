using System;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class RepairBicycleRig {
 public static object Run(){
 if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");
 AssetDatabase.Refresh();var bike=UnityEngine.Object.FindFirstObjectByType<BicycleController>();var model=bike.frontWheel.parent;
 if(model.name=="Headset steering pivot")throw new Exception("Rig already installed");
 var imported=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/bicycle/Optimized/Bicycle_Rigged.fbx");
 var filters=imported.GetComponentsInChildren<MeshFilter>();
 if(PrefabUtility.IsPartOfPrefabInstance(model.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(model.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
 foreach(var filter in filters)
 {
  if(filter.name=="RearControlCable")continue;
  var target=model.GetComponentsInChildren<MeshFilter>().First(f=>f.name==filter.name);
  target.sharedMesh=filter.sharedMesh;
  target.GetComponent<MeshRenderer>().sharedMaterials=filter.GetComponent<MeshRenderer>().sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/bicycle/Optimized/"+m.name+".mat")).ToArray();
 }
 var pivot=new GameObject("Headset steering pivot").transform;pivot.SetParent(bike.transform,false);
 pivot.localPosition=new Vector3(.00185097f,.34900263f,.3051303f);
 pivot.localRotation=Quaternion.FromToRotation(Vector3.up,new Vector3(0,.9340211538f,-.3572176944f));
 bike.steering.SetParent(pivot,true);bike.frontWheel.SetParent(pivot,true);bike.steering=pivot;
 var source=filters.First(f=>f.name=="RearControlCable");var cable=new GameObject("Flexible rear control cable");cable.transform.SetParent(bike.transform,false);
 var mesh=UnityEngine.Object.Instantiate(source.sharedMesh);mesh.name="Rear control cable - steering skin";
 var vertices=mesh.vertices;var weights=new BoneWeight[vertices.Length];
 for(int i=0;i<vertices.Length;i++)
 {
  float w=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.27f,.41f,vertices[i].y));
  weights[i]=new BoneWeight{boneIndex0=0,weight0=1-w,boneIndex1=1,weight1=w};
 }
 mesh.boneWeights=weights;mesh.bindposes=new[]{bike.transform.worldToLocalMatrix*cable.transform.localToWorldMatrix,pivot.worldToLocalMatrix*cable.transform.localToWorldMatrix};
 AssetDatabase.CreateAsset(mesh,"Assets/Models/bicycle/Optimized/RearControlCableRig.asset");
 var skin=cable.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=new[]{bike.transform,pivot};skin.rootBone=bike.transform;skin.quality=SkinQuality.Bone2;
 var bounds=mesh.bounds;bounds.Expand(.4f);skin.localBounds=bounds;
 skin.sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/bicycle/Optimized/"+m.name+".mat")).ToArray();
 var data=new SerializedObject(bike);data.FindProperty("pedalingPitchAmplitude").floatValue=.3f;data.ApplyModifiedProperties();
 EditorUtility.SetDirty(bike);AssetDatabase.SaveAssets();PrefabUtility.SaveAsPrefabAsset(bike.gameObject,"Assets/Models/bicycle/Optimized/Bicycle_Rideable.prefab");EditorSceneManager.SaveScene(bike.gameObject.scene);
 return new{steeringAxis=pivot.up,wheelChild=bike.frontWheel.IsChildOf(pivot),triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3),cableVertices=mesh.vertexCount};
 }
}
