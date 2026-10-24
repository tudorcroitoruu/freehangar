using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public static class ValidateWallRuntime
{
    public static async Task<string> Main()
    {
        Application.runInBackground=true; Time.timeScale=1;
        var pile=UnityEngine.Object.FindFirstObjectByType<GrainPile>();
        var field=(GrainHeightField)typeof(GrainPile).GetField("field",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pile);
        Vector3 local=default; float score=0;
        for(int z=2;z<field.Resolution-2;z++) for(int x=2;x<field.Resolution-2;x++)
        {
            int i=z*field.Resolution+x;
            Vector3 candidate=new Vector3(x*field.Spacing-field.Size*.5f,field.Heights[i],z*field.Spacing-field.Size*.5f);
            if(candidate.y<.25f || !pile.IsInsideBoundary(pile.transform.TransformPoint(candidate),.6f)) continue;
            float uphill=Mathf.Max(Mathf.Max(field.Heights[i-1],field.Heights[i+1]),Mathf.Max(field.Heights[i-field.Resolution],field.Heights[i+field.Resolution]))-candidate.y;
            if(uphill>score) {score=uphill;local=candidate;}
        }
        if(score<.001f) throw new Exception("No slope fixture found");
        Vector3 point=pile.transform.TransformPoint(local);
        double mass=pile.Kilograms; float taken=pile.Scoop(point,.4f,.18f,6.5f);
        float dug=pile.SampleLocalHeight(local.x,local.z);
        await Task.Delay(2400);
        // Collision intentionally cooks at 5 Hz; wait for an actual synchronized frame.
        for (int wait=0;wait<80 && (bool)typeof(GrainPile).GetField("colliderDirty",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(pile);wait++) await Task.Delay(25);
        float settled=pile.SampleLocalHeight(local.x,local.z);
        if(taken<6.49f || !(settled>dug && settled<local.y) || Math.Abs(mass-pile.Kilograms-taken)>.03)
            throw new Exception("Refill/mass failed: "+local.y+" -> "+dug+" -> "+settled+", removed="+taken);
        Vector3 surface=pile.transform.TransformPoint(new Vector3(local.x,settled,local.z));
        bool found=pile.GetComponent<MeshCollider>().Raycast(new Ray(surface+Vector3.up,Vector3.down),out RaycastHit hit,2);
        if(!found || Mathf.Abs(hit.point.y-surface.y)>.002f)
            throw new Exception("Clipped runtime collider mismatch: found="+found+", sample="+surface+", hit="+hit.point+", local="+local);
        return "PASS: live wall-fitted pile scoop/refill/collision; "+taken+" kg conserved; local height "+local.y+" -> "+dug+" -> "+settled;
    }
}
