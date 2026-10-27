using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class ApplyCurvedSections {
 public static string Main() {
  GrainSpatialValidation.Run();
  var p=Object.FindObjectsByType<GrainPile>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(p=>p.name.StartsWith("Hangar 3"));
  Undo.RecordObject(p,"Curved grain sections");
  p.ConfigureRegions(GrainSpatialSetup.Preset(3),GrainSpatialField.Arrangement.ChunksAndCurves,.16f);
  EditorUtility.SetDirty(p);PrefabUtility.RecordPrefabInstancePropertyModifications(p);
  p.GetComponent<CornSurfaceKernels>()?.Rebuild();
  EditorSceneManager.MarkSceneDirty(p.gameObject.scene);EditorSceneManager.SaveScene(p.gameObject.scene);
  return "Validated and saved Hangar 3 curved sections";
 }
}

