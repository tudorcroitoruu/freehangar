using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class BuildVisibilityAudit
{
    [InitializeOnLoadMethod] static void Run() => EditorApplication.delayCall += () =>
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "Gameplay") return;
        var report = new StringBuilder();
        var finds = Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var f in finds)
        {
            var so = new SerializedObject(f);
            var pile = so.FindProperty("coveringPile").objectReferenceValue as GrainPile;
            Vector3 p = pile ? pile.transform.InverseTransformPoint(f.transform.position) : Vector3.zero;
            report.AppendLine($"FIND {f.name} id={f.FindId} duplicate={finds.Count(x=>x.FindId==f.FindId)} pos={f.transform.position} pile={pile?.name} local={p} size={pile?.Size} active={f.gameObject.activeInHierarchy}");
        }
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!r.name.ToLowerInvariant().Contains("flower")) continue;
            bool hidden = SceneVisibilityManager.instance.IsHidden(r.gameObject);
            float ground = Terrain.activeTerrain ? Terrain.activeTerrain.SampleHeight(r.bounds.center) + Terrain.activeTerrain.transform.position.y : 0;
            report.AppendLine($"FLOWER {GlobalObjectId.GetGlobalObjectIdSlow(r)} {r.name} pos={r.transform.position} minY={r.bounds.min.y} terrain={ground} hidden={hidden} active={r.gameObject.activeInHierarchy} shader={r.sharedMaterial?.shader.name}");
        }
        File.WriteAllText("output/build-visibility-audit.txt", report.ToString());
    };
}
