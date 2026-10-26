using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using UnityEngine.Rendering.HighDefinition;using Object=UnityEngine.Object;
public static class ExpandFarm {
 const string A="Assets/NewMap/FiveHangars";
 static Transform root,group;static Terrain terrain;static TerrainData td;static Dictionary<string,string> paths;
 static Vector3[] centers={Vector3.zero,new Vector3(47,0,-9),new Vector3(-47,0,-9),new Vector3(-83,0,43),new Vector3(83,0,43)};
 static float[] yaws={0,-60,60,90,-90};static Vector3[] scales={Vector3.one,new Vector3(1.3f,1.05f,1.4f),new Vector3(1.3f,1.05f,1.4f),new Vector3(1.65f,1.2f,1.85f),new Vector3(1.65f,1.2f,1.85f)};
 static void Init(){root=GameObject.Find("New_Map").transform;terrain=root.GetComponentInChildren<Terrain>();td=terrain.terrainData;group=root.Find("11_Five_Hangar_Farm");paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Imported/Toon Series/Toon Farm Pack/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).GroupBy(Path.GetFileNameWithoutExtension).ToDictionary(g=>g.Key,g=>g.First());}
 static Transform Group(string name,Transform parent){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
 static float Height(Vector3 p)=>terrain.SampleHeight(p)+terrain.transform.position.y;
 static GameObject Prefab(string name,Transform parent,Vector3 p,float yaw=0,float scale=1){var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[name]),parent);g.transform.SetPositionAndRotation(new Vector3(p.x,Height(p),p.z),Quaternion.Euler(0,yaw,0));g.transform.localScale=Vector3.one*scale;return g;}
 static Material Mat(string name,Color color){var m=new Material(Shader.Find("HDRP/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);m.enableInstancing=true;HDMaterial.ValidateMaterial(m);AssetDatabase.CreateAsset(m,A+"/"+name+".mat");return m;}
 static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,bool collision=true){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
 static void Set(UnityEngine.Object target,string name,Action<SerializedProperty> set){var so=new SerializedObject(target);set(so.FindProperty(name));so.ApplyModifiedPropertiesWithoutUndo();}
 static Vector3 Local(int i,Vector3 p)=>Quaternion.Inverse(Quaternion.Euler(0,yaws[i],0))*(p-centers[i]);
 static float RectDistance(Vector3 p,int i,float margin=0){var q=Local(i,p);return Mathf.Max(Mathf.Abs(q.x)-(10.5f*scales[i].x+margin),Mathf.Abs(q.z)-(15.8f*scales[i].z+margin));}
 static float Segment(Vector3 p,Vector3 a,Vector3 b){var q=new Vector2(p.x-a.x,p.z-a.z);var d=new Vector2(b.x-a.x,b.z-a.z);return(q-d*Mathf.Clamp01(Vector2.Dot(q,d)/d.sqrMagnitude)).magnitude;}
 static Vector3 Entry(int i)=>centers[i]+Quaternion.Euler(0,yaws[i],0)*new Vector3(0,0,17*scales[i].z);
 static bool Clear(Vector3 p,float margin=0){if(centers.Select((c,i)=>RectDistance(p,i,margin)).Any(d=>d<0))return true;if(new Vector2(p.x-22,p.z-57).magnitude<19+margin)return true;foreach(int i in new[]{1,2,3,4})if(Segment(p,Entry(i),new Vector3(0,0,32))<6+margin)return true;return false;}
 static void Save(){AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);EditorSceneManager.SaveScene(terrain.gameObject.scene);}
 public static string Layout(){
  Init();if(EditorApplication.isPlaying||terrain.gameObject.scene.path!="Assets/Scenes/Gameplay.unity"||group)throw new Exception("Edit Gameplay with no existing expansion");
  Directory.CreateDirectory("output/NewMap/FiveHangars");Directory.CreateDirectory("Temp/FiveHangars");EditorSceneManager.SaveScene(terrain.gameObject.scene,"Temp/FiveHangars/Gameplay.before-expansion.unity",true);
  Directory.CreateDirectory(A);AssetDatabase.Refresh();AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(td),A+"/Expanded Farm Terrain.asset");td=AssetDatabase.LoadAssetAtPath<TerrainData>(A+"/Expanded Farm Terrain.asset");terrain.terrainData=td;terrain.GetComponent<TerrainCollider>().terrainData=td;
  group=Group("11_Five_Hangar_Farm",root);Undo.RegisterCreatedObjectUndo(group.gameObject,"Expand five hangar farm");
  var heights=td.GetHeights(0,0,td.heightmapResolution,td.heightmapResolution);int n=td.heightmapResolution;
  for(int z=0;z<n;z++)for(int x=0;x<n;x++){var p=terrain.transform.position+new Vector3(x/(float)(n-1)*td.size.x,0,z/(float)(n-1)*td.size.z);float h=heights[z,x]*td.size.y+terrain.transform.position.y;
   for(int i=1;i<5;i++){float d=RectDistance(p,i,3);float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,9,d));h=Mathf.Lerp(h,0,blend);}
   float ds=new Vector2(p.x-22,p.z-57).magnitude;h=Mathf.Lerp(h,0,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(18,27,ds)));
   float edge=Mathf.Min(Mathf.Min(p.x+192,192-p.x),Mathf.Min(p.z+70,314-p.z));float ridge=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,34,edge)))*(19+11*Mathf.PerlinNoise(p.x*.018f+45,p.z*.018f+89));h=Mathf.Max(h,ridge);
   heights[z,x]=Mathf.Clamp01((h-terrain.transform.position.y)/td.size.y);
  }td.SetHeights(0,0,heights);
  var hall=root.Find("01_Modular_Hangar/Tall timber hall with glazing");var floor=root.Find("01_Modular_Hangar/Concrete threshing floor");
  var colors=new[]{new Color(.4f,.09f,.05f),new Color(.46f,.31f,.12f),new Color(.17f,.30f,.36f),new Color(.32f,.07f,.035f),new Color(.19f,.29f,.16f)};
  for(int i=1;i<5;i++){var h=Group("Hangar "+(i+1)+(i<3?" - Medium":" - Large"),group);h.position=centers[i];h.rotation=Quaternion.Euler(0,yaws[i],0);h.localScale=scales[i];var shell=Object.Instantiate(hall,h,false);shell.name="Combined structure";shell.localPosition=Vector3.zero;shell.localRotation=Quaternion.identity;shell.localScale=Vector3.one;Object.Instantiate(floor,h,false).name="Concrete floor";
   var exterior=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/TallHangar/Weathered red plank cladding.mat")){name="Hangar "+(i+1)+" exterior"};exterior.SetColor("_BaseColor",colors[i]);AssetDatabase.CreateAsset(exterior,A+"/"+exterior.name+".mat");
   foreach(var r in shell.GetComponentsInChildren<MeshRenderer>()){r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains(" red ")?exterior:m).ToArray();}
   var light=Group("Daylight interior bounce",h).gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=35;light.intensity=4000;light.color=new Color(1,.9f,.72f);light.shadows=LightShadows.None;light.transform.localPosition=new Vector3(0,8,0);
  }
  var shop=Group("S - Farm supplies and cottage",group);
  Prefab("TFP_House_01A_Preset_1",shop,new Vector3(31,0,62),180,.6f).name="Small caretaker house";
  var building=Group("Farm supplies shop",shop);building.position=new Vector3(14,0,57);building.rotation=Quaternion.Euler(0,180,0);
  var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/TallHangar/Rich oak framing.mat");var cream=Mat("Shop warm cream",new Color(.67f,.54f,.32f));var roof=Mat("Shop green roof",new Color(.12f,.24f,.18f));
  Box("Floor",building,new Vector3(0,.06f,0),new Vector3(10,.2f,8),cream);Box("Back wall",building,new Vector3(0,2.5f,-3.85f),new Vector3(10,5,.22f),cream);foreach(int s in new[]{-1,1})Box("Side wall",building,new Vector3(s*4.9f,2.5f,0),new Vector3(.22f,5,8),cream);
  foreach(int s in new[]{-1,1}){Box("Front pilaster",building,new Vector3(s*4.8f,2.5f,4),new Vector3(.3f,5,.3f),wood);var panel=Box("Pitched roof",building,new Vector3(s*2.6f,5.65f,0),new Vector3(5.9f,.2f,9.5f),roof);panel.transform.localRotation=Quaternion.Euler(0,0,-s*18);}
  Box("Counter",building,new Vector3(0,1.05f,2.1f),new Vector3(7,1.7f,.85f),wood);Box("Countertop",building,new Vector3(0,1.95f,2.1f),new Vector3(7.4f,.12f,1.1f),cream);Box("Shop sign board",building,new Vector3(0,4.1f,4.16f),new Vector3(6.8f,.9f,.18f),roof);
  foreach(var c in new[]{new Vector3(11,0,54),new Vector3(12.3f,0,54),new Vector3(17,0,54)})Prefab(c.x<16?"TFP_Cereal_Sack_01A":"TFP_Wooden_Crate_01A",shop,c,10,1.2f);
  Prefab("TFP_Wooden_Pallet_01A",shop,new Vector3(18,0,59),20);Prefab("TFP_Sack_Pile_01A",shop,new Vector3(18,0,59),20);
  // Remove vegetation cells crossing new footprints; retain a scene-local inactive recovery group.
  int cleared=0;foreach(var t in root.Find("05_Landscape").GetComponentsInChildren<Transform>().OrderBy(t=>t.GetSiblingIndex()).ToArray()){
   if(t==root.Find("05_Landscape")||!t.gameObject.activeInHierarchy)continue;
   var lod=t.GetComponent<LODGroup>();if(!lod&&t.parent.GetComponentInParent<LODGroup>())continue;
   var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;
   Bounds b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
   bool overlaps=Clear(b.center,Mathf.Min(10,Mathf.Max(b.extents.x,b.extents.z)));
   if(overlaps&&(lod||t.GetComponent<MeshRenderer>()||t.childCount==0)){t.gameObject.SetActive(false);cleared++;}
  }
  // Fence crossing the new fan-shaped approaches would trap the player.
  foreach(var t in root.Find("03_Farmyard").Cast<Transform>().Where(t=>t.name.StartsWith("Fence")&&t.position.z<25).ToArray())t.gameObject.SetActive(false);
  PaintYards();EditorUtility.SetDirty(td);Save();return "Four larger hangars, S cottage/shop, flattened building sites, perimeter hills and yard terrain authored. Vegetation objects/cells cleared: "+cleared;
 }
 static void PaintYards(){int n=td.alphamapWidth;var alpha=td.GetAlphamaps(0,0,n,n);for(int z=0;z<n;z++)for(int x=0;x<n;x++){var p=terrain.transform.position+new Vector3(x/(float)(n-1)*td.size.x,0,z/(float)(n-1)*td.size.z);float d=1000;for(int i=1;i<5;i++){d=Mathf.Min(d,RectDistance(p,i,4));d=Mathf.Min(d,Segment(p,Entry(i),new Vector3(0,0,32))-5);}d=Mathf.Min(d,new Vector2(p.x-22,p.z-57).magnitude-17);float w=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,4,d));if(w<=0)continue;for(int l=0;l<td.alphamapLayers;l++)alpha[z,x,l]*=1-w;alpha[z,x,1]+=w*.3f;alpha[z,x,2]+=w*.1f;alpha[z,x,6]+=w*.6f;}td.SetAlphamaps(0,0,alpha);}
 public static string Grain(){Init();var source=Object.FindFirstObjectByType<GrainPile>();var grain=Group("Interactive grain stockpiles",group);for(int i=1;i<5;i++){
   var g=Object.Instantiate(source.gameObject,grain);g.name="Hangar "+(i+1)+" - "+(i<3?"Almost full grain bank":"Grain bank to entrance");g.SetActive(false);
   foreach(var scatter in g.GetComponents<CornGroundScatter>())Object.DestroyImmediate(scatter);
   float width=20*scales[i].x-1,depth=30*scales[i].z-(i<3?7:2.5f);g.transform.SetPositionAndRotation(centers[i]+Quaternion.Euler(0,yaws[i],0)*new Vector3(0,.04f,i<3?-2.8f:-.8f),Quaternion.Euler(0,yaws[i]+180,0));g.transform.localScale=new Vector3(width/24,1,depth/24);
   var pile=g.GetComponent<GrainPile>();Set(pile,"size",p=>p.floatValue=24);Set(pile,"initialHeight",p=>p.floatValue=i<3?6.4f:8.4f);Set(pile,"shape",p=>p.enumValueIndex=2);Set(pile,"fitToWalls",p=>p.boolValue=false);Set(pile,"shapeSeed",p=>p.intValue=840+i);
   var so=new SerializedObject(pile);var m=so.FindProperty("mounds");m.arraySize=2;for(int j=0;j<2;j++){var el=m.GetArrayElementAtIndex(j);el.FindPropertyRelative("center").vector2Value=new Vector2(j==0?-.28f:.3f,.2f);el.FindPropertyRelative("radius").vector2Value=new Vector2(2f,2.2f);el.FindPropertyRelative("height").floatValue=j==0?1:.93f;}so.ApplyModifiedPropertiesWithoutUndo();
   var kernels=g.GetComponent<CornSurfaceKernels>();Set(kernels,"spacing",p=>p.floatValue=.06f);Set(kernels,"drawDistance",p=>p.floatValue=28);Set(kernels,"fullDetailDistance",p=>p.floatValue=1.8f);g.SetActive(true);pile.RebuildPreview();kernels.Rebuild();
  }Save();return "Original starter pile preserved; four larger scoopable grain banks added with near-full/entrance-full footprints.";}
}

