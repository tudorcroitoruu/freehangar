using System;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.Rendering.HighDefinition;using Object=UnityEngine.Object;
public static class FinalArt {
public static string Main(){
var root=GameObject.Find("New_Map").transform;
var all=root.GetComponentsInChildren<TextMesh>();
var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
font.RequestCharactersInTexture(string.Join("",all.Select(t=>t.text)),64,FontStyle.Normal);
var tex=font.material.mainTexture;var rt=RenderTexture.GetTemporary(tex.width,tex.height,0,RenderTextureFormat.ARGB32);Graphics.Blit(tex,rt);var prev=RenderTexture.active;RenderTexture.active=rt;
var atlas=new Texture2D(tex.width,tex.height,TextureFormat.RGBA32,false);atlas.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);atlas.Apply();RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);
var colors=atlas.GetPixels();for(int i=0;i<colors.Length;i++)colors[i]=new Color(1,1,1,colors[i].a);atlas.SetPixels(colors);atlas.Apply();atlas.name="Baked farm sign glyph atlas";AssetDatabase.CreateAsset(atlas,"Assets/NewMap/Materials/SignGlyphAtlas.asset");
var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/NewMap/Materials/Depth tested signage.mat");mat.SetTexture("_UnlitColorMap",atlas);mat.SetColor("_UnlitColor",new Color(.85f,.7f,.47f));HDMaterial.ValidateMaterial(mat);
int index=0;
foreach(var t in all){
var text=t.text;var parent=t.transform.parent;var panel=parent.Find("Painted panel");float w=panel?panel.localScale.x*.85f:3,h=panel?panel.localScale.y*.75f:.5f;
var lines=text.Split('\n');var widths=new List<float>();foreach(var line in lines){float width=0;foreach(char ch in line){font.GetCharacterInfo(ch,out CharacterInfo info,64);width+=info.advance;}widths.Add(width);}
float scale=Mathf.Min(w/Mathf.Max(1,widths.Max()),h/(lines.Length*64f));
var vertices=new List<Vector3>();var uvs=new List<Vector2>();var tris=new List<int>();
for(int row=0;row<lines.Length;row++){float x=-widths[row]*.5f;float y=(lines.Length-1)*36-row*72-22;
foreach(char ch in lines[row]){font.GetCharacterInfo(ch,out CharacterInfo c,64);int b=vertices.Count;
vertices.Add(new Vector3((x+c.minX)*scale,(y+c.maxY)*scale,0));vertices.Add(new Vector3((x+c.maxX)*scale,(y+c.maxY)*scale,0));vertices.Add(new Vector3((x+c.maxX)*scale,(y+c.minY)*scale,0));vertices.Add(new Vector3((x+c.minX)*scale,(y+c.minY)*scale,0));
uvs.Add(c.uvTopLeft);uvs.Add(c.uvTopRight);uvs.Add(c.uvBottomRight);uvs.Add(c.uvBottomLeft);tris.AddRange(new[]{b,b+1,b+2,b,b+2,b+3});x+=c.advance;}}
var go=t.gameObject;Object.DestroyImmediate(t);var mr=go.GetComponent<MeshRenderer>();if(!mr)mr=go.AddComponent<MeshRenderer>();mr.sharedMaterial=mat;
var mf=go.GetComponent<MeshFilter>();if(!mf)mf=go.AddComponent<MeshFilter>();var mesh=new Mesh{name="Sign "+index};mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,"Assets/NewMap/Meshes/Sign "+index+++".asset");mf.sharedMesh=mesh;
}
var props=root.Find("02_Hangar_Props");props.Find("Container shelves - reference left").localPosition=new Vector3(-6.55f,0,-.7f);
props.Find("Open filling sack").localPosition=new Vector3(-1.5f,.08f,-5.6f);
root.Find("Player").rotation=Quaternion.Euler(0,-13,0);
var cam=root.Find("Player").GetComponentInChildren<Camera>();
var refs=root.Find("07_Reference_Views");
refs.GetChild(0).SetPositionAndRotation(cam.transform.position,cam.transform.rotation);
CreateCamera("Exterior Review",refs,new Vector3(27,2.4f,38),new Vector3(-1,3,25),68);
CreateCamera("Map Overview",refs,new Vector3(220,260,-100),new Vector3(0,0,115),52);
CreateCamera("Village Review",refs,new Vector3(8,7,214),new Vector3(8,10,255),60);
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return "Baked depth-correct signs and adjusted composition";
}
static void CreateCamera(string name,Transform p,Vector3 pos,Vector3 target,float fov){
var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.position=pos;g.transform.LookAt(target);var c=g.AddComponent<Camera>();c.fieldOfView=fov;c.farClipPlane=800;c.enabled=false;g.AddComponent<HDAdditionalCameraData>();
}
}
