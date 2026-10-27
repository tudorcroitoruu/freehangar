using System;
using UnityEngine;

/// <summary>Bagged kilograms unlock recipes. Applying a stage preserves pile geometry and held cargo.</summary>
[DisallowMultipleComponent]
public sealed class GrainProgression : MonoBehaviour
{
    [Serializable]
    public sealed class HangarRecipe
    {
        public GrainPile pile;
        public GrainLayer[] layers = { new GrainLayer(52, GrainType.Corn) };
    }
    [Serializable]
    public sealed class Stage
    {
        public string title;
        [Min(0)] public float baggedKilograms;
        public GrainLayer[] defaultRecipe = { new GrainLayer(52, GrainType.Corn) };
        public HangarRecipe[] overrides = Array.Empty<HangarRecipe>();
    }
    [SerializeField] private GrainPile[] piles = Array.Empty<GrainPile>();
    [SerializeField] private Stage[] stages = {
        new Stage { title = "Corn", baggedKilograms = 0 },
        new Stage { title = "Corn + wheat", baggedKilograms = 100, defaultRecipe = new[] { new GrainLayer(52, GrainType.Corn, GrainType.Wheat) } },
        new Stage { title = "Wheat + barley + oats", baggedKilograms = 250, defaultRecipe = new[] { new GrainLayer(52, GrainType.Wheat, GrainType.Barley, GrainType.Oats) } },
        new Stage { title = "Rice and soybeans", baggedKilograms = 500, defaultRecipe = new[] { new GrainLayer(52, GrainType.Corn), new GrainLayer(52, GrainType.Rice, GrainType.Soybeans) } },
        new Stage { title = "Full harvest", baggedKilograms = 1000, defaultRecipe = new[] { new GrainLayer(52, GrainType.Rye, GrainType.Peas, GrainType.SunflowerSeeds), new GrainLayer(52, GrainType.Buckwheat, GrainType.Soybeans) } }
    };
    private PlayerData loadedData;
    private double pendingKg, sessionKg;
    private bool dirty;
    private float saveAt;
    public int CurrentStage { get; private set; } = -1;
    public double BaggedKilograms => sessionKg;
    public string StageTitle => CurrentStage >= 0 && CurrentStage < stages.Length ? stages[CurrentStage].title : "Corn";
    public int StageCount => stages?.Length ?? 0;
    public double NextThreshold => CurrentStage + 1 < StageCount ? stages[CurrentStage + 1].baggedKilograms : -1;

    private void OnEnable() { GrainContainer.GrainDeposited += RecordDeposit; }
    private void Start() { if (CurrentStage < 0) ApplyStage(0); }
    private void OnDisable() { GrainContainer.GrainDeposited -= RecordDeposit; FlushSave(); }
    private void OnApplicationPause(bool paused) { if (paused) FlushSave(); }
    private void OnApplicationQuit() => FlushSave();
    private void RecordDeposit(float kg)
    {
        if (!float.IsFinite(kg) || kg <= 0) return;
        sessionKg += kg;
        if (loadedData == null) pendingKg += kg;
        else { loadedData.grainBaggedKilograms = sessionKg; if (!dirty) saveAt = Time.unscaledTime + 1; dirty = true; }
    }
    private void Update()
    {
        if (SaveSystem.savesLoaded && SaveSystem.Data != null && loadedData != SaveSystem.Data)
        {
            loadedData = SaveSystem.Data;
            double saved = loadedData.grainBaggedKilograms;
            sessionKg = (double.IsFinite(saved) ? Math.Max(0, saved) : 0) + pendingKg;
            loadedData.grainBaggedKilograms = sessionKg;
            if (pendingKg > 0) { dirty = true; saveAt = Time.unscaledTime + 1; }
            pendingKg = 0;
        }
        int next = StageFor(sessionKg);
        if (next != CurrentStage) ApplyStage(next);
        if (dirty && Time.unscaledTime >= saveAt) FlushSave();
    }
    public int StageFor(double baggedKg)
    {
        int result = 0;
        for (int i = 0; i < StageCount; i++) if (stages[i] != null && baggedKg >= stages[i].baggedKilograms) result = i;
        return result;
    }
    public void ApplyStage(int index)
    {
        if (StageCount == 0) return;
        index = Mathf.Clamp(index, 0, StageCount - 1);
        var stage = stages[index];
        if (stage == null) return;
        foreach (var pile in piles ?? Array.Empty<GrainPile>())
        {
            // Spatial hangars own real stored species. A milestone must never transmute their contents.
            if (pile == null || pile.HasSpatialLayout) continue;
            var recipe = stage.defaultRecipe;
            foreach (var item in stage.overrides ?? Array.Empty<HangarRecipe>())
                if (item != null && item.pile == pile) { recipe = item.layers; break; }
            pile.SetRecipe(recipe);
        }
        CurrentStage = index;
    }
    private void FlushSave()
    {
        if (!dirty || loadedData == null || loadedData != SaveSystem.Data || !SaveSystem.savesLoaded) return;
        SaveSystem.SaveData(); dirty = false;
    }
}
