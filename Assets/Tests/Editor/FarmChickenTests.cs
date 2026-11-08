using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class FarmChickenTests
{
    [TestCase(97.99f, true)]
    [TestCase(98f, false)]
    [TestCase(99f, false)]
    [TestCase(100f, false)]
    public void RaidsStopAtHangarGoalCompletion(float clearance, bool expected)
    {
        var data = new PlayerData();
        for (int hangar = 1; hangar <= 5; hangar++)
        {
            data.TryOpenHangar(hangar);
            data.TryRecordHangarClearance(hangar, clearance);
            Assert.AreEqual(expected, HangarChickenTutorial.CanRaidHangar(data, hangar), $"Hangar {hangar}");
        }
    }

    private GameObject root;
    private FarmChicken chicken;
    private PlayerData previousData;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [SetUp] public void Setup()
    {
        previousData=SaveSystem.Data;
        SaveSystem.Data=new PlayerData { foundSecrets=new[]{PlayerCombatTools.AxeFindId} };
        root=new GameObject("Chicken test");
        new GameObject("Visual").transform.SetParent(root.transform);
        chicken=root.AddComponent<FarmChicken>();
        chicken.yard=new Bounds(Vector3.zero,Vector3.one*100);
        typeof(FarmChicken).GetMethod("Awake",Private).Invoke(chicken,null);
    }
    [TearDown] public void Cleanup() { Object.DestroyImmediate(root); SaveSystem.Data=previousData; }
    [Test] public void ChickenCanReactToHitsBeforeAxeDiscovery()
    {
        SaveSystem.Data=new PlayerData();
        Assert.IsFalse(FarmChicken.AxeDiscovered);
        Assert.IsTrue(chicken.Hit(3,Vector3.back));
        Assert.AreEqual(97,chicken.health);
        SaveSystem.Data.foundSecrets=new[]{PlayerCombatTools.LegacyAxeFindId};
        Assert.IsTrue(FarmChicken.AxeDiscovered);
    }
    [Test] public void IdleCallsUseExtendedLinearHearingRange()
    {
        var source=(AudioSource)typeof(FarmChicken).GetField("idleAudioSource",Private).GetValue(chicken);
        Assert.AreEqual(28f,source.maxDistance);
        Assert.AreEqual(AudioRolloffMode.Linear,source.rolloffMode);
        Assert.AreEqual(1f,source.spatialBlend);
    }
    [Test] public void HangarTutorialAppearsOnlyAfterOpeningAndStaysCompletedAfterReload()
    {
        var data=new PlayerData();
        Assert.IsFalse(HangarChickenTutorial.ShouldAppear(data));
        data.openedHangars.Add(2);
        Assert.IsTrue(HangarChickenTutorial.ShouldAppear(data));
        data.hangar2ChickenDefeated=true;
        var restored=JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.IsFalse(HangarChickenTutorial.ShouldAppear(restored));
    }
    private void Strike(float damage)
    {
        typeof(FarmChicken).GetField("nextHit",Private).SetValue(chicken,-1f);
        Assert.IsTrue(chicken.Hit(damage,Vector3.back));
    }

    [Test]
    public void ChickenThreatRequiresOpenHangarAndRemainingGrain()
    {
        var pileObject = new GameObject("Raid grain");
        try
        {
            pileObject.SetActive(false);
            var context = pileObject.AddComponent<GoalContext>();
            typeof(GoalContext).GetField("hangarId", Private).SetValue(context, 2);
            var pile = pileObject.AddComponent<GrainPile>();
            typeof(GrainPile).GetField("initialHeight", Private).SetValue(pile, 0f);
            pile.Initialize();
            pileObject.SetActive(true);
            var empty = pile.CaptureSave();
            chicken.feedingPile = pile;
            SaveSystem.Data.openedHangars.Add(2);
            Assert.IsFalse(chicken.IsGrainThreat, "An open but empty hangar cannot be raided.");
            var grain = new GrainCargo();
            grain.Add(GrainType.Corn, 10f);
            Assert.IsTrue(pile.Deposit(Vector3.zero, grain));
            Assert.IsTrue(chicken.IsGrainThreat);
            SaveSystem.Data.openedHangars.Remove(2);
            Assert.IsFalse(chicken.IsGrainThreat, "A closed hangar cannot be raided.");
            SaveSystem.Data.openedHangars.Add(2);
            pile.RestoreSave(empty);
            Assert.IsFalse(chicken.IsGrainThreat, "An existing raider must stop when the pile becomes empty.");
        }
        finally { Object.DestroyImmediate(pileObject); }
    }
    [Test] public void FourAxeHitsKillAndFurtherHitsAreRejected()
    {
        for(int i=0;i<3;i++) { Strike(28);Assert.IsFalse(chicken.IsDead); }
        Strike(28);Assert.IsTrue(chicken.IsDead);Assert.IsFalse(chicken.Hit(28,Vector3.zero));
    }
    [Test] public void BroomDoesVeryLittleDamageAndRepeatedContactIsDebounced()
    {
        Strike(3);Assert.AreEqual(97,chicken.health);
        Assert.IsFalse(chicken.Hit(3,Vector3.zero));Assert.AreEqual(97,chicken.health);
    }
    [Test] public void YardRejectsOutsidePointsRegardlessOfHeight()
    {
        Assert.IsTrue(chicken.InsideYard(new Vector3(0,200,0)));
        Assert.IsFalse(chicken.InsideYard(new Vector3(51,0,0)));
        Assert.IsFalse(chicken.InsideYard(new Vector3(0,0,-51)));
    }
    [TestCase(false, false, 12f, 100f)]
    [TestCase(true, false, 1f, 100f)]
    [TestCase(true, false, 4f, 72f)]
    [TestCase(true, true, 8f, 0f)]
    public void VehicleDamageRequiresMotorAndSpeed(bool motor, bool pickup, float speed, float remaining)
    {
        var vehicle=new GameObject("Vehicle test");vehicle.SetActive(false);vehicle.transform.SetParent(root.transform);
        var movement=vehicle.AddComponent<BicycleController>();
        if(pickup)vehicle.AddComponent<PickupController>();
        movement.motorDriven=motor;
        typeof(BicycleController).GetField("<Speed>k__BackingField",Private).SetValue(movement,speed);
        chicken.HitByVehicle(movement);
        Assert.AreEqual(remaining,chicken.health);
    }
}
