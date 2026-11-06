using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene collectable lists and saved progress, independent of other foundSecrets entries.</summary>
[DisallowMultipleComponent]
public sealed class CollectablesManager : MonoBehaviour
{
    [Serializable]
    public sealed class Collection
    {
        public BuriedFind.Kind kind;
        public string title;
        public Sprite image;
        public List<BuriedFind> collectables = new List<BuriedFind>();

        public Collection(BuriedFind.Kind kind, string title) { this.kind = kind; this.title = title; }

        public void GetProgress(PlayerData data, out int found, out int total)
        {
            found = total = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var saved = new HashSet<string>(data?.foundSecrets ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (var item in collectables)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.FindId) || !ids.Add(item.FindId)) continue;
                total++;
                if (saved.Contains(item.FindId)) found++;
            }
        }
    }

    [SerializeField] private List<Collection> collections = new List<Collection>
    {
        new Collection(BuriedFind.Kind.Key, "Chest keys"),
        new Collection(BuriedFind.Kind.CashChest, "Chests"),
        new Collection(BuriedFind.Kind.UpgradeVoucher, "Upgrade vouchers"),
        new Collection(BuriedFind.Kind.InfiniteStamina, "Stamina bottles"),
        new Collection(BuriedFind.Kind.CashCache, "Cash caches"),
        new Collection(BuriedFind.Kind.Axe, "Axes"),
        new Collection(BuriedFind.Kind.Hammer, "Hammers"),
        new Collection(BuriedFind.Kind.HangarKey, "Hangar keys"),
        new Collection(BuriedFind.Kind.Broom, "Brooms"),
        new Collection(BuriedFind.Kind.VehicleKey, "Vehicle keys"),
        new Collection(BuriedFind.Kind.PerkBottle, "Perk bottles"),
        new Collection(BuriedFind.Kind.Pistol, "Pistols")
    };
    [Tooltip("Include all BuriedFind objects in this scene, including inactive and runtime spawned objects.")]
    [SerializeField] private bool discoverSceneCollectables = true;
    [SerializeField, Min(.1f)] private float popupSeconds = 3f;

    public IReadOnlyList<Collection> Collections => collections;

    private void OnEnable() => BuriedFind.Collected += OnCollected;
    private void OnDisable() => BuriedFind.Collected -= OnCollected;
    private void Start() { if (discoverSceneCollectables) PopulateFromScene(); }

    [ContextMenu("Populate lists from scene")]
    public void PopulateFromScene()
    {
        foreach (var collection in collections) collection.collectables.RemoveAll(item => item == null);
        foreach (var item in FindObjectsByType<BuriedFind>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item.gameObject.scene != gameObject.scene) continue;
            var collection = collections.Find(entry => entry.kind == item.FindKind);
            if (collection != null && !collection.collectables.Contains(item)) collection.collectables.Add(item);
        }
    }

    private void OnCollected(BuriedFind item, PlayerData data)
    {
        if (item == null || item.gameObject.scene != gameObject.scene) return;
        // Refresh here as well: vehicle keys can be spawned after Start.
        if (discoverSceneCollectables) PopulateFromScene();
        foreach (var collection in collections)
        {
            if (!collection.collectables.Exists(entry => entry != null && entry.FindId == item.FindId)) continue;
            collection.GetProgress(data, out int found, out int total);
            GameplayCanvas.Instance?.ShowAchievement(collection.image, $"{collection.title} {found}/{total}", popupSeconds);
            break;
        }
    }
}
