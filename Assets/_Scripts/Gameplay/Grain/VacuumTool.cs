using UnityEngine;

/// <summary>Continuous vacuum collection and a removable, animated grain reservoir.</summary>
public sealed class VacuumTool : MonoBehaviour
{
    public Transform nozzle, dustTray, fillFrame;
    public Transform cylinderInlet;
    public float suctionRadius = .55f, suctionDepth = .35f;
    public Mesh kernelMesh;
    public Material kernelMaterial;
    public float capacityKg = 30f, suctionKgPerSecond = 8f;
    public Vector3 trayRemoval = new Vector3(0, -.2f, -.12f);
    public Vector3 trayTilt = new Vector3(0, 0, 80);
    [Tooltip("Optional grain packing positions fitted to the reservoir interior, in fill-frame space.")]
    public Vector3[] reservoirPoints = System.Array.Empty<Vector3>();
    public float reservoirSpacing = .01f;
    public bool uprightCylinder;
    public float fillRadius = .1f, fillLength = .3f;
    public bool Emptying { get; private set; }
    public bool Sucking { get; private set; }
    public Vector3 PositionOffset => new Vector3(0, -.015f * suctionBlend, .07f * suctionBlend);
    public Quaternion RotationOffset => Quaternion.Euler(-3f * suctionBlend, 0, Mathf.Sin(clock * 40) * .18f * suctionBlend);
    private bool requested, initialized, grainMoving;
    private Camera view;
    private Transform actor;
    private float clock, emptyTime, suctionBlend, shownFill, emission;
    private Vector3 trayPosition;
    private Quaternion trayRotation;
    private Mesh fillMesh;
    private MeshRenderer fillRenderer;
    private ParticleSystem stream;
    private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[96];
    private readonly Vector3[] starts = new Vector3[96], ends = new Vector3[96];
    private readonly float[] ages = new float[96];
    private readonly bool[] pouring = new bool[96];
    private int nextParticle, packedCount, kernelIndexCount, displayedCount;
    private Vector3[] settledVertices, animatedVertices, settledPositions;
    private float[] arrivalTimes;
    private int verticesPerKernel;
    private readonly GrainAppearance appearance = new GrainAppearance();

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        trayPosition = dustTray.localPosition; trayRotation = dustTray.localRotation;
        fillMesh = new Mesh { name = "Vacuum grain reservoir" }; fillMesh.MarkDynamic();
        fillFrame.GetComponent<MeshFilter>().sharedMesh = fillMesh;
        fillRenderer = fillFrame.GetComponent<MeshRenderer>();
        BuildPackedGrain();
        fillRenderer.sharedMaterial=kernelMaterial;
        var go=new GameObject("Vacuum grain stream");go.transform.SetParent(transform,false);go.layer=0;
        stream=go.AddComponent<ParticleSystem>();stream.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=stream.main;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=96;main.startSpeed=0;main.startLifetime=1;
        var emit=stream.emission;emit.enabled=false;var shape=stream.shape;shape.enabled=false;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=kernelMesh;renderer.sharedMaterial=kernelMaterial;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        for(int i=0;i<ages.Length;i++)ages[i]=1;
        stream.Play();
    }
    public void RequestSuction(Camera camera, Transform player) { view=camera;actor=player;requested=true; }
    public void BeginEmpty() { Initialize(); Emptying=true;emptyTime=0;requested=false;Sucking=false; }
    public void Tick(ShovelTool tool,float dt)
    {
        Initialize();clock+=dt;Sucking=false;
        if(Emptying)
        {
            emptyTime+=dt;float p=Mathf.Clamp01(emptyTime/2.1f);
            float pull=p<.25f?Mathf.SmoothStep(0,1,p/.25f):p<.75f?1:1-Mathf.SmoothStep(0,1,(p-.75f)/.25f);
            float tip=p<.3f?0:p<.45f?Mathf.SmoothStep(0,1,(p-.3f)/.15f):p<.7f?1:1-Mathf.SmoothStep(0,1,(p-.7f)/.2f);
            dustTray.localPosition=trayPosition+trayRemoval*pull;
            dustTray.localRotation=trayRotation*Quaternion.Euler(trayTilt*tip);
            float moved=tool.VacuumEmptyStep(p,dt);
            if(moved>0)Emit(dustTray.position,tool.VacuumDropPoint,true,Mathf.Max(8,Mathf.CeilToInt(moved*15)));
            if(p>=1){Cancel();tool.CancelAction();}
        }
        else if(requested && view!=null && actor!=null && tool.Kilograms<tool.CapacityKg-.0001f)
        {
            float amount=tool.VacuumCollect(view,actor,suctionKgPerSecond*dt,out var point);
            Sucking=amount>0;
            if(Sucking){emission+=dt*180;int n=(int)emission;emission-=n;Emit(point,nozzle.position,false,n);}
        }
        requested=false;
        suctionBlend=Mathf.MoveTowards(suctionBlend,Sucking?1:0,dt*6);
        shownFill=Mathf.MoveTowards(shownFill,Mathf.Clamp01(tool.Kilograms/tool.CapacityKg),dt*2);
        UpdateFill(shownFill);appearance.Set(tool.Cargo);fillRenderer.SetPropertyBlock(appearance.Block);UpdateParticles(dt);
    }
    private void Emit(Vector3 from,Vector3 to,bool pour,int count)
    {
        for(int n=0;n<Mathf.Min(count,32);n++)
        {int i=nextParticle++%96;starts[i]=from+Random.insideUnitSphere*.05f;ends[i]=to;ages[i]=0;pouring[i]=pour;}
    }
    private void UpdateParticles(float dt)
    {
        int count=0;
        for(int i=0;i<96;i++)
        {
            ages[i]+=dt/.45f;if(ages[i]>=1)continue;float t=ages[i];
            Vector3 end=pouring[i]?ends[i]:nozzle.position;
            Vector3 p=Vector3.Lerp(starts[i],end,t*t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*(pouring[i]?.06f:.12f));
            // World particles end at the mouth. Reservoir kernels enter through the internal inlet.
            if(!pouring[i]&&t>.85f)p=Vector3.Lerp(p,nozzle.position,(t-.85f)/.15f);
            particles[count++]=new ParticleSystem.Particle{position=p,startSize=.016f,startColor=new Color(1,.7f,.16f),remainingLifetime=1,startLifetime=1,rotation3D=new Vector3(t*540,i*37,0)};
        }
        stream.SetParticles(particles,count);
    }
    private void BuildPackedGrain()
    {
        var points=new System.Collections.Generic.List<Vector3>();
        float volume=Mathf.PI*fillRadius*fillRadius*fillLength;
        float spacing=Mathf.Pow(volume/850f,1f/3f);
        var random=new System.Random(761);
        float yMin=uprightCylinder?-fillLength*.5f:-fillRadius;
        float yMax=-yMin;
        float zMax=uprightCylinder?fillRadius:fillLength*.5f;
        for(float y=yMin+spacing*.5f;y<yMax;y+=spacing)
        for(float z=-zMax+spacing*.5f;z<zMax;z+=spacing)
        for(float x=-fillRadius+spacing*.5f;x<fillRadius;x+=spacing)
        {
            float radial=uprightCylinder?x*x+z*z:x*x+y*y;
            if(radial>(fillRadius-spacing*.3f)*(fillRadius-spacing*.3f))continue;
            points.Add(new Vector3(x,y,z)+new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*spacing*.18f);
        }
        if(reservoirPoints != null && reservoirPoints.Length > 0)
        {
            points.Clear();points.AddRange(reservoirPoints);spacing=reservoirSpacing;
            for(int i=0;i<points.Count;i++)points[i]+=new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*spacing*.25f;
        }
        points.Sort((a,b)=>(a.y+new Vector2(a.x,a.z).magnitude*.12f).CompareTo(b.y+new Vector2(b.x,b.z).magnitude*.12f));packedCount=points.Count;
        settledPositions=points.ToArray();arrivalTimes=new float[packedCount];
        var original=kernelMesh.vertices;var normals=kernelMesh.normals;var uv=kernelMesh.uv;var indices=kernelMesh.triangles;kernelIndexCount=indices.Length;
        var vertices=new Vector3[points.Count*original.Length];var outputNormals=new Vector3[vertices.Length];var outputUV=new Vector2[vertices.Length];var triangles=new int[points.Count*indices.Length];
        float size=spacing*.95f/Mathf.Max(kernelMesh.bounds.size.x,kernelMesh.bounds.size.y,kernelMesh.bounds.size.z);
        for(int k=0;k<points.Count;k++)
        {
            var rotation=Quaternion.Euler((float)random.NextDouble()*360,(float)random.NextDouble()*360,(float)random.NextDouble()*360);
            for(int i=0;i<original.Length;i++){int index=k*original.Length+i;vertices[index]=points[k]+rotation*(original[i]*size);outputNormals[index]=rotation*normals[i];if(uv.Length==original.Length)outputUV[index]=uv[i];}
            for(int i=0;i<indices.Length;i++)triangles[k*indices.Length+i]=indices[i]+k*original.Length;
        }
        verticesPerKernel=original.Length;settledVertices=vertices;animatedVertices=(Vector3[])vertices.Clone();
        fillMesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        fillMesh.vertices=vertices;fillMesh.normals=outputNormals;fillMesh.uv=outputUV;fillMesh.triangles=triangles;fillMesh.RecalculateBounds();
    }
    private void UpdateFill(float fill)
    {
        int count=Mathf.Clamp(Mathf.CeilToInt(packedCount*fill),0,packedCount);
        fillRenderer.enabled=count>0;
        if(count==displayedCount&&!grainMoving)return;
        grainMoving=false;
        for(int k=displayedCount;k<count;k++)arrivalTimes[k]=clock;
        Vector3 inlet=cylinderInlet!=null?fillFrame.InverseTransformPoint(cylinderInlet.position):new Vector3(0,0,-fillLength*.4f);
        System.Array.Copy(settledVertices,animatedVertices,settledVertices.Length);
        for(int k=0;k<count;k++)
        {
            float t=Mathf.Clamp01((clock-arrivalTimes[k])/.65f);
            if(t>=1)continue;
            grainMoving=true;
            float eased=t*t*(3-2*t);
            float swirl=Mathf.Sin(t*Mathf.PI)*fillRadius*.18f;
            Vector3 position=Vector3.Lerp(inlet,settledPositions[k],eased)+new Vector3(Mathf.Sin(t*13+k)*swirl,Mathf.Cos(t*13+k)*swirl,0);
            Vector3 offset=position-settledPositions[k];
            for(int i=0;i<verticesPerKernel;i++)animatedVertices[k*verticesPerKernel+i]+=offset;
        }
        fillMesh.vertices=animatedVertices;
        displayedCount=count;
        fillMesh.SetSubMesh(0,new UnityEngine.Rendering.SubMeshDescriptor(0,count*kernelIndexCount),UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
    }
    public void Cancel()
    {
        requested=Sucking=Emptying=false;suctionBlend=0;
        if(!initialized)return;
        dustTray.localPosition=trayPosition;dustTray.localRotation=trayRotation;
        for(int i=0;i<ages.Length;i++)ages[i]=1;
        stream.Clear();
    }
    private void OnDisable()=>Cancel();
    private void OnDestroy(){if(fillMesh!=null){if(Application.isPlaying)Destroy(fillMesh);else DestroyImmediate(fillMesh);}}
}
