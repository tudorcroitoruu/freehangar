using UnityEngine;using System;using System.Reflection;using System.IO;
public static class ValidateBicycleCargo {
 public static string Run(){var rider=UnityEngine.Object.FindFirstObjectByType<PlayerBicycleInteraction>();var bike=rider.bicycle;var bags=rider.GetComponent<BagInteraction>();var source=UnityEngine.Object.FindFirstObjectByType<GrainContainer>();var copy=UnityEngine.Object.Instantiate(source.gameObject);copy.name="Bike bag validation";var bag=copy.GetComponent<GrainContainer>();
 void Check(bool ok,string m){if(!ok)throw new Exception(m);}
 try{
 var sack=bag.GetComponent<SackController>();sack.Close=0;bag.Add(bag.CapacityKg);sack.Close=1;
 var take=typeof(BagInteraction).GetMethod("Take",BindingFlags.NonPublic|BindingFlags.Instance);
 Check((bool)take.Invoke(bags,new object[]{bag}),"Take full bag");float mass=bag.Kilograms;
 Check(rider.TryLoadBag(),"Load carried bag");Check(!bags.IsCarrying&&!bag.IsCarried,"Carry state cleared");Check(bike.HasCargo(bag.transform),"Bag attached");Check(bag.Kilograms==mass,"Contents preserved");
 bike.SetRidden(true);Check(!bag.GetComponent<Collider>().enabled,"Own cargo collision disabled riding");bike.SetRidden(false);Check(bag.GetComponent<Collider>().enabled,"Cargo collider restored");
 Check((bool)take.Invoke(bags,new object[]{bag}),"Take bag from rack");
 var parent=typeof(BagInteraction).GetField("originalParent",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bags);Check(parent==null,"Unloaded bag will detach from bike");
 Check(bags.TryStow(bike.FreeCargoSlot()),"Restow bag");
 string result="10 filled-bag cargo checks passed, preserving mass, colliders and unloading ownership.";File.WriteAllText("output/Bicycle/cargo-validation.txt",result);return result;
 }finally{UnityEngine.Object.DestroyImmediate(copy);}
 }
}
