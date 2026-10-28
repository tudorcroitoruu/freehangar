using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ColourExhaust
{
    static void UpdateRocket(GameObject root)
    {
        foreach(var label in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if(label.text=="NAVIGATION" || label.text=="ENGINE CONTROL" || label.text=="POWER SYSTEM") Object.DestroyImmediate(label.gameObject);
        foreach(var p in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if(p.name=="SmokeTrail") continue;
            var main=p.main; main.startColor=Color.white;
            var colors=new Gradient();
            if(p.name=="FireSpikes")
                colors.SetKeys(new[]{new GradientColorKey(new Color(.015f,.15f,1),0),new GradientColorKey(new Color(.04f,.45f,1),.32f),new GradientColorKey(new Color(1,.8f,.01f),.55f),new GradientColorKey(new Color(1,.16f,.005f),.85f),new GradientColorKey(new Color(.8f,.05f,0),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.65f),new GradientAlphaKey(0,1)});
            else if(p.name=="SparkTrail")
                colors.SetKeys(new[]{new GradientColorKey(new Color(1,.8f,.04f),0),new GradientColorKey(new Color(1,.15f,.005f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});
            else
                colors.SetKeys(new[]{new GradientColorKey(new Color(.02f,.22f,1),0),new GradientColorKey(new Color(.04f,.5f,1),1)},new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(0,1)});
            var lifetime=p.colorOverLifetime; lifetime.enabled=true; lifetime.color=colors;
        }
    }
    public static object Run()
    {
        if(EditorApplication.isPlaying) throw new System.Exception("Edit mode required");
        foreach(var path in new[]{"Assets/Models/Rocket Modular/Rocket Modular.prefab","Assets/Models/Rocket Modular/Rocket Fleet.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path); UpdateRocket(root); PrefabUtility.SaveAsPrefabAsset(root,path); PrefabUtility.UnloadPrefabContents(root);
        }
        var preview=GameObject.Find("Rocket preview temporary"); if(preview!=null) UpdateRocket(preview);
        AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "Blue core, yellow/orange plume saved; cabin equipment labels removed.";
    }
}
