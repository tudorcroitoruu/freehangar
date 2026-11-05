using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapPickupIdentitySetup
{
    [InitializeOnLoadMethod] static void RequestedRun() => EditorApplication.delayCall += () =>
    {
        if (!File.Exists("output/fix-map-pickups.request")) return;
        File.Delete("output/fix-map-pickups.request");
        Apply();
    };
    [MenuItem("Tools/Grain/Fix duplicated map pickup IDs")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "Gameplay") return;
        var scene = SceneManager.GetActiveScene();
        var report = new StringBuilder();
        var finds = Object.FindObjectsByType<BuriedFind>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(f=>f.gameObject.scene==scene).ToArray();
        foreach (var group in finds.GroupBy(f=>f.FindId).Where(g=>g.Count()>1))
        {
            // Keep the original object's ID so existing collection saves still apply.
            foreach (var find in group.OrderBy(f=>f.name.Contains("(") ? 1 : 0).ThenBy(f=>f.name).Skip(1))
            {
                if (find.FindKind != BuriedFind.Kind.CashCache && find.FindKind != BuriedFind.Kind.PerkBottle && find.FindKind != BuriedFind.Kind.InfiniteStamina && find.FindKind != BuriedFind.Kind.UpgradeVoucher) continue;
                var data = new SerializedObject(find);
                data.FindProperty("findId").stringValue = group.Key + ".copy." + GlobalObjectId.GetGlobalObjectIdSlow(find).targetObjectId;
                data.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(find);
                report.AppendLine("Independent pickup: " + find.name);
            }
        }
        foreach (var find in finds)
        {
            var data = new SerializedObject(find);
            var pile = data.FindProperty("coveringPile").objectReferenceValue as GrainPile;
            if (!pile) continue;
            Vector3 local = pile.transform.InverseTransformPoint(find.transform.position);
            if (Mathf.Abs(local.x) <= pile.Size * .5f && Mathf.Abs(local.z) <= pile.Size * .5f) continue;
            data.FindProperty("coveringPile").objectReferenceValue = null;
            data.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(find);
            report.AppendLine("Removed unrelated burial pile: " + find.name);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("output/map-pickups-fixed.txt", report.ToString());
    }
}
