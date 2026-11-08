using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Exercises the authored Gameplay campaign, without opening or changing the player's scene/save.</summary>
public class GoalCampaignTests
{
    [Test]
    public void MovedHangarKeyMeshRetainsPickupColliderAtVisibleKey()
    {
        var root = new GameObject("Offset hangar key regression"); root.SetActive(false);
        try
        {
            var find = root.AddComponent<BuriedFind>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(BuriedFind).GetField("kind", flags).SetValue(find, BuriedFind.Kind.HangarKey);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Combined key mesh"; mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(3.8f, .55f, 12.03f);
            mesh.transform.localScale = new Vector3(.44f, 1.14f, .063f);
            UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
            root.SetActive(true);
            typeof(BuriedFind).GetMethod("Awake", flags).Invoke(find, null);
            typeof(BuriedFind).GetMethod("SetRevealed", flags).Invoke(find, new object[] { true });
            Physics.SyncTransforms();
            var collider = root.GetComponent<BoxCollider>();
            Assert.That(Vector3.Distance(collider.bounds.center, mesh.GetComponent<Renderer>().bounds.center), Is.LessThan(.001f));
            Assert.IsTrue(collider.Raycast(new Ray(mesh.transform.position - Vector3.forward * 2, Vector3.forward), out _, 4));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private GameObject owner;
    private GoalsManager manager;
    private PlayerData data;
    private readonly List<string> completed = new();
    public static string LastCampaignTrace { get; private set; }

    public static GoalSettings[] ReadSceneGoals()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameCanvas.prefab");
        var source = prefab.GetComponentInChildren<GoalsManager>(true);
        Assert.IsNotNull(source, "GameCanvas needs a GoalsManager.");
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long id);
        var field = typeof(GoalsManager).GetField("allGoalSettings", BindingFlags.Instance | BindingFlags.NonPublic);
        var goals = ((GoalSettings[])field.GetValue(source))
            .Select(g => JsonUtility.FromJson<GoalSettings>(JsonUtility.ToJson(g))).ToList();
        string scene = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        string prefix = @"- target: \{fileID: " + id + ", guid: " + guid + @", type: 3\}\r?\n[ \t]+propertyPath: ";
        var size = Regex.Match(scene, prefix + @"allGoalSettings.Array.size\r?\n[ \t]+value: (\d+)");
        if (size.Success)
        {
            int count = int.Parse(size.Groups[1].Value);
            while (goals.Count < count) goals.Add(new GoalSettings());
            if (goals.Count > count) goals.RemoveRange(count, goals.Count - count);
        }
        foreach (Match match in Regex.Matches(scene, prefix +
                     @"allGoalSettings.Array.data\[(\d+)\].(\w+)\r?\n[ \t]+value: ([^\r\n]*)"))
        {
            int index = int.Parse(match.Groups[1].Value);
            if (index >= goals.Count) continue; // Unity ignores stale overrides outside the array.
            var property = typeof(GoalSettings).GetField(match.Groups[2].Value);
            if (property == null || property.FieldType == typeof(GameObject)) continue;
            string value = match.Groups[3].Value;
            object parsed = property.FieldType.IsEnum ? Enum.ToObject(property.FieldType, int.Parse(value)) :
                property.FieldType == typeof(bool) ? value == "1" :
                Convert.ChangeType(value, property.FieldType, CultureInfo.InvariantCulture);
            property.SetValue(goals[index], parsed);
        }
        foreach (var goal in goals) goal.navigationIndicator = null;
        return goals.ToArray();
    }

    [TearDown]
    public void TearDown()
    {
        if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
    }

