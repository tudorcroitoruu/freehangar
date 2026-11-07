using UnityEngine;

/// <summary>Saved first encounter, yard population, and recurring grain raids.</summary>
public sealed class HangarChickenTutorial : MonoBehaviour
{
    public FarmChicken chicken;
    private float nextCheck;
    private float nextRaid;
    private FarmChicken template;
    private readonly System.Collections.Generic.List<FarmChicken> raiders = new();
    private void Start()
    {
        if (!chicken) return;
        // Keep an inactive template alive after the tutorial bird is defeated.
        var storage = new GameObject("Chicken spawn template");
        storage.transform.SetParent(transform);
        storage.SetActive(false);
        template = Instantiate(chicken, storage.transform);
        template.gameObject.SetActive(false);
        nextRaid = Time.time + Random.Range(300f, 720f);
        int count = FindObjectsByType<FarmChicken>(FindObjectsSortMode.None).Length;
        int target = Random.Range(8, 11);
        for (int i = count; i < target; i++) Spawn(null);
    }
    private void TickRaids()
    {
        if (!template || !SaveSystem.savesLoaded || Time.time < nextRaid) return;
        nextRaid = Time.time + Random.Range(300f, 720f);
        raiders.RemoveAll(bird => !bird || bird.IsDead || !CanRaidPile(SaveSystem.Data, bird.feedingPile));
        if (!FarmChicken.AxeDiscovered || Random.value >= .8f || raiders.Count >= 3) return;
        var piles = new System.Collections.Generic.List<GrainPile>();
        foreach (var pile in FindObjectsByType<GrainPile>(FindObjectsSortMode.None))
        {
            if (CanRaidPile(SaveSystem.Data, pile)) piles.Add(pile);
        }
        if (piles.Count == 0) return;
        var selected = piles[Random.Range(0, piles.Count)];
        int count = Mathf.Min(Random.Range(1, 4), 3 - raiders.Count);
        for (int i = 0; i < count; i++)
        {
            var bird = Spawn(selected);
            if (bird) raiders.Add(bird);
        }
    }
    private FarmChicken Spawn(GrainPile pile)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            Vector3 point = pile ? pile.transform.TransformPoint(new Vector3(
                Random.Range(-pile.Size * .5f, pile.Size * .5f), 0f,
                Random.Range(-pile.Size * .5f, pile.Size * .5f))) :
                new Vector3(Random.Range(template.yard.min.x, template.yard.max.x),
                    template.transform.position.y, Random.Range(template.yard.min.z, template.yard.max.z));
            if (!UnityEngine.AI.NavMesh.SamplePosition(point, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            point = hit.position;
            if (!FarmChicken.IsGrainClearAt(point)) continue;
            if (!FarmChicken.TryGroundPoint(point, out var support) || Mathf.Abs(support.y - point.y) > .15f) continue;
            if (!template.InsideYard(point)) continue;
            if (Physics.CheckCapsule(point + Vector3.up * .3f, point + Vector3.up * .5f, .27f,
                    ~0, QueryTriggerInteraction.Ignore)) continue;
            Vector3 food = default;
            if (pile && (!pile.TryNearestSurface(point + Vector3.up * .25f, 1f, out food) ||
                         pile.SurfaceHeight(point) > point.y + .1f)) continue;
            var bird = Instantiate(template, point, Quaternion.identity, transform);
            bird.feedingPile = pile;
            bird.feedingPoint = food;
            bird.gameObject.SetActive(true);
            return bird;
        }
        return null;
    }
    public static bool CanRaidHangar(PlayerData data, int hangar) => data != null &&
        hangar >= 1 && hangar <= 5 && data.IsHangarOpen(hangar) && data.GetHangarClearedPercent(hangar) < 100f;

    public static bool CanRaidPile(PlayerData data, GrainPile pile)
    {
        if (pile == null || !pile.isActiveAndEnabled || pile.IsDroppedPile || pile.CollectibleKilograms <= .001) return false;
        GoalContext.Resolve(pile, out int hangar, out _);
        return CanRaidHangar(data, hangar);
    }

    public static bool ShouldAppear(PlayerData data) => CanRaidHangar(data, 2) && !data.hangar2ChickenDefeated;
    private void OnEnable() { if(chicken)chicken.Died+=Complete; }
    private void OnDisable() { if(chicken)chicken.Died-=Complete; }
    private void Update()
    {
        TickRaids();
        if(Time.time<nextCheck || !SaveSystem.savesLoaded || !chicken || chicken.IsDead)return;
        nextCheck=Time.time+.25f;
        bool show=ShouldAppear(SaveSystem.Data) && CanRaidPile(SaveSystem.Data, chicken.feedingPile);
        if(chicken.gameObject.activeSelf!=show)chicken.gameObject.SetActive(show);
    }
    private void Complete()
    {
        if(SaveSystem.Data==null)return;
        SaveSystem.Data.hangar2ChickenDefeated=true;
        SaveSystem.SaveData();
    }
}
