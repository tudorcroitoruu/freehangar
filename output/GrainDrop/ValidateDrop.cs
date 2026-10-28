using UnityEngine;
using System;
using System.Linq;
public static class ValidateDrop {
public static string Main() {
var source=UnityEngine.Object.FindObjectsByType<GrainPile>(FindObjectsSortMode.None).First();
GrainPile p=null;
try {
p=GrainPile.CreateDroppedPile(new Vector3(10000,100,10000),source);
if(p.Kilograms>0.0001) throw new Exception("Mini pile started with stock "+p.Kilograms);
var c=new GrainCargo();c.Add(GrainType.Rice,4);c.Add(GrainType.Soybeans,2.5f);
if(!p.Deposit(p.transform.position,c)||c.Total!=0)throw new Exception("Deposit failed");
if(Math.Abs(p.Kilograms-6.5)>.001 || Math.Abs(p.GrainKilograms(GrainType.Rice)-4)>.001)throw new Exception("Mass/type mismatch");
c.Add(GrainType.Wheat,3);p.Deposit(p.transform.position,c);
if(Math.Abs(p.Kilograms-9.5)>.001)throw new Exception("Repeat deposit mismatch");
var collected=new GrainCargo();
for(int i=0;i<20;i++){var point=p.transform.position;point.y=p.SurfaceHeight(point);p.Scoop(point,2,2,20,collected);}
if(Math.Abs(collected.Total-9.5)>.002 || Math.Abs(collected[GrainType.Soybeans]-2.5)>.002)throw new Exception("Scoop mass mismatch "+collected.Total);
return "PASS: empty initialization, mixed deposit, per-type stock, repeated deposit, scoop recovery and total conservation";
}finally{if(p!=null)UnityEngine.Object.DestroyImmediate(p.gameObject);}
}}
