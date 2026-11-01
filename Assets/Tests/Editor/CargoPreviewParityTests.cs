using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CargoPreviewParityTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private PlayerData previousData;
    [SetUp] public void SetUp() { root=new GameObject("Cargo parity tests");previousData=SaveSystem.Data;SaveSystem.Data=new PlayerData(); }
    [TearDown] public void TearDown() { Object.DestroyImmediate(root);SaveSystem.Data=previousData; }
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    private GameObject Copy(string path)
    {
        var copy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));copy.transform.SetParent(root.transform,true);return copy;
    }
    [TestCase(1,"FreeBag")][TestCase(2,"SmallBag")][TestCase(3,"LargeBag")]
    [TestCase(4,"Crate")][TestCase(5,"Barrel")][TestCase(6,"BigBag")]
    public void LegacyContainersResolveTheSameCargoPoseAsTheirShopType(int level,string id)
    {
        var cargo=new GameObject("Legacy cargo");cargo.transform.SetParent(root.transform);var container=cargo.AddComponent<GrainContainer>();
        Set(container,"transportLevel",level);
        Assert.AreEqual(id,container.CargoTypeId);
        var profile=new VehicleUpgradeSettings.VehicleType { cargoPoses=new[]{new VehicleUpgradeSettings.CargoPose { containerId=id,slotIndex=0,rotation=new Vector3(0,90,-90) }} };
        Assert.IsNotNull(profile.FindCargoPose(container.CargoTypeId,0));
    }
    [TestCase("FreeBag")][TestCase("SmallBag")][TestCase("LargeBag")][TestCase("BigBag")][TestCase("Crate")][TestCase("Barrel")]
    public void FullClosedPreviewUsesTheSameContactBoundsAsGameplay(string id)
    {
        string path="Assets/Prefabs/Gameplay/Containers/"+id+".prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        float originalKg=prefab.GetComponent<GrainContainer>().Kilograms;
        var preview=Copy(path);var live=Copy(path);live.transform.SetPositionAndRotation(preview.transform.position,preview.transform.rotation);
        VehicleCargoPreview.PrepareContainer(preview,1,1);
        var cargo=live.GetComponent<GrainContainer>();
        foreach(var sack in live.GetComponentsInChildren<SackController>(true)) { sack.RefreshBindings();sack.Fill=1;sack.Close=1; }
        Set(cargo,"kilograms",cargo.CapacityKg);cargo.RefreshFillPresentation();
        Assert.Less(Vector3.Distance(preview.GetComponent<GrainContainer>().WorldBounds.center,cargo.WorldBounds.center),.0001f);
        Assert.Less(Vector3.Distance(preview.GetComponent<GrainContainer>().WorldBounds.size,cargo.WorldBounds.size),.0001f);
        Assert.AreEqual(originalKg,prefab.GetComponent<GrainContainer>().Kilograms,"Preview must not modify source prefab.");
    }
    [Test]
    public void LegacyBagStowMatchesPreviewAndRemainsStableAfterPickupAndReload()
    {
        var bike=Copy("Assets/Resources/Vehicles/1_Bicycle_Rideable.prefab");
        bike.transform.position=new Vector3(10000,1000,10000);bike.transform.rotation=Quaternion.Euler(0,35,0);
        var rack=bike.GetComponent<VehicleCargoRack>();var slot=rack.FreeSlot(true);Assert.IsNotNull(slot);
        var item=Copy("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab");var cargo=item.GetComponent<GrainContainer>();
        VehicleCargoPreview.PrepareContainer(item,1,1);Set(cargo,"cargoTypeId","");
        var preview=Copy("Assets/Prefabs/Gameplay/Containers/FreeBag.prefab");VehicleCargoPreview.PrepareContainer(preview,1,1);
        preview.transform.rotation=Quaternion.AngleAxis(slot.eulerAngles.y,Vector3.up)*preview.transform.rotation;
        VehicleCargoPlacement.Place(preview.transform,slot,bike.GetComponent<VehicleUpgrade>().Profile,"FreeBag",0,Quaternion.Inverse(slot.rotation)*preview.transform.rotation,true,preview.transform.localScale);
        var actor=new GameObject("Carrier");actor.transform.SetParent(root.transform);var carry=actor.AddComponent<BagInteraction>();
        var anchor=new GameObject("Hands");anchor.transform.SetParent(actor.transform);Set(carry,"carryAnchor",anchor.transform);
        Vector3 worldScale=item.transform.lossyScale;
        for(int i=0;i<3;i++)
        {
            Assert.IsTrue((bool)typeof(BagInteraction).GetMethod("Take",Private).Invoke(carry,new object[]{cargo}));
            Assert.IsTrue(carry.TryStow(slot));
            Assert.Less(Quaternion.Angle(preview.transform.rotation,item.transform.rotation),.02f,"Rotation must match preview on cycle "+i);
            Assert.Less(Vector3.Distance(preview.transform.position,item.transform.position),.001f,"Position must match preview on cycle "+i);
            Assert.Less(Vector3.Distance(preview.transform.lossyScale,item.transform.lossyScale),.001f,"Scale must match preview on cycle "+i);
        }
        Assert.IsTrue((bool)typeof(BagInteraction).GetMethod("Take",Private).Invoke(carry,new object[]{cargo}));
        typeof(BagInteraction).GetMethod("Release",Private).Invoke(carry,new object[]{bike.transform.position+Vector3.right*4});
        Assert.Less(Vector3.Distance(worldScale,item.transform.lossyScale),.001f,"World drop must not keep the bicycle parent's scale compensation.");
    }
}
