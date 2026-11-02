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
    [Test] public void LockedToolsCannotBeEquipped()
    {
        SaveSystem.Data.foundSecrets=System.Array.Empty<string>();
        Assert.IsFalse(tools.TryEquip(PlayerCombatTools.Tool.Broom));Assert.IsFalse(tools.TryEquip(PlayerCombatTools.Tool.Axe));Assert.IsFalse(tools.TryEquip(PlayerCombatTools.Tool.Hammer));
        Assert.IsTrue(tools.TryEquip(PlayerCombatTools.Tool.Shovel));
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
    private Transform Child(string name){var go=new GameObject(name);go.transform.SetParent(root.transform,false);return go.transform;}
    private void Set(string field,object value)=>typeof(PlayerCombatTools).GetField(field,Private).SetValue(tools,value);
    private void Call(string method)=>typeof(PlayerCombatTools).GetMethod(method,Private).Invoke(tools,null);
}
