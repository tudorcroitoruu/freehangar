using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System;
public static class SetupShovelPoses {
 public static object Run(){if(EditorApplication.isPlaying)throw new Exception("Edit mode required");var levels=UnityEngine.Object.FindFirstObjectByType<HeldGrainToolLevels>(FindObjectsInactive.Include);var tool=levels.GetComponent<ShovelTool>();var serialized=new SerializedObject(tool);var template=(ShovelGrainPhysics)serialized.FindProperty("grainPhysics").objectReferenceValue;
 var first=levels.levels[0].GetComponent<HeldShovelPose>()??levels.levels[0].AddComponent<HeldShovelPose>();first.bladeTip=(Transform)serialized.FindProperty("bladeTip").objectReferenceValue;first.gripAnchor=(Transform)serialized.FindProperty("gripAnchor").objectReferenceValue;first.grainPhysics=template;EditorUtility.SetDirty(first);
 for(int i=1;i<=3;i++){
 var go=levels.levels[i];var pose=go.GetComponent<HeldShovelPose>()??go.AddComponent<HeldShovelPose>();float scale=go.transform.lossyScale.x;float f=i==1?1:1.55f;
 pose.bladeTip=Anchor(go.transform,"Shovel contact point",new Vector3(-.204f,0,-.68f)*f/scale);
 pose.gripAnchor=Anchor(go.transform,"Shovel grip anchor",new Vector3(0,0,.49f)*f/scale);
 var frame=Anchor(go.transform,"Simulated grain in bowl",new Vector3(-.115f,0,-.50f)*f/scale);frame.localRotation=Quaternion.LookRotation(new Vector3(-.16f,0,-.29f),Vector3.left);frame.localScale=new Vector3(i==1?.82f:i==2?1.68f:1.94f, f, .85f*f)/scale;
 var physics=frame.GetComponent<ShovelGrainPhysics>()??frame.gameObject.AddComponent<ShovelGrainPhysics>();EditorUtility.CopySerialized(template,physics);pose.grainPhysics=physics;
 var mesh=go.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;for(int v=0;v<vertices.Length;v++)vertices[v]=frame.InverseTransformPoint(go.transform.TransformPoint(vertices[v]));var triangles=mesh.triangles;var floor=new float[289];
 for(int z=0;z<17;z++)for(int x=0;x<17;x++){
 float px=Mathf.Lerp(-.2f,.2f,x/16f),pz=Mathf.Lerp(-.18f,.19f,z/16f),height=float.NegativeInfinity;
 for(int t=0;t<triangles.Length;t+=3){var a=vertices[triangles[t]];var b=vertices[triangles[t+1]];var c=vertices[triangles[t+2]];float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<1e-10f)continue;float u=((b.z-c.z)*(px-c.x)+(c.x-b.x)*(pz-c.z))/det;float v=((c.z-a.z)*(px-c.x)+(a.x-c.x)*(pz-c.z))/det;if(u>=0&&v>=0&&u+v<=1)height=Mathf.Max(height,u*a.y+v*b.y+(1-u-v)*c.y);}
 floor[z*17+x]=float.IsNegativeInfinity(height)?0:height;
 }
 var so=new SerializedObject(physics);var array=so.FindProperty("bowlFloor");array.arraySize=289;for(int j=0;j<289;j++)array.GetArrayElementAtIndex(j).floatValue=floor[j];so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(physics);EditorUtility.SetDirty(pose);
 }
 EditorSceneManager.MarkSceneDirty(tool.gameObject.scene);EditorSceneManager.SaveOpenScenes();return "Configured LVL_1–4 profiles, per-model anchors and fitted grain floors";
 }
 static Transform Anchor(Transform parent,string name,Vector3 position){var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}t.gameObject.layer=parent.gameObject.layer;t.localPosition=position;return t;}
}


