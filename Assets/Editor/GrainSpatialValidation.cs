using System;
using UnityEditor;
using UnityEngine;

public static class GrainSpatialValidation
{
    private static int checks;
    private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    private static bool Near(double a, double b, double tolerance = .0001) => Math.Abs(a-b) < tolerance;
    [MenuItem("Tools/Grain/Validate spatial crop stock")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run model checks in Edit Mode.");
        checks = 0;
        var cargo = new GrainCargo();
        var field = new GrainHeightField(33, 4, 1, (x,z)=>1);
        var regions = new[] { new GrainRegion(20,GrainType.Corn),new GrainRegion(20,GrainType.Rice),new GrainRegion(20,GrainType.Soybeans),new GrainRegion(20,GrainType.Wheat),new GrainRegion(20,GrainType.Barley) };
        var spatial = new GrainSpatialField(field,regions,GrainSpatialField.Arrangement.FrontToBack,0,731);
        try
        {
            foreach (var type in new[]{GrainType.Corn,GrainType.Rice,GrainType.Soybeans,GrainType.Wheat,GrainType.Barley})
                Check(Near(spatial.VolumeOf(type)/field.Volume,.2),"Exactly 20% physical stock: "+type);
            spatial.Sample(0,-1.6f,cargo);Check(cargo[GrainType.Corn]>.99,"Front visibly corn");
            spatial.Sample(0,-.8f,cargo);Check(cargo[GrainType.Rice]>.99,"Next region visibly rice before excavation");
            cargo.Clear();spatial.BeginCollection(cargo,720);
            float removed=field.Scoop(0,-.8f,.25f,.2f,6.5f/720);
            spatial.EndCollection();
            Check(Near(cargo.Total,removed*720,.003)&&cargo[GrainType.Rice]>6.49&&cargo[GrainType.Corn]==0,"Scooping rice area yields rice, not a global corn recipe");
        }
        finally { spatial.Dispose(); }
        field=new GrainHeightField(33,4,1,(x,z)=>1);
        var layered=new GrainRegion(100,GrainType.Corn){layers=new[]{new GrainStratum(20,GrainType.Corn),new GrainStratum(20,GrainType.Rice),new GrainStratum(20,GrainType.Soybeans),new GrainStratum(20,GrainType.Wheat),new GrainStratum(20,GrainType.Barley)}};
        spatial=new GrainSpatialField(field,new[]{layered},GrainSpatialField.Arrangement.FrontToBack,0,1);
        try
        {
            cargo.Clear();spatial.BeginCollection(cargo,1);field.Scoop(0,0,100,.21f,100);spatial.EndCollection();
            spatial.Sample(0,0,cargo);Check(cargo[GrainType.Rice]>.99,"Removing the local 20% corn cap reveals rice");
            spatial.BeginCollection(null,1);field.Scoop(0,0,100,.21f,100);spatial.EndCollection();
            spatial.Sample(0,0,cargo);Check(cargo[GrainType.Soybeans]>.99,"Removing the next local layer reveals soybeans");
        }
        finally { spatial.Dispose(); }
        field=new GrainHeightField(33,4,1,(x,z)=>1);
        spatial=new GrainSpatialField(field,GrainSpatialSetup.Preset(4),GrainSpatialField.Arrangement.FrontToBack,0,1);
        try
        {
            spatial.Sample(0,-1.85f,cargo);Check(cargo[GrainType.Wheat]>.99,"Repeated sequence starts with wheat");
            spatial.Sample(0,-1.5f,cargo);Check(cargo[GrainType.Rice]>.99,"5% rice strip remains distinct");
            spatial.Sample(0,-1.2f,cargo);Check(cargo[GrainType.Wheat]>.99,"Wheat returns after rice");
            Check(Near(spatial.VolumeOf(GrainType.Corn)/field.Volume,.05),"Late hangar has exactly 5% corn by mass");
        }
        finally { spatial.Dispose(); }
        field=new GrainHeightField(33,4,1,(x,z)=>1+.1f*z);
        spatial=new GrainSpatialField(field,new[]{new GrainRegion(50,GrainType.Corn),new GrainRegion(50,GrainType.Rice)},GrainSpatialField.Arrangement.FrontToBack,0,1);
        try
        {
            double corn=spatial.VolumeOf(GrainType.Corn), rice=spatial.VolumeOf(GrainType.Rice);
            cargo.Clear();spatial.BeginCollection(cargo,1);field.Scoop(0,0,.7f,.45f,.3f);spatial.EndCollection();
            for(int i=0;i<240;i++)field.Settle(32,.45f);
            Check(Near(corn,spatial.VolumeOf(GrainType.Corn)+cargo[GrainType.Corn]),"Settling conserves corn separately");
            Check(Near(rice,spatial.VolumeOf(GrainType.Rice)+cargo[GrainType.Rice]),"Settling conserves rice separately");
            Check(Near(field.Volume,spatial.VolumeOf(GrainType.Corn)+spatial.VolumeOf(GrainType.Rice)),"Typed stock matches height-field volume after settling");
        }
        finally { spatial.Dispose(); }
        field=new GrainHeightField(33,4,1,(x,z)=>1);
        spatial=new GrainSpatialField(field,regions,GrainSpatialField.Arrangement.FrontToBack,0,1);
        try
        {
            double original=field.Volume;spatial.PreviewClear(20);
            Check(Near(field.Volume,original*.8)&&spatial.VolumeOf(GrainType.Corn)<.0001,"20% preview clears the corn region without relabeling remaining stock");
            spatial.Sample(0,-.8f,cargo);Check(cargo[GrainType.Rice]>.99,"Preview preserves the visible next rice region");
        }
        finally { spatial.Dispose(); }
        field=new GrainHeightField(65,4,1,(x,z)=>1);
        spatial=new GrainSpatialField(field,regions,GrainSpatialField.Arrangement.CurvedSections,.12f,731);
        try
        {
            var visible = new System.Collections.Generic.HashSet<GrainType>();
            for(int i=0;i<72;i++)
            {
                float angle=i*Mathf.PI*2/72;
                spatial.Sample(.2f+Mathf.Cos(angle)*1.4f,-.08f+Mathf.Sin(angle)*1.4f,cargo);
                visible.Add(cargo.Dominant);
            }
            Check(visible.Count==5,"Curved layout exposes five distinct crops around its junction");
            foreach(var type in new[]{GrainType.Corn,GrainType.Rice,GrainType.Soybeans,GrainType.Wheat,GrainType.Barley})
                Check(Near(spatial.VolumeOf(type)/field.Volume,.2),"Curved sections preserve exact mass share: "+type);
            cargo.Clear();spatial.BeginCollection(cargo,1);float removed=field.Scoop(.2f,-.08f,.6f,.2f,.1f);spatial.EndCollection();
            Check(Near(cargo.Total,removed),"Junction scoop conserves total mixed cargo");
            int types=0;for(int t=0;t<GrainCargo.TypeCount;t++)if(cargo[(GrainType)t]>.00001f)types++;
            Check(types>=3,"Junction scoop can collect several neighboring crops together");
        }
        finally { spatial.Dispose(); }
        field=new GrainHeightField(65,4,1,(x,z)=>1+.2f*z);
        spatial=new GrainSpatialField(field,regions,GrainSpatialField.Arrangement.ChunksAndCurves,.16f,731);
        try
        {
            foreach(var type in new[]{GrainType.Corn,GrainType.Rice,GrainType.Soybeans,GrainType.Wheat,GrainType.Barley})
                Check(Near(spatial.VolumeOf(type)/field.Volume,.2),"Chunk layout preserves mass on sloped pile: "+type);
            var visible=new System.Collections.Generic.HashSet<GrainType>();
            for(int z=0;z<20;z++)for(int x=0;x<20;x++)
            {spatial.Sample(-1.9f+x*.2f,-1.9f+z*.2f,cargo);visible.Add(cargo.Dominant);}
            Check(visible.Count==5,"Chunk layout exposes all five crops");
        }
        finally { spatial.Dispose(); }
        var go=new GameObject("Spatial transform validation"){hideFlags=HideFlags.HideAndDontSave};go.SetActive(false);
        try
        {
            var pile=go.AddComponent<GrainPile>();using(var data=new SerializedObject(pile)){data.FindProperty("fitToWalls").boolValue=false;data.ApplyModifiedPropertiesWithoutUndo();}
            go.transform.SetPositionAndRotation(new Vector3(1000,2,1000),Quaternion.Euler(0,67,0));go.transform.localScale=new Vector3(2,1.5f,3);
            pile.ConfigureRegions(GrainSpatialSetup.Preset(5),GrainSpatialField.Arrangement.Patches,.2f);
            Check(Near(pile.GrainKilograms(GrainType.Corn)/pile.Kilograms,.05),"Rotated/nonuniformly scaled pile still has 5% corn");
            var block=new MaterialPropertyBlock();pile.ApplySpatialAppearance(block);
            Check(block.GetFloat("_UseSpatialGrain")==1&&block.GetTexture("_GrainMapA")!=null,"Shader receives the actual spatial stock map");
            double before=pile.GrainKilograms(GrainType.Rice);pile.SetRecipe(new[]{new GrainLayer(52,GrainType.Corn)});
            Check(Near(before,pile.GrainKilograms(GrainType.Rice)),"Legacy recipe cannot transmute spatial material");
            Check(!ShaderUtil.ShaderHasError(Shader.Find("Freehangar/HDRP/Grain Kernels")),"Spatial shader compiles");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
        Debug.Log("Spatial grain validation passed: "+checks+" assertions.");
    }
}
