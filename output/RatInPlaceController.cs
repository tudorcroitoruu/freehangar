using UnityEngine;using UnityEditor;using UnityEditor.Animations;using UnityEditor.SceneManagement;using System.Linq;
public static class RatInPlaceController {
 static string folder="Assets/Imported/Polygonmaker/Wild Life Series/Wild Life - Random Beasts/Rat/Animations/";
 static AnimationClip Clip(string file,bool loop){
  string path=folder+"Anim_Rat@"+file+".FBX";
  var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  var clips=importer.clipAnimations;if(clips.Length==0)clips=importer.defaultClipAnimations;
  foreach(var c in clips){c.lockRootRotation=true;c.lockRootHeightY=true;c.lockRootPositionXZ=true;c.keepOriginalOrientation=true;c.keepOriginalPositionY=true;c.keepOriginalPositionXZ=true;c.loopTime=loop;c.loopPose=loop;}
  importer.clipAnimations=clips;importer.SaveAndReimport();
  return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(x=>!x.name.StartsWith("__preview"));
 }
 public static object Run(){
 if(EditorApplication.isPlaying)throw new System.Exception("Edit mode required");
 var idle=Clip("idle",true);var walk=Clip("walk-inplace",true);var run=Clip("run-inplace",true);var eat=Clip("idle break",false);
 string path="Assets/NewMap/Discoveries/Pest rat in place.controller";
 if(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path)!=null)throw new System.Exception("Controller already exists");
 var controller=AnimatorController.CreateAnimatorControllerAtPath(path);
 controller.AddParameter("Speed",AnimatorControllerParameterType.Float);controller.AddParameter("IdleBreak",AnimatorControllerParameterType.Trigger);
 var sm=controller.layers[0].stateMachine;
 var tree=new BlendTree{name="In place locomotion",blendParameter="Speed",useAutomaticThresholds=false};
 AssetDatabase.AddObjectToAsset(tree,controller);tree.AddChild(idle,0);tree.AddChild(walk,.6f);tree.AddChild(run,1);
 var locomotion=sm.AddState("Blend Tree");locomotion.motion=tree;sm.defaultState=locomotion;
 var feeding=sm.AddState("idlebreak");feeding.motion=eat;
 var transition=sm.AddAnyStateTransition(feeding);transition.AddCondition(AnimatorConditionMode.If,0,"IdleBreak");transition.duration=.12f;transition.canTransitionToSelf=false;
 var exit=feeding.AddTransition(locomotion);exit.hasExitTime=true;exit.exitTime=.95f;exit.duration=.12f;
 var d=Object.FindFirstObjectByType<GrainPestDirector>();d.ratAnimation=controller;EditorUtility.SetDirty(d);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveOpenScenes();
 return "Baked root transforms on FBX clips, enabled looping, assigned dedicated idle/walk/run/eat controller";
 }}
