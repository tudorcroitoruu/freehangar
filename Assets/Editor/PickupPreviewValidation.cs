using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PickupPreviewValidation
{
    [InitializeOnLoadMethod]
    private static void AfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("PickupPreview.MixedLengthFix.1",false)) return;
            SessionState.SetBool("PickupPreview.MixedLengthFix.1",true);
            Validate();
        };
    }
    [MenuItem("Tools/Vehicles/Validate pickup previews")]
    public static void Validate()
    {
        var report=new System.Text.StringBuilder();
        try
        {
            string[] files={"5_Pick_up_Driveable","6_Pick_Up_Bigger_Driveable","7_Pick_Up_Last_Driveable"};
            int[] expected={5,6,8};
            var small=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab").GetComponent<GrainContainer>();
            var big=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/BigBag.prefab").GetComponent<GrainContainer>();
            for(int i=0;i<files.Length;i++)
            {
                var scene=EditorSceneManager.NewPreviewScene();
                try
                {
                    var prefab=Resources.Load<GameObject>("Vehicles/"+files[i]);
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    var layout=instance.GetComponentInChildren<PickupCargoLayout>(true);
                    var serialized=new SerializedObject(layout);
                    serialized.FindProperty("anchors").arraySize=0;serialized.ApplyModifiedPropertiesWithoutUndo();
                    var smallSlots=layout.PreviewSlots(small);var bigSlots=layout.PreviewSlots(big);
                    if(smallSlots.Count!=expected[i]*2 || bigSlots.Count!=expected[i]) throw new Exception(files[i]+": wrong preview counts");
                    foreach(var slot in smallSlots) if(slot==null)throw new Exception("Missing small slot");
                    foreach(var slot in bigSlots) if(slot==null)throw new Exception("Missing large slot");
                    if(layout.PreviewSlots(small).Count!=smallSlots.Count)throw new Exception("Repeated build changed capacity");
                    Vector3 separation=smallSlots[1].localPosition-smallSlots[0].localPosition;
                    if(Mathf.Abs(separation.x)>.0001f || separation.z<=0) throw new Exception("Small container pairs must run lengthwise");
                    foreach(string type in new[]{"BigBag","FreeBag","SmallBag","LargeBag","Crate","Barrel"})
                    {
                        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Containers/"+type+".prefab");
                        var container=source.GetComponent<GrainContainer>();
                        var slot=layout.FreeSlot(container);
                        if(slot==null || !layout.CanLoadAt(slot,container))throw new Exception("Mixed cargo rejected "+type);
                        var cargo=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
                        cargo.transform.SetParent(slot,false);
                    }
                    report.AppendLine("PASS "+files[i]+": lengthwise pairs and mixed BigBag/FreeBag/SmallBag/LargeBag/Crate/Barrel allocation.");
                    report.AppendLine($"PASS {files[i]}: {smallSlots.Count} FreeBag positions / {bigSlots.Count} BigBag positions; empty serialized cache rebuilt.");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/VehicleCargoPreviewColor.shader");
            var material=new Material(shader);
            try
            {
                ShaderUtil.CompilePass(material,0,true);
                foreach(var message in ShaderUtil.GetShaderMessages(shader))
                    if(message.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) throw new Exception(message.message);
                report.AppendLine("PASS neutral material-color preview shader: no compiler errors.");
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
            Debug.Log(report.ToString());
        }
        catch(Exception exception) { report.AppendLine("FAIL "+exception);Debug.LogException(exception); }
        Directory.CreateDirectory("output/CargoPreview");
        File.WriteAllText("output/CargoPreview/validation.txt",report.ToString());
    }
}