    [Test]
    public void AuthoredGoalsHaveCompatibleGameplayReports()
    {
        var produced = new HashSet<GoalType>
        {
            GoalType.SackPickedUp, GoalType.SackPlaced, GoalType.SackFilled, GoalType.SackClosed,
            GoalType.SackLoaded, GoalType.BagsFilled, GoalType.GrainCollected, GoalType.GrainMoved,
            GoalType.GrainLoaded, GoalType.GrainSold, GoalType.MoneyEarned, GoalType.BuyerReached,
            GoalType.ShovelEquipped, GoalType.UpgradePurchased, GoalType.HangarKeyCollected,
            GoalType.HangarUnlocked, GoalType.HangarCleared
        };
        var goals = ReadSceneGoals();
        Assert.That(goals.Select(g => g.goalId).Distinct().Count(), Is.EqualTo(goals.Length));
        foreach (var goal in goals)
        {
            Assert.That(produced.Contains(goal.goalType), Is.True, goal.goalId + " has no gameplay producer.");
            Assert.That(goal.goalAmount, Is.GreaterThan(0), goal.goalId);
            if (GoalChecker.IsGlobalGrainQuantity(goal.goalType))
                Assert.That(goal.progressMode, Is.Not.EqualTo(GoalProgressMode.CurrentValue), goal.goalId);
            if (goal.goalType == GoalType.UpgradePurchased)
            {
                Assert.That(goal.hangarId, Is.Zero, goal.goalId);
                Assert.IsTrue(new[] { GoalUpgradeType.Shovel, GoalUpgradeType.LargerSack,
                    GoalUpgradeType.LargerTruck, GoalUpgradeType.Stamina }.Contains(goal.upgradeType), goal.goalId);
            }
        }
    }

