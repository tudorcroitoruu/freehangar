using System;using System.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
public static class SetupVacuums {
 public static object Run(){
 if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
 var p=UnityEngine.Object.FindFirstObjectByType<PlayerController>();var hands=p.HeldInstrument;var models=hands.Find("Item In Hands");
 var physics=hands.GetComponentInChildren<ShovelGrainPhysics>(true);var data=new SerializedObject(physics);
 var kernel=(Mesh)data.FindProperty("kernelMesh").objectReferenceValue;var grainMat=(Material)data.FindProperty("kernelMaterial").objectReferenceValue;
 var fillMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/GrainPrototype/Vacuum grain.mat");if(fillMaterial==null){fillMaterial=new Material(Shader.Find("HDRP/Lit"));fillMaterial.SetColor("_BaseColor",new Color(.8f,.48f,.045f));fillMaterial.SetFloat("_Smoothness",.18f);var old=hands.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Shovel grain load").sharedMaterial;if(old.HasProperty("_BaseColorMap"))fillMaterial.SetTexture("_BaseColorMap",old.GetTexture("_BaseColorMap"));AssetDatabase.CreateAsset(fillMaterial,"Assets/GrainPrototype/Vacuum grain.mat");}
 for(int level=5;level<=6;level++){
 var root=models.Find("LVL_"+level);var v=root.GetComponent<VacuumTool>()??root.gameObject.AddComponent<VacuumTool>();v.capacityKg=level==5?30:50;v.suctionKgPerSecond=level==5?8:12;v.kernelMesh=kernel;v.kernelMaterial=grainMat;
 Transform cylinder=root.Find(level==5?"casing":"DustCylinder_LowPoly");v.dustTray=root.Find(level==5?"casing":"DustTray_LowPoly");
 var nozzle=new GameObject("Vacuum suction mouth").transform;nozzle.SetParent(level==5?cylinder:root.Find("Extension_LowPoly"),false);
 nozzle.localPosition=level==5?new Vector3(.42f,0,.06f):new Vector3(0,0,.77f);v.nozzle=nozzle;
 var fill=new GameObject("Visible collected grain",typeof(MeshFilter),typeof(MeshRenderer)).transform;fill.SetParent(v.dustTray,false);
 if(level==5){fill.localPosition=new Vector3(.157f,0,-.015f);fill.localRotation=Quaternion.LookRotation(Vector3.right,Vector3.back);v.fillRadius=.047f;v.fillLength=.27f;}
 else {fill.position=cylinder.TransformPoint(new Vector3(0,.01f,.17f));fill.rotation=cylinder.rotation*Quaternion.Euler(90,0,0);v.fillRadius=.13f;v.fillLength=.29f;v.uprightCylinder=true;}
 fill.GetComponent<MeshRenderer>().sharedMaterial=fillMaterial;v.fillFrame=fill;
 v.trayRemoval=v.dustTray.parent.InverseTransformVector(p.characterCamera.transform.right*-.22f-p.characterCamera.transform.up*.18f);
 v.trayTilt=new Vector3(0,0,85);EditorUtility.SetDirty(v);
 }
 var selector=hands.GetComponent<HeldGrainToolLevels>()??hands.gameObject.AddComponent<HeldGrainToolLevels>();selector.levels=Enumerable.Range(1,6).Select(i=>models.Find("LVL_"+i).gameObject).ToArray();EditorUtility.SetDirty(selector);
 var settings=Resources.Load<ShovelUpgradeSettings>(ShovelUpgradeSettings.ResourcePath);if(settings.shovels.Length==4){var list=settings.shovels.ToList();list.Add(new ShovelUpgradeSettings.ShovelType{name="Grain Vacuum",capacityKg=30,capacityPerUpgrade=0,weightKg=.35f,weightReductionPerUpgrade=.02f,prices=new[]{12000,15000,18000,22000}});list.Add(new ShovelUpgradeSettings.ShovelType{name="High Capacity Vacuum",capacityKg=50,capacityPerUpgrade=0,weightKg=.3f,weightReductionPerUpgrade=.02f,prices=new[]{25000,30000,35000,40000}});settings.shovels=list.ToArray();EditorUtility.SetDirty(settings);}
 HeldToolCameraSetup.Setup();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(hands.gameObject.scene);
 return new{levels=selector.levels.Length,capacity5=30,capacity6=50,kernel=kernel.name,material=grainMat.name};
 }
}
