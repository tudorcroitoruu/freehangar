using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class CreateGallery
{
    public static string Main()
    {
        var gameplay = SceneManager.GetActiveScene();
        var source = Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(p=>p.gameObject.scene==gameplay);
        var kernelMesh = (Mesh)new SerializedObject(source.GetComponent<CornSurfaceKernels>()).FindProperty("kernelMesh").objectReferenceValue;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var cameraObject = new GameObject("Grain gallery camera", typeof(Camera), typeof(HDAdditionalCameraData));
        var camera = cameraObject.GetComponent<Camera>();
        camera.transform.position = new Vector3(0,9,-10); camera.transform.LookAt(new Vector3(0,0,1.2f));
        camera.orthographic = true; camera.orthographicSize = 5.25f;
        camera.GetComponent<HDAdditionalCameraData>().clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        camera.GetComponent<HDAdditionalCameraData>().backgroundColorHDR = new Color(.035f,.045f,.06f);
        camera.GetComponent<HDAdditionalCameraData>().volumeLayerMask = 1 << 31;
        var volumeObject = new GameObject("Gallery exposure", typeof(Volume)); var volume = volumeObject.GetComponent<Volume>(); volume.isGlobal = true; volume.priority=1000;
        volumeObject.layer = 31;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var exposure = profile.Add<Exposure>(true); exposure.mode.value=ExposureMode.Fixed; exposure.fixedExposure.value=0;
        string profilePath="Assets/GrainPrototype/Grains/Gallery Exposure.asset";
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (existing == null) { AssetDatabase.CreateAsset(profile,profilePath); AssetDatabase.AddObjectToAsset(exposure,profile); volume.sharedProfile=profile; }
        else { volume.sharedProfile=existing; Object.DestroyImmediate(profile); }
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name="Gallery floor"; ground.transform.localScale = new Vector3(1.6f,1,1.2f); ground.transform.position=new Vector3(0,-.02f,1.5f);
        var floorMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/GrainPrototype/Grains/Gallery Floor.mat");
        if (floorMat==null) { floorMat=new Material(Shader.Find("HDRP/Unlit")); floorMat.SetColor("_UnlitColor",new Color(.045f,.055f,.068f)); AssetDatabase.CreateAsset(floorMat,"Assets/GrainPrototype/Grains/Gallery Floor.mat"); }
        ground.GetComponent<Renderer>().sharedMaterial=floorMat;
        for (int i=0;i<10;i++)
        {
            var grain=(GrainType)i;
            var go=new GameObject(GrainNames.Display(grain)); go.SetActive(false);
            go.transform.position=new Vector3((i%5-2)*2.25f,0,(1-i/5)*2.6f);
            var pile=go.AddComponent<GrainPile>();
            using(var data=new SerializedObject(pile)) {data.FindProperty("size").floatValue=1.8f;data.FindProperty("initialHeight").floatValue=.42f;data.FindProperty("fitToWalls").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();}
            pile.SetRecipe(new[]{new GrainLayer(52,grain)});
            pile.GetComponent<MeshRenderer>().sharedMaterial=GrainTypesSetup.Material(false);
            var kernels=go.AddComponent<CornSurfaceKernels>();
            using(var data=new SerializedObject(kernels)) {data.FindProperty("kernelMesh").objectReferenceValue=kernelMesh;data.FindProperty("kernelMaterial").objectReferenceValue=GrainTypesSetup.Material(true);data.FindProperty("spacing").floatValue=.0315f;data.FindProperty("fullDetailDistance").floatValue=30;data.FindProperty("drawDistance").floatValue=50;data.ApplyModifiedPropertiesWithoutUndo();}
            go.SetActive(true); pile.RebuildPreview(); kernels.Rebuild();
            var label=new GameObject("Label",typeof(TextMeshPro)); label.transform.position=go.transform.position+new Vector3(0,.025f,-1.02f); label.transform.rotation=Quaternion.Euler(65,0,0);
            var text=label.GetComponent<TextMeshPro>();text.text=GrainNames.Display(grain);text.fontSize=3.2f;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.95f,.9f,.76f);text.rectTransform.sizeDelta=new Vector2(2.2f,.5f);
            string path="Assets/GrainPrototype/Grains/"+grain+".mat";
            if(AssetDatabase.LoadAssetAtPath<Material>(path)==null)
            {
                var material=new Material(GrainTypesSetup.Material(true));material.name=GrainNames.Display(grain);
                var a=Vector4.zero;var b=Vector4.zero;var c=Vector4.zero;
                if(i<4)a[i]=1;else if(i<8)b[i-4]=1;else c[i-8]=1;
                material.SetVector("_GrainWeightsA",a);material.SetVector("_GrainWeightsB",b);material.SetVector("_GrainWeightsC",c);AssetDatabase.CreateAsset(material,path);
            }
        }
        foreach(var root in scene.GetRootGameObjects()) root.transform.position += new Vector3(1000,0,1000);
        foreach(var pile in Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(p=>p.gameObject.scene==scene)) {pile.RebuildPreview();pile.GetComponent<CornSurfaceKernels>().Rebuild();}
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/GrainGallery.unity");
        Capture(camera,"output/GrainTypes/GrainPalette.png",1800,1100);
        EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(gameplay);
        return "Created GrainGallery.unity, ten crop materials and output/GrainTypes/GrainPalette.png";
    }
    public static void Capture(Camera camera,string path,int width,int height)
    {
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
        var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
        try {camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(texture);}
    }
}