    [Test]
    public void ScenePilesKeysAndDoorsHaveMatchingHangarIdentities()
    {
        string scene = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        var blocks = Regex.Matches(scene, @"(?ms)^--- !u!\d+ &(\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)")
            .Cast<Match>().ToDictionary(m => long.Parse(m.Groups[1].Value), m => m.Value);
        long Reference(string block, string name) => long.Parse(Regex.Match(block,
            @"(?m)^  " + name + @": \{fileID: (\d+)").Groups[1].Value);
        int Number(string block, string name) => int.Parse(Regex.Match(block,
            @"(?m)^  " + name + @": (\d+)").Groups[1].Value);
        var contexts = blocks.Values.Where(b => b.Contains("guid: eb8c8fd97f6b7fa45b024eed93702617"))
            .ToDictionary(b => Reference(b, "m_GameObject"), b => Number(b, "hangarId"));
        var transforms = blocks.Where(b => (b.Value.Contains("\nTransform:") || b.Value.Contains("\nRectTransform:")) &&
                                          b.Value.Contains("  m_GameObject:"))
            .ToDictionary(b => Reference(b.Value, "m_GameObject"), b => b.Key);
        int Resolve(long gameObject)
        {
            while (gameObject != 0)
            {
                if (contexts.TryGetValue(gameObject, out int hangar) && hangar > 0) return hangar;
                if (!transforms.TryGetValue(gameObject, out long transform)) break;
                long parent = Reference(blocks[transform], "m_Father");
                gameObject = parent == 0 ? 0 : Reference(blocks[parent], "m_GameObject");
            }
            return 0;
        }
        var problems = new List<string>();
        var piles = blocks.Values.Where(b => b.Contains("guid: def2a2e4d587bab4d809a66ba02b6bf0")).ToArray();
        Assert.That(piles.Length, Is.EqualTo(5));
        var pileHangars = new List<int>();
        foreach (var pile in piles)
        {
            long go = Reference(pile, "m_GameObject");
            int hangar = Resolve(go);
            pileHangars.Add(hangar);
            if (hangar < 1 || hangar > 5)
                problems.Add("Pile lacks GoalContext: " + Regex.Match(blocks[go], @"m_Name: ([^\r\n]+)").Groups[1].Value);
        }
        var keys = blocks.Values.Where(b => b.Contains("guid: 9e953992af4286a48902ae0bb6c42048") &&
                     Regex.IsMatch(b, @"(?m)^  kind: 7\r?$" )).ToArray();
        Assert.That(keys.Select(k => Number(k, "targetHangar")).OrderBy(h => h), Is.EqualTo(new[] { 2, 3, 4, 5 }));
        foreach (var key in keys)
        {
            int target = Number(key, "targetHangar");
            long pile = Reference(key, "coveringPile");
            if (!key.Contains("findId: " + HangarKeyDoor.KeyFor(target)) || pile == 0 ||
                Resolve(Reference(blocks[pile], "m_GameObject")) != target - 1)
                problems.Add("Hangar " + target + " key must be in the preceding hangar's pile.");
        }
        var doors = blocks.Values.Where(b => b.Contains("guid: 30ced2e7183f0bd4daa8283902d9f25b")).ToArray();
        Assert.That(doors.Select(d => Number(d, "hangarNumber")).OrderBy(h => h), Is.EqualTo(new[] { 2, 3, 4, 5 }));
        foreach (var door in doors)
        {
            int target = Number(door, "hangarNumber");
            long visibility = Reference(door, "grainVisibility");
            if (visibility == 0 || Resolve(Reference(blocks[visibility], "grainRoot")) != target)
                problems.Add("Hangar " + target + " door is not linked to its own grain pile.");
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
        Assert.That(pileHangars.OrderBy(h => h), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WholeCampaignCompletesUsingAvailableGrain(bool reloadBetweenHangars)
    {
        var goals = ReadSceneGoals();
        owner = new GameObject("Isolated goal campaign") { hideFlags = HideFlags.HideAndDontSave };
        owner.SetActive(false);
        manager = owner.AddComponent<GoalsManager>();
        typeof(GoalsManager).GetField("goalAchievedDelay", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, 0f);
        typeof(GoalsManager).GetField("allGoalSettings", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, goals);
        data = new PlayerData();
        completed.Clear();
        manager.GoalCompleted += g => completed.Add(g.goalId);
        manager.Initialize(data);
        Report(GoalType.SackPickedUp);
        Report(GoalType.SackPlaced);
        Report(GoalType.SackFilled, 15, value: true, grain: GrainType.Corn);
        Report(GoalType.SackClosed);
        Report(GoalType.SackLoaded);
        Report(GoalType.BuyerReached, value: true);
        LoadAndSell(GrainType.Corn, 15);
        data.shovelUpgradeLevel = 1;
        Report(GoalType.UpgradePurchased, value: true, upgrade: GoalUpgradeType.Shovel);
        data.purchasedGoalUpgrades.Add(GoalUpgradeType.LargerSack);
        Report(GoalType.UpgradePurchased, value: true, upgrade: GoalUpgradeType.LargerSack);
        HarvestHangar(1, 15);
        ExpectCurrent("h2_collect_key");
        CollectKey(2);
        ClearHangar(1, 100);
        UnlockHangar(2);
        if (reloadBetweenHangars) Reload();

        HarvestHangar(2);
        ExpectCurrent("upgrade_larger_truck");
        data.unlockedVehicles.Add("motoroller");
        data.purchasedGoalUpgrades.Add(GoalUpgradeType.LargerTruck);
        Report(GoalType.UpgradePurchased, value: true, upgrade: GoalUpgradeType.LargerTruck);
        CollectKey(3);
        ClearHangar(2, 100);
        UnlockHangar(3);
        if (reloadBetweenHangars) Reload();

        data.purchasedGoalUpgrades.Add(GoalUpgradeType.Stamina);
        Report(GoalType.UpgradePurchased, value: true, upgrade: GoalUpgradeType.Stamina);

        HarvestHangar(3);
        ExpectCurrent("h3_clear");
        ClearHangar(3, 100);
        CollectKey(4);
        UnlockHangar(4);
        if (reloadBetweenHangars) Reload();

        HarvestHangar(4);
        ExpectCurrent("h4_clear_98");
        ClearHangar(4, 98);
        CollectKey(5);
        UnlockHangar(5);
        if (reloadBetweenHangars) Reload();

        HarvestHangar(5);
        ExpectCurrent("h5_find_out_what_is_behind_Door");
        ClearHangar(5, 98);
        Assert.IsTrue(manager.AllGoalsCompleted, manager.CurrentGoal?.goalId);
        Assert.That(completed, Is.EqualTo(goals.Select(g => g.goalId)), "Each goal must complete once, in order.");
        Reload();
        Assert.IsTrue(manager.AllGoalsCompleted);
        Assert.That(completed.Count, Is.EqualTo(goals.Length), "Reload must not repeat completion rewards.");
        LastCampaignTrace = string.Join("\n", completed.Select((id, i) => $"{i + 1}. PASS {id}"));
    }

    private void Report(GoalType type, float amount = 1, int hangar = 0, bool value = false,
        GrainType? grain = null, GoalUpgradeType upgrade = GoalUpgradeType.None) =>
        manager.ProcessAction(new GoalAction(type, amount, hangar, grain, upgrade, value));

    private void LoadAndSell(GrainType grain, float kilograms)
    {
        if (kilograms <= 0) return;
        Report(GoalType.GrainCollected, kilograms, grain: grain);
        Report(GoalType.GrainMoved, kilograms, grain: grain);
        Report(GoalType.GrainLoaded, kilograms, grain: grain);
        Report(GoalType.GrainSold, kilograms, grain: grain);
        var economy = GrainEconomySettings.Default;
        Assert.IsTrue(economy.TryGetGrain(grain, out var settings));
        int cash = (int)(kilograms * settings.grainPrice);
        data.cash += cash;
        Report(GoalType.MoneyEarned, cash);
    }

    private void HarvestHangar(int number, float alreadySoldCorn = 0)
    {
        var economy = GrainEconomySettings.Default;
        var stock = ReadAuthoredStock(number);
        for (int i = 0; i < GrainCargo.TypeCount; i++)
        {
            // Leave 22% for pests, cleanup and handling losses, rather than assuming a perfect harvest.
            float kg = (float)(stock.physicalGrainKg[i] / economy.ConsumeMultiplier * .78);
            LoadAndSell((GrainType)i, kg - (i == 0 ? alreadySoldCorn : 0));
        }
    }

    private static GrainEconomySettings.HangarStock ReadAuthoredStock(int number)
    {
        string scene = File.ReadAllText("Assets/Scenes/Gameplay.unity");
        var blocks = Regex.Matches(scene, @"(?ms)^--- !u!\d+ &(\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)")
            .Cast<Match>().ToDictionary(m => long.Parse(m.Groups[1].Value), m => m.Value);
        long Reference(string block, string field) => long.Parse(Regex.Match(block,
            @"(?m)^  " + field + @": \{fileID: (\d+)").Groups[1].Value);
        var contexts = blocks.Values.Where(b => b.Contains("guid: eb8c8fd97f6b7fa45b024eed93702617"))
            .ToDictionary(b => Reference(b, "m_GameObject"), b => int.Parse(Regex.Match(b, @"hangarId: (\d+)").Groups[1].Value));
        var transforms = blocks.Values.Where(b => b.Contains("\nTransform:") && b.Contains("  m_GameObject:"))
            .ToDictionary(b => Reference(b, "m_GameObject"));
        int Hangar(string pile)
        {
            long go = Reference(pile, "m_GameObject");
            while (go != 0)
            {
                if (contexts.TryGetValue(go, out int hangar) && hangar > 0) return hangar;
                long parent = Reference(transforms[go], "m_Father");
                go = parent == 0 ? 0 : Reference(blocks[parent], "m_GameObject");
            }
            return 0;
        }
        string pile = blocks.Values.Single(b => b.Contains("guid: def2a2e4d587bab4d809a66ba02b6bf0") && Hangar(b) == number);
        double mass = double.Parse(Regex.Match(pile, @"(?m)^  kilograms: ([^\r\n]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        var stock = new GrainEconomySettings.HangarStock { hangarId = number, physicalGrainKg = new double[GrainCargo.TypeCount] };
        string layout = pile.Substring(0, pile.IndexOf("  previewClearedPercent:", StringComparison.Ordinal));
        foreach (Match region in Regex.Matches(layout, @"(?ms)^  - percent: ([\d.]+)\r?\n(.*?)(?=^  - percent:|\z)"))
        {
            var grains = Regex.Matches(region.Groups[2].Value, @"grain: (\d+)\r?\n\s+weight: ([\d.]+)").Cast<Match>().ToArray();
            double totalWeight = grains.Sum(g => double.Parse(g.Groups[2].Value, CultureInfo.InvariantCulture));
            Assert.That(totalWeight, Is.GreaterThan(0));
            double fraction = double.Parse(region.Groups[1].Value, CultureInfo.InvariantCulture) / 100;
            foreach (var grain in grains)
                stock.physicalGrainKg[int.Parse(grain.Groups[1].Value)] += mass * fraction *
                    double.Parse(grain.Groups[2].Value, CultureInfo.InvariantCulture) / totalWeight;
        }
        Assert.That(stock.physicalGrainKg.Sum(), Is.EqualTo(mass).Within(.01), "Scene crop regions must cover the whole pile.");
        return stock;
    }

    private void CollectKey(int number)
    {
        data.foundSecrets = data.foundSecrets.Concat(new[] { HangarKeyDoor.KeyFor(number) }).ToArray();
        Report(GoalType.HangarKeyCollected, hangar: number, value: true);
    }

    private void UnlockHangar(int number)
    {
        Assert.IsTrue(data.TryOpenHangar(number));
        Report(GoalType.HangarUnlocked, hangar: number, value: true);
    }

    private void ClearHangar(int number, float percent)
    {
        data.TryRecordHangarClearance(number, percent);
        Report(GoalType.HangarCleared, percent, number, true);
    }

    private void ExpectCurrent(string id) => Assert.That(manager.CurrentGoal?.goalId, Is.EqualTo(id));

    private void Reload()
    {
        data = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        manager.Initialize(data);
    }
}
