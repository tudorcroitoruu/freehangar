using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class OriginalRoadsidePlanting {
public static string Main(){
if(EditorApplication.isPlaying)throw new Exception("Edit mode required");var root=GameObject.Find("New_Map").transform;var land=root.Find("05_Landscape");if(land.Find("Original mixed grass and wildflowers"))throw new Exception("Already placed");
var terrain=root.GetComponentInChildren<Terrain>();var road=File.ReadAllLines("output/NewMap/Revision2/road-centerline.csv").Skip(1).Select(l=>{var a=l.Split(',');return new Vector3(float.Parse(a[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(a[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(a[2],System.Globalization.CultureInfo.InvariantCulture));}).ToArray();
var parent=new GameObject("Original mixed grass and wildflowers").transform;parent.SetParent(land,false);var rng=new System.Random(52410);string[] flowers={"TFP_Flowers_Patch_01A","TFP_Flowers_Patch_01D","TFP_Flowers_Patch_02A","TFP_Flowers_Patch_02C"};string[] grasses={"TFP_Grass_Patch_04A","TFP_Grass_Patch_05A","TFP_Grass_Patch_06A"};int count=0;
for(int i=12;i<road.Length-4;i+=3){var tangent=(road[i+1]-road[i-1]);tangent.y=0;tangent.Normalize();var side=new Vector3(-tangent.z,0,tangent.x)*(i%2==0?1:-1);
for(int j=0;j<5;j++){
float lateral=7.6f+(float)rng.NextDouble()*5;var p=road[i]+side*lateral+tangent*((float)rng.NextDouble()*7-3.5f);
if(new Vector2(p.x,p.z-14).magnitude<35||new Vector2(p.x-25,p.z-39).magnitude<15||new Vector2(p.x-8,p.z-175).magnitude<37)continue;
if(p.x>-104&&p.x<-32&&p.z>53&&p.z<127)continue;
p.y=terrain.SampleHeight(p)+terrain.transform.position.y-.02f;string name=j<3?flowers[rng.Next(flowers.Length)]:grasses[rng.Next(grasses.Length)];var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Imported/Toon Series/Toon Farm Pack/Prefabs/Vegetation/Grass/"+name+".prefab");var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);float scale=.65f+(float)rng.NextDouble()*.5f;go.transform.localScale=Vector3.one*scale;count++;
}
}
EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return count+" original flower and mixed-grass patches added in roadside clusters.";
}
}
