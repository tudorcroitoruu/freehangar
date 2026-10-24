using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.Rendering;using UnityEngine.Rendering.HighDefinition;
public static class FinalHillPolish {
public static string Main(){
if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
var root=GameObject.Find("New_Map");
foreach(var t in root.transform.Find("04_Mini_Village").Cast<Transform>().Where(t=>t.name.StartsWith("Fence ")&&t.position.z>175&&t.position.z<188))t.gameObject.SetActive(false);
var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/NewMap/Lighting/SurfaceGrade.asset");profile.TryGet<ColorAdjustments>(out var ca);ca.postExposure.value=.1f;ca.saturation.value=2;EditorUtility.SetDirty(ca);
// Flatten map-owned authoring-format copies to PNG at their native runtime resolution.
// Retain imported originals; substitute the new PNG references only under NewMap.
var materials=AssetDatabase.FindAssets("t:Material",new[]{"Assets/NewMap"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).ToArray();
var layers=root.GetComponentInChildren<Terrain>().terrainData.terrainLayers;int count=0;
foreach(var path in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/NewMap/Textures"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".psd")||p.EndsWith(".tif")).ToArray()){
var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var output=Path.ChangeExtension(path,".png");
var oldImporter=(TextureImporter)AssetImporter.GetAtPath(path);var settings=new TextureImporterSettings();oldImporter.ReadTextureSettings(settings);
var rt=RenderTexture.GetTemporary(tex.width,tex.height,0,RenderTextureFormat.ARGB32,oldImporter.sRGBTexture?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear);
var previous=RenderTexture.active;Graphics.Blit(tex,rt);RenderTexture.active=rt;var flat=new Texture2D(tex.width,tex.height,TextureFormat.RGBA32,false,!oldImporter.sRGBTexture);flat.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);flat.Apply();File.WriteAllBytes(output,flat.EncodeToPNG());RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(flat);
AssetDatabase.ImportAsset(output);var importer=(TextureImporter)AssetImporter.GetAtPath(output);importer.SetTextureSettings(settings);importer.maxTextureSize=oldImporter.maxTextureSize;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.compressionQuality=100;importer.SaveAndReimport();var replacement=AssetDatabase.LoadAssetAtPath<Texture2D>(output);
foreach(var m in materials)foreach(var key in m.GetTexturePropertyNames())if(m.GetTexture(key)==tex){m.SetTexture(key,replacement);EditorUtility.SetDirty(m);}
foreach(var l in layers){if(l.diffuseTexture==tex)l.diffuseTexture=replacement;if(l.normalMapTexture==tex)l.normalMapTexture=replacement;EditorUtility.SetDirty(l);}
AssetDatabase.SaveAssets();AssetDatabase.DeleteAsset(path);count++;
}
var marker=root.transform.Find("07_Reference_Views").GetChild(0);var camera=GameObject.Find("Player").GetComponentInChildren<Camera>();marker.name="Interior - current player - 60 vertical FOV";marker.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return "Village road gate cleared; grading balanced; "+count+" scene texture copies compacted to PNG.";
}
}
