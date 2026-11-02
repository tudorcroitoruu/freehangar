var report = new System.Text.StringBuilder();
report.AppendLine("Playing=" + UnityEngine.Application.isPlaying);
foreach(var b in UnityEngine.Object.FindObjectsByType<BreakableGrainBarrier>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) {
var s=new UnityEditor.SerializedObject(b);
report.AppendLine(b.name+" scene="+b.gameObject.scene.path+" parent="+b.transform.parent?.name+" pos="+b.transform.position+" rot="+b.transform.eulerAngles+" scale="+b.transform.localScale+" id="+s.FindProperty("barrierId").stringValue+" material="+s.FindProperty("material").enumValueIndex+" pile="+s.FindProperty("coveringPile").objectReferenceValue+" collider="+b.GetComponent<UnityEngine.BoxCollider>().size);
foreach(var r in b.GetComponentsInChildren<UnityEngine.Renderer>(true)) report.AppendLine("  "+r.name+" bounds="+r.bounds+" mesh="+r.GetComponent<UnityEngine.MeshFilter>()?.sharedMesh?.name);
}
foreach(var p in UnityEngine.Object.FindObjectsByType<GrainPile>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) report.AppendLine("PILE "+p.name+" position="+p.transform.position+" bounds="+p.GetComponent<UnityEngine.Renderer>()?.bounds);
System.IO.File.WriteAllText("output/Barriers/before.txt",report.ToString());
return report.ToString();
