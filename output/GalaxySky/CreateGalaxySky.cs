using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object=UnityEngine.Object;
public static class CreateGalaxySky
{
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
        const string folder="Assets/EarthRendering Free/HDRP";
        if(AssetDatabase.IsValidFolder(folder))throw new Exception("HDRP sky folder already exists; inspect before replacing");
        var volume=GameObject.Find("Sky and Fog").GetComponent<Volume>();
        EditorSceneManager.SaveScene(volume.gameObject.scene,"output/GalaxySky/Before.unity",true);
        AssetDatabase.CreateFolder("Assets/EarthRendering Free","HDRP");
        var cube=new Cubemap(1024,TextureFormat.RGBAHalf,true){name="Galaxy HDRI",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};
        string[] names={"Lf","Rt","Up","Dn","Ft","Bk"};
        CubemapFace[] faces={CubemapFace.PositiveX,CubemapFace.NegativeX,CubemapFace.PositiveY,CubemapFace.NegativeY,CubemapFace.PositiveZ,CubemapFace.NegativeZ};
        for(int f=0;f<6;f++)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!texture.LoadImage(File.ReadAllBytes("Assets/EarthRendering Free/Galaxy"+names[f]+".png")))throw new Exception("Could not load face");
            if(texture.width!=1024||texture.height!=1024)throw new Exception("Unexpected face resolution");
            var pixels=texture.GetPixels();
            for(int p=0;p<pixels.Length;p++)pixels[p]=pixels[p].linear;
            cube.SetPixels(pixels,faces[f]);Object.DestroyImmediate(texture);
        }
        cube.Apply(true,false);
        AssetDatabase.CreateAsset(cube,folder+"/Galaxy HDRI.asset");
        var profile=Object.Instantiate(volume.sharedProfile);profile.name="Galaxy HDRP Sky";
        // Clone component subassets as well, retaining this scene's fog/exposure configuration.
        profile.components=volume.sharedProfile.components.Select(c=>Object.Instantiate(c)).ToList();
        AssetDatabase.CreateAsset(profile,folder+"/Galaxy HDRP Sky.asset");
        foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);
        VisualEnvironment environment;if(!profile.TryGet(out environment)){environment=profile.Add<VisualEnvironment>();AssetDatabase.AddObjectToAsset(environment,profile);}
        environment.active=true;environment.skyType.Override(1);
        HDRISky sky;if(!profile.TryGet(out sky)){sky=profile.Add<HDRISky>();AssetDatabase.AddObjectToAsset(sky,profile);}
        sky.active=true;sky.hdriSky.Override(cube);sky.rotation.Override(0);sky.exposure.Override(0);
        PhysicallyBasedSky physical;if(profile.TryGet(out physical))physical.active=false;
        GradientSky gradient;if(profile.TryGet(out gradient))gradient.active=false;
        VolumetricClouds clouds;if(profile.TryGet(out clouds))clouds.active=false;
        CloudLayer layer;if(profile.TryGet(out layer))layer.active=false;
        volume.sharedProfile=profile;EditorUtility.SetDirty(volume);EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(volume.gameObject.scene);
        Selection.activeObject=profile;
        return "Created 1024px-per-face RGBAHalf cubemap with mipmaps and Galaxy HDRP Sky profile; applied to "+volume.gameObject.scene.path;
    }
}
