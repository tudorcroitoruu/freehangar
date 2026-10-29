using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
public static class VerifyShovelPoses {
 public static object Run() {
  if(UnityEditor.EditorApplication.isPlaying) throw new Exception("Requires Edit mode");
  var source=UnityEngine.Object.FindFirstObjectByType<ShovelTool>(FindObjectsInactive.Include);
  var root=new GameObject("Temporary pose verification");
  var copy=UnityEngine.Object.Instantiate(source.gameObject,root.transform);
  try {
   var tool=copy.GetComponent<ShovelTool>(); var levels=copy.GetComponent<HeldGrainToolLevels>();
   var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   typeof(ShovelTool).GetMethod("Awake",flags).Invoke(tool,null);
   var results=new List<string>();
   for(int level=1;level<=3;level++) {
    typeof(HeldGrainToolLevels).GetField("selected",flags).SetValue(levels,level);
    for(int i=0;i<levels.levels.Length;i++)levels.levels[i].SetActive(i==level);
    var authored=levels.levels[level].transform.localRotation;
    foreach(float pitch in new[]{-60f,0f,60f})foreach(float mass in new[]{0f,10f}) {
     root.transform.rotation=Quaternion.Euler(pitch,0,0);tool.Cargo.Clear();tool.Cargo.Add(GrainType.Corn,mass);
     tool.ComposePresentation(1,Vector2.zero,0,Vector3.zero,out var offset,out var pose);
     if(Quaternion.Angle(pose,Quaternion.identity)>.01f || Quaternion.Angle(authored,levels.levels[level].transform.localRotation)>.01f)throw new Exception("Pose changed: "+level);
    }
    results.Add("LVL_"+(level+1)+": authored rotation preserved empty/loaded at -60, 0, +60 camera pitch");
   }
   return results;
  } finally {UnityEngine.Object.DestroyImmediate(root);}
 }
}
