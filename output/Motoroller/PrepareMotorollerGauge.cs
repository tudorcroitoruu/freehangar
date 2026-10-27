using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using System;using System.Linq;using System.Collections.Generic;using System.IO;
public static class PrepareMotorollerGauge {
 const string Dir="Assets/Models/motorcycle-old/Optimized/";
 [Serializable]class Lettering{public Vector3[] vertices;public int[] triangles;}
 static Mesh SaveMesh(Mesh mesh,string name){mesh.name=name;mesh.RecalculateBounds();mesh.RecalculateNormals();var path=Dir+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);return old;}AssetDatabase.CreateAsset(mesh,path);return mesh;}
 static Material Material(string name,Color color){var path=Dir+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("HDRP/Unlit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_UnlitColor",color);m.SetFloat("_CullMode",0);m.SetFloat("_CullModeForward",0);m.SetFloat("_DoubleSidedEnable",1);m.EnableKeyword("_DOUBLESIDED_ON");EditorUtility.SetDirty(m);return m;}
 static Transform Part(string name,Transform parent,Mesh mesh,Material mat,float z=0){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.forward*z;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go.transform;}
 static Mesh Disk(float radius){var v=new Vector3[65];var t=new int[64*3];for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;v[i+1]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);t[i*3]=0;t[i*3+1]=(i+1)%64+1;t[i*3+2]=i+1;}return new Mesh{vertices=v,triangles=t};}
 public static string Main(){if(Application.isPlaying)throw new Exception("Edit Mode required");
 var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"Motoroller_Rigged.fbx"));source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,180,0));
 var root=PrefabUtility.LoadPrefabContents(Dir+"Motoroller_Rideable.prefab");
 try{var rig=root.GetComponent<BicycleController>();var motor=root.GetComponent<MotorollerController>();
 foreach(string name in new[]{"SteeringAssembly","LeftMirror","RightMirror"}){
 var mf=source.GetComponentsInChildren<MeshFilter>().First(m=>m.name==name);var matrix=mf.transform.localToWorldMatrix;var origin=mf.transform.position;var mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);var v=mesh.vertices;for(int i=0;i<v.Length;i++)v[i]=matrix.MultiplyPoint3x4(v[i])-origin;mesh.vertices=v;var n=mesh.normals;for(int i=0;i<n.Length;i++)n[i]=matrix.inverse.transpose.MultiplyVector(n[i]).normalized;mesh.normals=n;mesh.RecalculateBounds();mesh.RecalculateTangents();var path=Dir+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
 var part=rig.steering.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name==name);
 if(part==null){part=new GameObject(name).transform;part.SetParent(root.transform,false);part.localPosition=origin;part.SetParent(rig.steering,true);part.gameObject.AddComponent<MeshFilter>();part.gameObject.AddComponent<MeshRenderer>();}
 part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/motorcycle-old/source/Materials/map_BodyM05_BaseColor.mat");
 }
 var previous=root.GetComponentInChildren<MotorollerSpeedometer>();if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
 var gauge=new GameObject("Speedometer - km per hour").transform;gauge.SetParent(root.transform,false);var outward=new Vector3(0,.9063078f,-.4226183f);gauge.localPosition=new Vector3(0,1.03562f,.1659225f)+outward*.0025f;gauge.localRotation=Quaternion.LookRotation(-outward,new Vector3(0,.4226183f,.9063078f));gauge.SetParent(rig.steering,true);
 var ivory=Material("Gauge ivory",new Color(.88f,.84f,.68f));var dark=Material("Gauge face",new Color(.012f,.016f,.019f));var red=Material("Gauge needle",new Color(.95f,.19f,.065f));
 Part("Dial rim",gauge,SaveMesh(Disk(.038f),"GaugeRim"),ivory);
 Part("Blank dial face",gauge,SaveMesh(Disk(.0365f),"GaugeFace"),dark,-.0002f);
 var verts=new List<Vector3>();var tris=new List<int>();
 for(int i=0;i<=30;i++){float a=(135-i*9)*Mathf.Deg2Rad;Vector3 axis=new Vector3(-Mathf.Sin(a),Mathf.Cos(a),0),side=new Vector3(axis.y,-axis.x,0)*.0003f;float inner=i%5==0?.029f:.032f;int b=verts.Count;verts.Add(axis*inner-side);verts.Add(axis*.035f-side);verts.Add(axis*.035f+side);verts.Add(axis*inner+side);tris.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});}
 var letters=JsonUtility.FromJson<Lettering>(File.ReadAllText("output/Motoroller/dial-lettering.json"));int start=verts.Count;verts.AddRange(letters.vertices);tris.AddRange(letters.triangles.Select(i=>i+start));
 Part("Scale 0 to 60 kmh",gauge,SaveMesh(new Mesh{vertices=verts.ToArray(),triangles=tris.ToArray()},"GaugeMarkings"),ivory,-.00045f);
 var needleMesh=new Mesh{vertices=new[]{new Vector3(-.0013f,-.004f,0),new Vector3(.0013f,-.004f,0),new Vector3(.0006f,.026f,0),new Vector3(0,.030f,0),new Vector3(-.0006f,.026f,0)},triangles=new[]{0,2,1,0,4,2,4,3,2}};
 var needle=Part("Moving speed needle",gauge,SaveMesh(needleMesh,"SpeedometerNeedle"),red,-.0008f);
 Part("Needle center",gauge,SaveMesh(Disk(.0028f),"GaugeHub"),ivory,-.001f);
 gauge.gameObject.AddComponent<MotorollerSpeedometer>().Configure(motor,needle);
 PrefabUtility.SaveAsPrefabAsset(root,Dir+"Motoroller_Rideable.prefab");AssetDatabase.SaveAssets();
 }finally{PrefabUtility.UnloadPrefabContents(root);UnityEngine.Object.DestroyImmediate(source);}
 var sceneMotor=UnityEngine.Object.FindFirstObjectByType<MotorollerController>();Selection.activeGameObject=sceneMotor.gameObject;EditorSceneManager.MarkSceneDirty(sceneMotor.gameObject.scene);EditorSceneManager.SaveScene(sceneMotor.gameObject.scene);return "Separate mirror meshes and working speedometer saved to prefab and scene";
 }
}

