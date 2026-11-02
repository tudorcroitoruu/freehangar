using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class VerifyScaleCoverage {
public static object Main() {
var zone=Object.FindFirstObjectByType<SellingZone>();var area=zone.GetComponent<BoxCollider>();
Undo.RecordObject(area,"Align weighing detection with platform");
area.size=new Vector3(12,area.size.y,7);area.center=new Vector3(-.64f,area.center.y,.08f);
EditorUtility.SetDirty(area);
var bag=GameObject.CreatePrimitive(PrimitiveType.Cube);bag.name="Temporary scale coverage test";bag.transform.localScale=Vector3.one*.2f;
var cargo=bag.AddComponent<GrainContainer>();cargo.SetDemonstrationFill(10);var found=new List<GrainContainer>();int passed=0;
try {
foreach(float x in new[]{-5.5f,0f,5.5f}) foreach(float z in new[]{-3f,0f,3f}) {
bag.transform.position=zone.transform.TransformPoint(new Vector3(area.center.x+x,.6f,area.center.z+z));Physics.SyncTransforms();zone.CollectContainers(found);
if(!found.Contains(cargo))throw new System.Exception("Bag not detected at "+x+","+z);passed++;}
bag.transform.position=zone.transform.TransformPoint(new Vector3(area.center.x+7,.6f,area.center.z));Physics.SyncTransforms();zone.CollectContainers(found);
if(found.Contains(cargo))throw new System.Exception("Off-scale bag included");passed++;
return "PASS: "+passed+" actual-scene container coverage checks";
}finally{Object.DestroyImmediate(bag);}
}}
