using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Places one persistent vehicle-key discovery in each Gameplay hangar.</summary>
public sealed class VehicleKeyBurial : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Gameplay") return;
        var host = new GameObject("Buried vehicle keys");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<VehicleKeyBurial>();
    }
    private System.Collections.IEnumerator Start()
    {
        yield return null; // Pile Awake/OnEnable and authored wall clipping must finish first.
        var settings = Resources.Load<VehicleUpgradeSettings>(VehicleUpgradeSettings.ResourcePath);
        if (settings == null) yield break;
        string[] ids = { "trike", "lawnmower", "pickup", "pickupBig", "pickupLast" };
        var piles = FindObjectsByType<GrainPile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < ids.Length; i++)
        {
            var profile = settings.Find(ids[i]);
            if (profile == null || !profile.requiresKey || AlreadyPlaced(profile.RequiredKeyId)) continue;
            GrainPile covering = null;
            foreach (var pile in piles)
            {
                if (pile.gameObject.scene != gameObject.scene) continue;
                GoalContext.Resolve(pile, out int hangar, out _);
                if (hangar == i + 1) { covering = pile; break; }
            }
            if (covering == null) { Debug.LogWarning($"No hangar {i + 1} pile for {profile.displayName} key.", this); continue; }
            covering.Initialize();
            float bestHeight = 0; Vector3 best = default;
            for (int z = -4; z <= 4; z++) for (int x = -4; x <= 4; x++)
            {
                Vector3 point = covering.transform.TransformPoint(new Vector3(x * covering.Size / 12f, 0, z * covering.Size / 12f));
                if (!covering.IsInsideBoundary(point, .3f)) continue;
                float height = covering.SurfaceHeight(point) - point.y;
                if (height <= bestHeight) continue;
                bestHeight = height; best = point;
            }
            var prefab = Resources.Load<GameObject>("VehicleKeys/" + ids[i]);
            if (prefab == null || bestHeight < .25f) { Debug.LogWarning($"Cannot bury {profile.displayName} key: prefab missing or no suitable grain.", this); continue; }
            best.y += Mathf.Max(.08f, bestHeight * .35f);
            var key = Instantiate(prefab, best, Quaternion.Euler(0, 27f + i * 41f, 0), transform);
            key.GetComponent<BuriedFind>().ConfigureVehicleKey(profile.RequiredKeyId, profile.displayName, covering);
        }
    }
    private bool AlreadyPlaced(string id)
    {
        foreach (var find in FindObjectsByType<BuriedFind>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (find.gameObject.scene == gameObject.scene && find.FindId == id) return true;
        return false;
    }
}
