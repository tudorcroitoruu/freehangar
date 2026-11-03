using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class FarmChickenTests
{
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
