using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class PlantHillGrass
{
    const string AssetsPath="Assets/NewMap/FiveHangars/HillGrass";
    struct Tuft { public Vector3 p; public float height,width,yaw; }
    static Vector3[] hillVertices;static int[] hillTriangles;
    static bool HillHeight(Vector3 p,out float h)
    {
        h=0;
        for(int i=0;i<hillTriangles.Length;i+=3)
        {
            var a=hillVertices[hillTriangles[i]];var b=hillVertices[hillTriangles[i+1]];var c=hillVertices[hillTriangles[i+2]];
            float d=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(d)<.00001f)continue;
            float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/d;
            float v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/d;float w=1-u-v;
            if(u<0||v<0||w<0)continue;h=u*a.y+v*b.y+w*c.y;return true;
        }
        return false;
    }
    public static string Run()
    {
        if(EditorApplication.isPlaying) throw new Exception("Edit mode required");
        var previous=GameObject.Find("Low poly outer hill grass");
        if(previous){Object.DestroyImmediate(previous);AssetDatabase.DeleteAsset(AssetsPath);}
        var terrain=Terrain.activeTerrain;
        var backdrop=GameObject.Find("Distant rolling hills - backdrop");
        if(terrain==null||backdrop==null) throw new Exception("Landscape missing");
        if(!File.Exists("output/HillGrass/Before.unity"))EditorSceneManager.SaveScene(terrain.gameObject.scene,"output/HillGrass/Before.unity",true);
        Directory.CreateDirectory(AssetsPath);AssetDatabase.Refresh();
        var mat=new Material(Shader.Find("HDRP/Lit")){name="Outer hill grass - meadow green"};
        mat.SetColor("_BaseColor",new Color(.16f,.36f,.025f));mat.SetFloat("_Smoothness",0);mat.SetFloat("_Metallic",0);
        AssetDatabase.CreateAsset(mat,AssetsPath+"/Meadow grass.mat");
        var mesh=backdrop.GetComponent<MeshFilter>().sharedMesh;
        hillVertices=mesh.vertices.Select(v=>backdrop.transform.TransformPoint(v)).ToArray();hillTriangles=mesh.triangles;
        var collider=backdrop.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
        Physics.SyncTransforms();
        var rng=new System.Random(28102026);
        Func<float,float,float> random=(a,b)=>Mathf.Lerp(a,b,(float)rng.NextDouble());
        var tufts=new List<Tuft>();int slopeCount=0,outerCount=0;
        var tp=terrain.transform.position;var ts=terrain.terrainData.size;
        var bounds=backdrop.GetComponent<Renderer>().bounds;
        try
        {
            for(float z=bounds.min.z+2;z<bounds.max.z-2;z+=1.55f)
            for(float x=bounds.min.x+2;x<bounds.max.x-2;x+=1.55f)
            {
                var p=new Vector3(x+random(-.8f,.8f),0,z+random(-.8f,.8f));
                float edge=Mathf.Min(Mathf.Min(p.x-tp.x,tp.x+ts.x-p.x),Mathf.Min(p.z-tp.z,tp.z+ts.z-p.z));
                if(edge>27)continue;
                float patch=Mathf.PerlinNoise(p.x*.065f+31,p.z*.065f+57);
                if(rng.NextDouble()>Mathf.Lerp(.35f,.94f,patch))continue;
                if(edge>=0)
                {
                    float slope=terrain.terrainData.GetSteepness((p.x-tp.x)/ts.x,(p.z-tp.z)/ts.z);
                    if(slope>48)continue;
                    p.y=terrain.SampleHeight(p)+tp.y-.025f;slopeCount++;
                }
                else
                {
                    float height;if(!HillHeight(p,out height))continue;
                    p.y=height-.025f;outerCount++;
                }
                tufts.Add(new Tuft{p=p,height=random(.65f,1.25f),width=random(.8f,1.4f),yaw=random(0,360)});
            }
        }
        finally {Object.DestroyImmediate(collider);}
        var root=new GameObject("Low poly outer hill grass");root.transform.SetParent(backdrop.transform.parent,false);
        Undo.RegisterCreatedObjectUndo(root,"Plant outer hill grass");
        long triangles=0;int serial=0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var cell in tufts.GroupBy(t=>new Vector2Int(Mathf.FloorToInt(t.p.x/48),Mathf.FloorToInt(t.p.z/48))))
            {
                var g=new GameObject("Meadow "+cell.Key.x+"_"+cell.Key.y);g.transform.SetParent(root.transform,false);
                g.transform.position=new Vector3(cell.Average(t=>t.p.x),cell.Average(t=>t.p.y),cell.Average(t=>t.p.z));
                var lod=g.AddComponent<LODGroup>();var levels=new LOD[3];
                for(int level=0;level<3;level++)
                {
                    var vertices=new List<Vector3>();var indices=new List<int>();
                    int blades=level==0?5:level==1?3:2;
                    foreach(var tuft in cell)
                    for(int blade=0;blade<blades;blade++)
                    {
                        float angle=(tuft.yaw+blade*137.5f)*Mathf.Deg2Rad;
                        var across=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                        var lean=new Vector3(-across.z,0,across.x);
                        var basePoint=tuft.p-g.transform.position+lean*(blade*.11f-.22f)*tuft.width;
                        float h=tuft.height*(1-blade*.085f),w=tuft.width*.14f;
                        int k=vertices.Count;
                        vertices.Add(basePoint-across*w);vertices.Add(basePoint+across*w);
                        vertices.Add(basePoint+Vector3.up*h+lean*(h*.24f));
                        indices.AddRange(new[]{k,k+2,k+1,k+1,k+2,k});
                    }
                    var m=new Mesh{name=g.name+" LOD "+level,indexFormat=IndexFormat.UInt32};m.SetVertices(vertices);m.SetTriangles(indices,0);m.SetNormals(Enumerable.Repeat(Vector3.up,vertices.Count).ToArray());m.RecalculateBounds();
                    AssetDatabase.CreateAsset(m,AssetsPath+"/Meadow "+(++serial)+".asset");
                    var child=new GameObject("Low poly grass LOD "+level);child.transform.SetParent(g.transform,false);
                    child.AddComponent<MeshFilter>().sharedMesh=m;var r=child.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;
                    levels[level]=new LOD(level==0?.16f:level==1?.065f:.008f,new Renderer[]{r});
                    if(level==0)triangles+=indices.Count/3;
                }
                lod.SetLODs(levels);lod.RecalculateBounds();
            }
        }
        finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(terrain.gameObject.scene);
        string report=$"Added {tufts.Count} low-poly grass clumps: {slopeCount} perimeter, {outerCount} outer hills. {root.transform.childCount} spatial cells, 3 LODs, {triangles} maximum triangles total, one shared material. No colliders or runtime scripts. Grounded on terrain/backdrop. Existing gameplay interior unchanged.";
        File.WriteAllText("output/HillGrass/Validation.txt",report);return report;
    }
}
