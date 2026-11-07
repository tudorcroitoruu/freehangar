using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class PlayerToolInteractionTests
{
    private GameObject root;
    private GameObject wallObject;
    private PlayerCombatTools tools;
    private PlayerData previousData;
    private float previousScale;
    private Transform axe,broom,hammer;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [SetUp] public void Setup()
    {
        previousData=SaveSystem.Data;previousScale=Time.timeScale;Time.timeScale=1;
        SaveSystem.Data=new PlayerData{foundSecrets=new[]{PlayerCombatTools.BroomFindId,PlayerCombatTools.LegacyAxeFindId,PlayerCombatTools.HammerFindId}};
        root=new GameObject("Tool interaction regression");root.transform.position=new Vector3(25000,1000,25000);
        tools=root.AddComponent<PlayerCombatTools>();
        axe=Child("Axe");broom=Child("Broom");hammer=Child("Hammer");
        Set("heldAxe",axe);Set("heldBroom",broom);Set("heldSledgehammer",hammer);
        var camera=Child("Camera").gameObject.AddComponent<Camera>();camera.enabled=false;Set("viewCamera",camera);
    }
    [TearDown] public void Cleanup(){Object.DestroyImmediate(root);if(wallObject!=null)Object.DestroyImmediate(wallObject);SaveSystem.Data=previousData;Time.timeScale=previousScale;}
    [TestCase(PlayerCombatTools.AxeFindId)]
    [TestCase(PlayerCombatTools.LegacyAxeFindId)]
    public void CollectedAxeCanBeEquippedWithLoadedShovelBeforeOpeningHangarTwo(string discoveryId)
    {
        SaveSystem.Data = new PlayerData { foundSecrets = new[] { discoveryId } };
        Assert.IsFalse(SaveSystem.Data.IsHangarOpen(2));
        CreateShovelPlayer(out var shovel);
        shovel.Cargo.Add(GrainType.Corn, 2);
        Assert.IsTrue(tools.TryEquip(PlayerCombatTools.Tool.Axe));
        Assert.AreEqual(PlayerCombatTools.Tool.Axe, tools.Equipped);
        Assert.IsTrue(axe.gameObject.activeSelf);
        Assert.AreEqual(2, shovel.Kilograms);
        Assert.IsTrue(tools.TryEquip(PlayerCombatTools.Tool.Shovel));
        Assert.AreEqual(2, shovel.Kilograms);
    }
    [TestCase(PlayerCombatTools.Tool.Axe)]
    [TestCase(PlayerCombatTools.Tool.Hammer)]
    [TestCase(PlayerCombatTools.Tool.Broom)]
    public void DrivingHidesToolsAndRestoresSelection(PlayerCombatTools.Tool selected)
    {
        Assert.IsTrue(tools.TryEquip(selected));
        tools.SetDriving(true);
        Assert.IsFalse(axe.gameObject.activeSelf);
        Assert.IsFalse(hammer.gameObject.activeSelf);
        Assert.IsFalse(broom.gameObject.activeSelf);
        Assert.IsFalse(tools.TryStartStrike());
        Assert.IsFalse(tools.TryEquip(PlayerCombatTools.Tool.Shovel));
        Assert.AreEqual(selected, tools.Equipped);
        tools.SetDriving(false);
        Assert.AreEqual(selected == PlayerCombatTools.Tool.Axe, axe.gameObject.activeSelf);
        Assert.AreEqual(selected == PlayerCombatTools.Tool.Hammer, hammer.gameObject.activeSelf);
        Assert.AreEqual(selected == PlayerCombatTools.Tool.Broom, broom.gameObject.activeSelf);
    }
    [Test] public void LockedToolsCannotBeEquipped()
    {
        SaveSystem.Data.foundSecrets=System.Array.Empty<string>();
        Assert.IsFalse(tools.TryEquip(PlayerCombatTools.Tool.Broom));Assert.IsFalse(tools.TryEquip(PlayerCombatTools.Tool.Axe));Assert.IsFalse(tools.TryEquip(PlayerCombatTools.Tool.Hammer));
        Assert.IsTrue(tools.TryEquip(PlayerCombatTools.Tool.Shovel));
    }
    [TestCase(PlayerCombatTools.Tool.Axe)]
    [TestCase(PlayerCombatTools.Tool.Hammer)]
    [TestCase(PlayerCombatTools.Tool.Broom)]
    public void CarryingBagHidesToolsAndRestoresSelectedTool(PlayerCombatTools.Tool selected)
    {
        var bags = root.AddComponent<BagInteraction>();
        Set("bags", bags);
        Assert.IsTrue(tools.TryEquip(selected));
        var bag = Child("Carried bag").gameObject.AddComponent<GrainContainer>();
        var carried = typeof(BagInteraction).GetField("carried", Private);
        var refresh = typeof(PlayerCombatTools).GetMethod("RefreshCarryingVisuals", Private);
        try
        {
            carried.SetValue(bags, bag);
            refresh.Invoke(tools, null);
            Assert.IsFalse(axe.gameObject.activeSelf);
            Assert.IsFalse(hammer.gameObject.activeSelf);
            Assert.IsFalse(broom.gameObject.activeSelf);
            Assert.IsFalse(tools.TryStartStrike());
            Assert.AreEqual(selected, tools.Equipped);
            carried.SetValue(bags, null);
            refresh.Invoke(tools, null);
            Assert.AreEqual(selected == PlayerCombatTools.Tool.Axe, axe.gameObject.activeSelf);
            Assert.AreEqual(selected == PlayerCombatTools.Tool.Hammer, hammer.gameObject.activeSelf);
            Assert.AreEqual(selected == PlayerCombatTools.Tool.Broom, broom.gameObject.activeSelf);
        }
        finally { carried.SetValue(bags, null); }
    }
    [Test] public void PistolRequiresDiscoveryAndCannotShootThroughWallsOrDuringReload()
    {
        var pistol = Child("Pistol");
        Set("heldPistol", pistol);
        Assert.IsFalse(tools.TryEquip(PlayerCombatTools.Tool.Pistol));
        SaveSystem.Data.foundSecrets = new[] { PlayerCombatTools.PistolFindId };
        Assert.IsTrue(tools.TryEquip(PlayerCombatTools.Tool.Pistol));
        Assert.AreEqual(LayerMask.NameToLayer("HeldTools"), pistol.gameObject.layer);
        var bird = new GameObject("Target behind wall");
        try
        {
            bird.transform.position = root.transform.position + Vector3.forward * 8f;
            new GameObject("Visual").transform.SetParent(bird.transform, false);
            bird.AddComponent<SphereCollider>();
            var chicken = bird.AddComponent<FarmChicken>();
            typeof(FarmChicken).GetMethod("Awake", Private).Invoke(chicken, null);
            var wall = Wall(1);
            wall.GetComponent<BoxCollider>().enabled = true;
            Physics.SyncTransforms();
            Assert.IsTrue(tools.TryStartStrike());
            Assert.IsFalse(chicken.IsDead);
            Assert.IsFalse(tools.TryStartStrike());
            wall.gameObject.SetActive(false);
            Set("lastShot", Time.time - 2f);
            Physics.SyncTransforms();
            Assert.IsTrue(tools.TryStartStrike());
            Assert.IsTrue(chicken.IsDead);
        }
        finally { Object.DestroyImmediate(bird); }
    }
    [TestCase(PlayerCombatTools.Tool.Axe, PlayerCombatTools.LegacyAxeFindId)]
    [TestCase(PlayerCombatTools.Tool.Hammer, PlayerCombatTools.HammerFindId)]
    public void CombatUpgradesRequireDiscoveryAndStopAtThree(PlayerCombatTools.Tool tool, string findId)
    {
        var data = new PlayerData { cash = 2000, freeUpgradeVouchers = 1 };
        Assert.IsFalse(CombatToolUpgrades.Purchase(data, tool, 0));
        Assert.AreEqual(1, data.freeUpgradeVouchers);
        data.foundSecrets = new[] { findId };
        Assert.IsTrue(CombatToolUpgrades.Purchase(data, tool, 0));
        Assert.AreEqual(2000, data.cash);
        Assert.AreEqual(0, data.freeUpgradeVouchers);
        Assert.IsFalse(CombatToolUpgrades.Purchase(data, tool, 0));
        Assert.IsTrue(CombatToolUpgrades.Purchase(data, tool, 1));
        Assert.IsTrue(CombatToolUpgrades.Purchase(data, tool, 2));
        Assert.IsFalse(CombatToolUpgrades.Purchase(data, tool, 3));
        Assert.AreEqual(600, data.cash);
        var restored = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.AreEqual(3, CombatToolUpgrades.Level(restored, tool));
        Assert.AreEqual(0, CombatToolUpgrades.Level(restored, tool == PlayerCombatTools.Tool.Axe ?
            PlayerCombatTools.Tool.Hammer : PlayerCombatTools.Tool.Axe));
    }

    [Test] public void MaxCombatUpgradeBreaksMatchingWallsAndNeedsTwoChickenHits()
    {
        SaveSystem.Data.axeUpgradeLevel = SaveSystem.Data.hammerUpgradeLevel = 3;
        Assert.AreEqual(50, CombatToolUpgrades.AxeDamage(3));
        for (int strength = 2; strength <= 8; strength++)
        {
            Assert.Less(CombatToolUpgrades.WallDamage(2, strength), strength);
            Assert.AreEqual(strength, CombatToolUpgrades.WallDamage(3, strength));
        }
        var metal = Wall(1);
        Assert.IsFalse(metal.AcceptsTool(PlayerCombatTools.Tool.Axe));
        Assert.IsTrue(metal.AcceptsTool(PlayerCombatTools.Tool.Hammer));
    }

    [Test] public void ExistingAxeDiscoverySaveUnlocksOnlyItsMatchingHeldModel()
    {
        SaveSystem.Data.foundSecrets=new[]{PlayerCombatTools.LegacyAxeFindId};
        Assert.IsTrue(tools.TryEquip(PlayerCombatTools.Tool.Axe));
        Assert.IsTrue(axe.gameObject.activeSelf);Assert.IsFalse(broom.gameObject.activeSelf);Assert.IsFalse(hammer.gameObject.activeSelf);
        Assert.IsFalse(tools.TryEquip(PlayerCombatTools.Tool.Hammer));
    }
    [TestCase(0,PlayerCombatTools.Tool.Axe,true)]
    [TestCase(0,PlayerCombatTools.Tool.Hammer,false)]
    [TestCase(1,PlayerCombatTools.Tool.Axe,false)]
    [TestCase(1,PlayerCombatTools.Tool.Hammer,true)]
    [TestCase(0,PlayerCombatTools.Tool.Broom,false)]
    public void BarrierRequiresTheCorrectTool(int material,PlayerCombatTools.Tool tool,bool accepted)
    {
        var wall=Wall(material);Assert.That(wall.AcceptsTool(tool),Is.EqualTo(accepted));
    }
    [Test] public void AxeDamageOccursAtAnimatedContactAndOnlyOnce()
    {
        var wall=Wall(0);
        Assert.IsTrue(tools.TryEquip(PlayerCombatTools.Tool.Axe));Assert.IsTrue(tools.TryStartStrike());
        Assert.That(wall.HitsRemaining,Is.EqualTo(3));
        Call("AdvanceStrike");Assert.That(wall.HitsRemaining,Is.EqualTo(3));
        Set("strikeStart",Time.time-.3f);Call("AdvanceStrike");
        Assert.That(wall.HitsRemaining,Is.EqualTo(2));
        Call("AdvanceStrike");Assert.That(wall.HitsRemaining,Is.EqualTo(2));
        Call("LateUpdate");Assert.Greater(axe.localPosition.sqrMagnitude,.0001f);
    }
    [Test] public void SwitchingToolsCancelsThePendingImpact()
    {
        var wall=Wall(0);tools.TryEquip(PlayerCombatTools.Tool.Axe);tools.TryStartStrike();
        tools.TryEquip(PlayerCombatTools.Tool.Broom);Set("strikeStart",Time.time-.3f);Call("AdvanceStrike");
        Assert.That(wall.HitsRemaining,Is.EqualTo(3));
    }
    [Test]
    public void LoadedShovelReturnsAfterTakingAndStowingBag()
    {
        var player = CreateShovelPlayer(out var shovel);
        shovel.Cargo.Add(GrainType.Corn, 2);
        var bags = root.AddComponent<BagInteraction>();
        Set("bags", bags);
        typeof(PlayerController).GetField("bagInteraction", Private).SetValue(player, bags);
        typeof(BagInteraction).GetField("carryAnchor", Private).SetValue(bags, Child("Carry anchor"));
        typeof(BagInteraction).GetField("viewCamera", Private).SetValue(bags, player.characterCamera);
        typeof(BagInteraction).GetField("shovelPresentation", Private).SetValue(bags, player.HeldInstrument);
        var bag = Child("Bag").gameObject.AddComponent<GrainContainer>();
        var slot = Child("Vehicle cargo slot");
        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue(bags.TryInteract(bag));
            Assert.IsFalse(player.IsShovelEquipped);
            tools.Tick(false, false);
            Assert.IsFalse(player.IsShovelEquipped, "Do not reveal the shovel while carrying.");
            Assert.IsTrue(bags.TryStow(slot));
            Assert.IsTrue(player.IsShovelEquipped, "Restore the selected shovel even when it contains grain.");
            Assert.AreEqual(2, shovel.Kilograms);
        }
        Assert.IsTrue(tools.TryEquip(PlayerCombatTools.Tool.Axe));
        Assert.AreEqual(2, shovel.Kilograms, "Stowing the shovel must preserve its grain.");
        Assert.IsTrue(tools.TryEquip(PlayerCombatTools.Tool.Shovel));
        Assert.AreEqual(2, shovel.Kilograms);
    }

    [Test]
    public void SelectedToolRecoversAfterMenuClosesButNotDuringDrivingOrCutscene()
    {
        var player = CreateShovelPlayer(out var shovel);
        shovel.Cargo.Add(GrainType.Corn, 2);
        var closedFrame = typeof(UpgradeManager).GetField("closedFrame", BindingFlags.Static | BindingFlags.NonPublic);
        int previous = (int)closedFrame.GetValue(null);
        try
        {
            Assert.IsTrue(PlayerCombatTools.SetShovelEquipped(player, false));
            closedFrame.SetValue(null, Time.frameCount);
            tools.Tick(false, false);
            Assert.IsFalse(player.IsShovelEquipped);
            closedFrame.SetValue(null, -1);
            player.characterController.enabled = false;
            tools.Tick(false, false);
            Assert.IsFalse(player.IsShovelEquipped);
            player.characterController.enabled = true;
            tools.Tick(false, false);
            Assert.IsTrue(player.IsShovelEquipped);
            Assert.AreEqual(2, shovel.Kilograms);
        }
        finally { closedFrame.SetValue(null, previous); }
    }

    [TestCase(0)] [TestCase(4)] [TestCase(8)] [TestCase(12)] [TestCase(16)] [TestCase(20)]
    public void UpgradeWhileHiddenShowsCurrentModelWhenHandsReturn(int level)
    {
        var player = CreateShovelPlayer(out var shovel);
        var levels = shovel.gameObject.AddComponent<HeldGrainToolLevels>();
        levels.levels = new GameObject[6];
        for (int i = 0; i < levels.levels.Length; i++)
        { levels.levels[i] = Child("Tool " + i).gameObject; levels.levels[i].transform.SetParent(shovel.transform, false); }
        typeof(HeldGrainToolLevels).GetMethod("Awake", Private).Invoke(levels, null);
        Assert.IsTrue(PlayerCombatTools.SetShovelEquipped(player, false));
        SaveSystem.Data.shovelUpgradeLevel = level;
        tools.Tick(false, false);
        typeof(HeldGrainToolLevels).GetMethod("Update", Private).Invoke(levels, null);
        Assert.IsTrue(player.IsShovelEquipped);
        for (int i = 0; i < levels.levels.Length; i++) Assert.AreEqual(i == level / 4, levels.levels[i].activeInHierarchy);
    }

    private PlayerController CreateShovelPlayer(out ShovelTool shovel)
    {
        var player = root.AddComponent<PlayerController>();
        player.characterCamera = root.GetComponentInChildren<Camera>();
        player.characterController = root.GetComponent<CharacterController>();
        var hands = Child("Shovel hands");
        shovel = hands.gameObject.AddComponent<ShovelTool>();
        typeof(PlayerController).GetField("heldInstrument", Private).SetValue(player, hands);
        typeof(PlayerController).GetField("shovelTool", Private).SetValue(player, shovel);
        Set("player", player); Set("shovelHands", hands); Set("shovelTool", shovel);
        return player;
    }
    [TestCase(PlayerCombatTools.Tool.Axe)]
    [TestCase(PlayerCombatTools.Tool.Broom)]
    [TestCase(PlayerCombatTools.Tool.Hammer)]
    public void EquippingToolIncludesInactiveMeshesInHeldCameraLayer(PlayerCombatTools.Tool tool)
    {
        var held = tool == PlayerCombatTools.Tool.Axe ? axe : tool == PlayerCombatTools.Tool.Broom ? broom : hammer;
        var mesh = new GameObject("Inactive held mesh");
        mesh.transform.SetParent(held, false);
        mesh.SetActive(false);
        int layer = LayerMask.NameToLayer("HeldTools");
        Assert.That(layer, Is.GreaterThanOrEqualTo(0));
        Assert.IsTrue(tools.TryEquip(tool));
        Assert.That(held.gameObject.layer, Is.EqualTo(layer));
        Assert.That(mesh.layer, Is.EqualTo(layer));
        Assert.IsFalse(mesh.activeSelf);
    }
    private BreakableGrainBarrier Wall(int material)
    {
        var go=new GameObject("External wall");wallObject=go;go.transform.position=root.transform.position+Vector3.forward*1.5f;
        var wall=go.AddComponent<BreakableGrainBarrier>();
        var data=new SerializedObject(wall);data.FindProperty("material").enumValueIndex=material;data.FindProperty("barrierId").stringValue="test.wall";data.ApplyModifiedPropertiesWithoutUndo();
        typeof(BreakableGrainBarrier).GetField("exposed",Private).SetValue(wall,true);Physics.SyncTransforms();return wall;
    }
    [Test]
    public void PistolMetalPanelsIgnoreWoodDiscoveryAndRecoverIncorrectlyHiddenSaves()
    {
        var wall = Wall(1);
        var data = new SerializedObject(wall);
        data.FindProperty("barrierId").stringValue = "farm.pistol.box.panel.0";
        data.FindProperty("alwaysExposed").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
        typeof(BreakableGrainBarrier).GetMethod("Awake", Private).Invoke(wall, null);
        SaveSystem.Data.foundSecrets = new[] { "farm.pistol.box.panel.0", "farm.pistol.box.wood.panel.0" };
        Assert.IsFalse(wall.IsBroken);
        Assert.IsTrue(wall.RestoreSave(0, true, false), "An untouched panel hidden by the shared ID must return.");
        Assert.IsTrue(wall.GetComponent<BoxCollider>().enabled);
        Assert.IsFalse(wall.RestoreSave(3, true, false), "A genuinely destroyed panel must stay broken.");
        Assert.IsTrue(wall.IsBroken);
        wall.RestoreSave(0, true);
        SaveSystem.Data.foundSecrets = new[] { "farm.pistol.box.metal.panel.0" };
        Assert.IsTrue(wall.IsBroken);
    }
    private Transform Child(string name){var go=new GameObject(name);go.transform.SetParent(root.transform,false);return go.transform;}

    [Test]
    public void BroomCanScareNonSolidPestWithoutSolidCargoContact()
    {
        var bag = Child("Target bag").gameObject.AddComponent<GrainContainer>();
        bag.SetDemonstrationFill(20);
        var rat = Child("Rat");
        rat.localPosition = Vector3.forward * 2;
        var body = rat.gameObject.AddComponent<Rigidbody>();
        var pest = rat.gameObject.AddComponent<GrainPest>();
        pest.Initialize(bag, false, null, null);
        var hitbox = rat.GetComponent<SphereCollider>();
        Assert.IsTrue(hitbox.isTrigger);
        Assert.IsTrue(body.isKinematic);
        Assert.IsFalse(body.useGravity);
        tools.TryEquip(PlayerCombatTools.Tool.Broom);
        Physics.SyncTransforms();
        Call("PerformStrike");
        Assert.IsFalse(hitbox.enabled, "The broom must still hit and scare trigger-based pests.");
        Assert.IsNull(pest.Target);
    }
    private void Set(string field,object value)=>typeof(PlayerCombatTools).GetField(field,Private).SetValue(tools,value);
    private void Call(string method)=>typeof(PlayerCombatTools).GetMethod(method,Private).Invoke(tools,null);
}
