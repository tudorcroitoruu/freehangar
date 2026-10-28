using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class PolishCabin
{
 const string A="Assets/Models/Rocket Modular";
 static readonly float[] heights={1.74f,1.9f,2.1f,2.3f,2.6f,4.14f};
 static readonly float[] radii={.77f,.89f,1.04f,1.17f,1.31f,1.31f};
 static Texture2D Texture(string name,bool floor,bool mask)
 {
  int size=512;var t=new Texture2D(size,size,TextureFormat.RGBA32,false);var rng=new System.Random(61);var pixels=new Color[size*size];
  for(int y=0;y<size;y++)for(int x=0;x<size;x++)
  {
   float noise=(float)rng.NextDouble()*.025f;Color c=new Color(.20f+noise,.245f+noise,.28f+noise);
   if(floor){int line=(x+y)%64;float tread=line<5||Math.Abs((x-y)%64)<5?.23f:.115f;c=new Color(tread+noise,tread*1.1f+noise,tread*1.18f+noise);}
   else
   {
    int edge=Math.Min(Math.Min(x,511-x),Math.Min(y,511-y));if(edge<5)c=new Color(.035f,.045f,.055f);else if(edge<9)c=new Color(.30f,.35f,.39f);
    if(x>54&&x<458&&y>115&&y<370){if(x<59||x>453||y<120||y>365)c*=.45f;else c*=.88f;}
    if(x>70&&x<210&&y>405&&y<463&&(y%12)<4)c=new Color(.035f,.045f,.055f);
    foreach(int bx in new[]{24,487})foreach(int by in new[]{24,487}){int dx=x-bx,dy=y-by;if(dx*dx+dy*dy<42)c=new Color(.38f,.42f,.44f);if(Math.Abs(dx)<2&&Math.Abs(dy)<5)c=new Color(.05f,.06f,.07f);}
   }
   pixels[y*size+x]=mask?new Color(.18f,1,0,.19f+noise):c;
  }
  t.SetPixels(pixels);t.Apply();string path=A+"/"+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.sRGBTexture=!mask;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.wrapMode=TextureWrapMode.Repeat;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
 }
 static Mesh Liner()
 {
  var vs=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
  for(int sector=1;sector<22;sector++) // Symmetric opening preserves the entrance.
  {
   float a=(sector+.5f)*15*Mathf.Deg2Rad,b=(sector+1.5f)*15*Mathf.Deg2Rad;
   for(int band=0;band<heights.Length-1;band++)
   {
    int k=vs.Count;vs.Add(new Vector3(Mathf.Sin(a)*radii[band],heights[band],Mathf.Cos(a)*radii[band]));vs.Add(new Vector3(Mathf.Sin(b)*radii[band],heights[band],Mathf.Cos(b)*radii[band]));vs.Add(new Vector3(Mathf.Sin(b)*radii[band+1],heights[band+1],Mathf.Cos(b)*radii[band+1]));vs.Add(new Vector3(Mathf.Sin(a)*radii[band+1],heights[band+1],Mathf.Cos(a)*radii[band+1]));
    uv.Add(new Vector2(sector*.5f,(heights[band]-1.74f)*.8f));uv.Add(new Vector2((sector+1)*.5f,(heights[band]-1.74f)*.8f));uv.Add(new Vector2((sector+1)*.5f,(heights[band+1]-1.74f)*.8f));uv.Add(new Vector2(sector*.5f,(heights[band+1]-1.74f)*.8f));
    tris.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});
   }
  }
  var m=new Mesh{name="Tapered cabin inner lining"};m.SetVertices(vs);m.SetUVs(0,uv);m.SetTriangles(tris,0);m.RecalculateNormals();m.RecalculateBounds();return m;
 }
 static void UpdateCabin(Transform root,Mesh liner,Material wall,Material floorMat)
 {
  var cabin=root.Find("Interior Devices");
  foreach(var child in cabin.Cast<Transform>().Where(t=>t.name.StartsWith("Interior wall segment")).ToArray())
  {
   foreach(var strip in child.Cast<Transform>().ToArray())strip.SetParent(cabin,true);
   Object.DestroyImmediate(child.gameObject);
  }
  var lining=new GameObject("Fitted textured cabin lining");lining.transform.SetParent(cabin,false);lining.AddComponent<MeshFilter>().sharedMesh=liner;var renderer=lining.AddComponent<MeshRenderer>();renderer.sharedMaterial=wall;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.TwoSided;
  cabin.Find("Cabin floor").localScale=new Vector3(1.54f,.04f,1.54f);cabin.Find("Cabin floor").GetComponent<Renderer>().sharedMaterial=floorMat;
  cabin.Find("Cabin ceiling").GetComponent<Renderer>().sharedMaterial=wall;
  foreach(var light in cabin.GetComponentsInChildren<Light>()){light.intensity=root.lossyScale.x<.1f?.05f:12f;light.range=root.lossyScale.x<.1f?.035f:2.4f;}
 }
 public static string Run()
 {
  string path=A+"/Rocket Modular.prefab";AssetDatabase.CopyAsset(path,A+"/Rocket Modular before polish.prefab");
  var panelTex=Texture("Cabin panel albedo",false,false);var mask=Texture("Cabin rough metal mask",false,true);var floorTex=Texture("Cabin deck tread",true,false);
  var metal=AssetDatabase.LoadAssetAtPath<Material>(A+"/Cabin graphite.mat");metal.SetTexture("_BaseColorMap",panelTex);metal.SetColor("_BaseColor",Color.white);metal.SetTexture("_MaskMap",mask);metal.EnableKeyword("_MASKMAP");metal.SetFloat("_Metallic",.18f);metal.SetFloat("_Smoothness",.2f);EditorUtility.SetDirty(metal);
  var trim=AssetDatabase.LoadAssetAtPath<Material>(A+"/Cabin aluminum.mat");trim.SetColor("_BaseColor",new Color(.12f,.17f,.21f));trim.SetFloat("_Metallic",.25f);trim.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(trim);
  var floorMat=new Material(metal){name="Textured cabin deck"};floorMat.SetTexture("_BaseColorMap",floorTex);floorMat.SetTextureScale("_BaseColorMap",new Vector2(3,3));AssetDatabase.CreateAsset(floorMat,A+"/Textured cabin deck.mat");
  var screens=AssetDatabase.LoadAssetAtPath<Material>(A+"/Cockpit emissive screens.mat");screens.SetFloat("_Smoothness",.04f);screens.SetFloat("_Metallic",0);screens.SetColor("_EmissiveColor",Color.white*2.5f);EditorUtility.SetDirty(screens);
  var amber=AssetDatabase.LoadAssetAtPath<Material>(A+"/Amber status emission.mat");amber.SetColor("_EmissiveColor",new Color(2,.55f,.025f));EditorUtility.SetDirty(amber);
  var cyan=AssetDatabase.LoadAssetAtPath<Material>(A+"/Cyan status emission.mat");cyan.SetColor("_EmissiveColor",new Color(.04f,1.2f,2));EditorUtility.SetDirty(cyan);
  var liner=Liner();AssetDatabase.CreateAsset(liner,A+"/Fitted cabin lining.asset");
  var prefab=PrefabUtility.LoadPrefabContents(path);UpdateCabin(prefab.transform,liner,metal,floorMat);PrefabUtility.SaveAsPrefabAsset(prefab,path);PrefabUtility.UnloadPrefabContents(prefab);
  var preview=GameObject.Find("Rocket preview temporary");if(preview!=null)UpdateCabin(preview.transform,liner,metal,floorMat);
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  return "Tapered lining fitted inside measured hull; replaced smooth plain panels with albedo/mask/tread textures; lowered light/specular/emission intensity.";
 }
}
