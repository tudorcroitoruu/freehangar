using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering.HighDefinition;
public static class HangarSurfaceDetail {
const string A="Assets/NewMap/TallHangar/";
static Material Lit(string name,Texture albedo,Texture normal,Color tint,float smooth){var m=new Material(Shader.Find("HDRP/Lit")){name=name};m.SetTexture("_BaseColorMap",albedo);m.SetTexture("_NormalMap",normal);m.SetFloat("_NormalScale",.35f);m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",smooth);HDMaterial.ValidateMaterial(m);AssetDatabase.CreateAsset(m,A+name+".mat");return m;}
public static string Main(){
if(EditorApplication.isPlaying)throw new Exception("Edit mode required");if(File.Exists(A+"Mapped surfaces.asset"))throw new Exception("Already applied");AssetDatabase.Refresh();Directory.CreateDirectory("output/NewMap/HangarMaterials");
foreach(var suffix in new[]{"Albedo","Normal"}){string path=A+"WeatheredGreyBoards_"+suffix+".png";var i=(TextureImporter)AssetImporter.GetAtPath(path);i.maxTextureSize=2048;i.filterMode=FilterMode.Trilinear;i.anisoLevel=16;i.mipmapEnabled=true;i.wrapMode=TextureWrapMode.Repeat;i.textureCompression=TextureImporterCompression.CompressedHQ;if(suffix=="Normal"){i.textureType=TextureImporterType.NormalMap;i.convertToNormalmap=true;i.heightmapScale=.025f;}i.SaveAndReimport();}
var board=AssetDatabase.LoadAssetAtPath<Texture2D>(A+"WeatheredGreyBoards_Albedo.png");var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(A+"WeatheredGreyBoards_Normal.png");var oak=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/NewMap/Textures/Oak_Albedo.png");var oakNormal=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/NewMap/Textures/Oak_Normal.png");var hall=GameObject.Find("New_Map").transform.Find("01_Modular_Hangar/Tall timber hall with glazing");int count=0,bolts=0;
AssetDatabase.StartAssetEditing();try{
var walls=Lit("Weathered grey plank walls",board,normal,new Color(.92f,.88f,.82f),.18f);var roofing=Lit("Weathered grey roof lining",board,normal,new Color(.80f,.76f,.70f),.22f);var timber=Lit("Rich oak framing",oak,oakNormal,new Color(.88f,.68f,.46f),.20f);var red=Lit("Weathered red plank cladding",board,normal,new Color(.72f,.16f,.095f),.17f);var iron=Lit("Dark aged iron fasteners",null,null,new Color(.10f,.08f,.065f),.35f);
foreach(var f in hall.GetComponentsInChildren<MeshFilter>()){
var r=f.GetComponent<Renderer>();var old=r.sharedMaterial;if(old.name.Contains("glass"))continue;bool wood=old.name=="Warm oak structural beams";bool wall=f.name.Contains("wall panel")||f.name.Contains("end wall panel");bool roof=f.name=="Grey roof panel";bool outside=old.name=="Weathered red exterior";if(!wood&&!wall&&!roof&&!outside)continue;
var mesh=UnityEngine.Object.Instantiate(f.sharedMesh);mesh.name="Metre mapped "+count;var verts=mesh.vertices;var ns=mesh.normals;var uv=new Vector2[verts.Length];var scale=f.transform.lossyScale;int axis=scale.x>scale.y?(scale.x>scale.z?0:2):(scale.y>scale.z?1:2);float offset=(count*.371f)%1;
for(int j=0;j<verts.Length;j++){
var local=Vector3.Scale(verts[j],scale);var world=f.transform.TransformPoint(verts[j]);
if(wood){int across=0;for(int k=0;k<3;k++)if(k!=axis&&Mathf.Abs(ns[j][k])<.5f)across=k;uv[j]=new Vector2(local[across]/.8f+offset,local[axis]/2.8f+offset);}
else if(roof)uv[j]=new Vector2(world.z/4,local.x/4+offset);
else uv[j]=new Vector2((scale.x<scale.z?world.z:world.x)/4,world.y/4);
}mesh.uv=uv;mesh.RecalculateTangents();if(count==0)AssetDatabase.CreateAsset(mesh,A+"Mapped surfaces.asset");else AssetDatabase.AddObjectToAsset(mesh,A+"Mapped surfaces.asset");f.sharedMesh=mesh;r.sharedMaterial=wood?timber:roof?roofing:outside?red:walls;count++;
}
// Small physical hardware catches light at the structural joints.
var details=new GameObject("Structural iron fixings").transform;details.SetParent(hall,false);
foreach(int side in new[]{-1,1})foreach(float z in new[]{-15f,-9f,-3f,3f,9f,15f})foreach(float y in new[]{3.8f,7.2f,11.8f})foreach(float dy in new[]{-.12f,.12f}){
var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="Recessed iron bolt head";g.transform.SetParent(details,false);g.transform.localPosition=new Vector3(side*9.52f,y+dy,z);g.transform.localRotation=Quaternion.Euler(0,0,90);g.transform.localScale=new Vector3(.065f,.017f,.065f);g.GetComponent<Renderer>().sharedMaterial=iron;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());bolts++;}
}finally{AssetDatabase.StopAssetEditing();}
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());var report=count+" wall/roof/timber surfaces mapped at physical metre scale. New generated grey boards albedo "+board.width+"x"+board.height+", derived normal import, trilinear mipmaps, 16x anisotropic filtering. Directional oak grain follows beams. "+bolts+" joint fasteners. Geometry, glazing, player, lighting and map layout preserved.";File.WriteAllText("output/NewMap/HangarMaterials/summary.txt",report);return report;
}
}
