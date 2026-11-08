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
    public Vector3 PositionOffset => new Vector3(-.08f * pourPose, .10f * pourPose - .015f * suctionBlend, .07f * suctionBlend);
    public Quaternion RotationOffset => Quaternion.Euler(-55f * pourPose - 3f * suctionBlend, -12f * pourPose, -35f * pourPose + Mathf.Sin(clock * 40) * .18f * suctionBlend);
    private bool requested, initialized;
    private bool pourSoundPlayed;
    private Camera view;
    private Transform actor;
    private float pourPose;
    private float nextIntakeSound;
    public const float EmptyDuration = 3.2f;
    public const float PourStart = .4f, PourEnd = .8f;
    private float clock, emptyTime, suctionBlend, shownFill, emission;
    private Vector3 trayPosition;
    private Quaternion trayRotation;
    private MeshRenderer fillRenderer;
    private readonly Matrix4x4[] streamMatrices = new Matrix4x4[96];
    private readonly float[] species = new float[96], drawnSpecies = new float[96];
    private readonly Matrix4x4[] fillMatrices = new Matrix4x4[1023];
    private readonly float[] beforePour = new float[GrainCargo.TypeCount];
    private readonly GrainCargo pouredGrain = new GrainCargo();
    private readonly GrainAppearance streamAppearance = new GrainAppearance();
    private static readonly int GrainTypeId = Shader.PropertyToID("_GrainType");
    private readonly Vector3[] starts = new Vector3[96], ends = new Vector3[96];
    private readonly float[] ages = new float[96];
    private readonly bool[] pouring = new bool[96];
    private int nextParticle, packedCount, displayedCount;
    private Vector3[] settledPositions;
    private Quaternion[] settledRotations;
    private float kernelScale;
    private float[] arrivalTimes;
    private readonly GrainAppearance appearance = new GrainAppearance();

    private void OnEnable()
    {
        SaveSystem.MoneyChanged += OnUpgradeChanged;
        ApplyUpgrades();
        // Never display the authored full-reservoir preview before the first cargo update.
        if (fillFrame != null && fillFrame.TryGetComponent<MeshRenderer>(out var renderer))
            renderer.enabled = false;
    }

    private void OnUpgradeChanged(int _) => ApplyUpgrades();

    internal void ApplyUpgrades()
    {
        var owner = GetComponentInParent<ShovelTool>(true);
        var settings = owner != null ? owner.ProgressionSettings : null;
        int level = SaveSystem.Data?.shovelUpgradeLevel ?? 0;
        if (settings == null || !settings.IsConfigured || !settings.IsVacuum(level)) return;
        capacityKg = settings.Capacity(level);
        suctionKgPerSecond = settings.SuctionSpeed(level);
    }

    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        trayPosition = dustTray.localPosition; trayRotation = dustTray.localRotation;
        // The upright model's tray is only the lid, not its grain reservoir.
        // Keep the packed grain in the cylinder when the lid opens.
        if (uprightCylinder && fillFrame.IsChildOf(dustTray))
            fillFrame.SetParent(transform, true);
        fillRenderer = fillFrame.GetComponent<MeshRenderer>();
        fillRenderer.enabled = false;
        BuildPackedGrain();
        for(int i=0;i<ages.Length;i++)ages[i]=1;
    }
    public void RequestSuction(Camera camera, Transform player) { view=camera;actor=player;requested=true; }
    public void BeginEmpty() { Initialize(); Emptying=true;emptyTime=0;requested=false;Sucking=false;pourSoundPlayed=false;GameplayAudio.Instance?.VacuumMoving(transform,false,uprightCylinder); }
    public void Tick(ShovelTool tool,float dt)
    {
        Initialize();clock+=dt;Sucking=false;
        // Run the motor for the held suction input, not only frames that remove grain.
        // Empty contact cells used to stop/restart the clip before it became audible.
        bool motorRunning = requested && !Emptying && view != null && actor != null;
        if(Emptying)
        {
            emptyTime+=dt;float p=Mathf.Clamp01(emptyTime/EmptyDuration);
            float pull=p<.22f?Mathf.SmoothStep(0,1,p/.22f):p<.92f?1:1-Mathf.SmoothStep(0,1,(p-.92f)/.08f);
            float tip=p<.22f?0:p<PourStart?Mathf.SmoothStep(0,1,(p-.22f)/(PourStart-.22f)):p<PourEnd?1:1-Mathf.SmoothStep(0,1,(p-PourEnd)/.12f);
            pourPose=tip;
            dustTray.localPosition=trayPosition-trayRemoval*.65f*pull;
            // Only the lid opens independently; the whole vacuum performs the pour rotation.
            dustTray.localRotation=trayRotation*Quaternion.Euler(uprightCylinder ? trayTilt*pull : Vector3.zero);
            for (int i = 0; i < GrainCargo.TypeCount; i++) beforePour[i] = tool.Cargo[(GrainType)i];
            float moved=tool.VacuumEmptyStep(p,dt);
            pouredGrain.Clear();
            for (int i = 0; i < GrainCargo.TypeCount; i++)
                pouredGrain.Add((GrainType)i, beforePour[i] - tool.Cargo[(GrainType)i]);
            if (moved > 0 && !pourSoundPlayed) { pourSoundPlayed = true; tool.PlayPourSound(); }
            if(moved>0)Emit(fillFrame.TransformPoint(uprightCylinder ? Vector3.up*fillLength*.5f : Vector3.forward*fillLength*.5f),tool.VacuumDropPoint,true,Mathf.Clamp(Mathf.CeilToInt(dt*500),1,32),pouredGrain);
            if(p>=1){Cancel();tool.CancelAction();}
        }
        else if(requested && view!=null && actor!=null && tool.Kilograms<tool.CapacityKg-.0001f)
        {
            float amount=tool.VacuumCollect(view,actor,suctionKgPerSecond*dt,out var point);
            Sucking=amount>0;
            if(Sucking)
            {
                emission+=dt*180;int n=(int)emission;emission-=n;
                Emit(point,nozzle.position,false,n,tool.VacuumCollectedCargo);
                if (clock >= nextIntakeSound)
                {
                    GameplayAudio.Instance?.VacuumGrainIntake();
                    nextIntakeSound = clock + 1.5f;
                }
            }
        }
        requested=false;
        GameplayAudio.Instance?.VacuumMoving(transform,motorRunning,uprightCylinder);
        if (!motorRunning) nextIntakeSound = clock;
        suctionBlend=Mathf.MoveTowards(suctionBlend,Sucking?1:0,dt*6);
        shownFill=Emptying ? Mathf.Clamp01(tool.Kilograms/tool.CapacityKg) : Mathf.MoveTowards(shownFill,Mathf.Clamp01(tool.Kilograms/tool.CapacityKg),dt*2);
        if (tool.Kilograms <= 0f) shownFill = 0f;
        appearance.Set(tool.Cargo);UpdateFill(shownFill);UpdateParticles(dt);
    }
    private void Emit(Vector3 from,Vector3 to,bool pour,int count,GrainCargo grain)
    {
        for(int n=0;n<Mathf.Min(count,32);n++)
        {int i=nextParticle++%96;starts[i]=from+Random.insideUnitSphere*.05f;ends[i]=to;ages[i]=0;pouring[i]=pour;species[i]=(float)grain.Sample(Random.value);}
    }
    private void UpdateParticles(float dt)
    {
        int count=0;
        for(int i=0;i<96;i++)
        {
            float lifetime=pouring[i] ? .18f : .45f;
            ages[i]+=dt/lifetime;if(ages[i]>=1)continue;float t=ages[i];
            if(pouring[i] && Emptying && emptyTime>=PourEnd*EmptyDuration){ages[i]=1;continue;}
            Vector3 end=pouring[i]?ends[i]:nozzle.position;
            Vector3 p=Vector3.Lerp(starts[i],end,t*t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*(pouring[i]?.06f:.12f));
            // World particles end at the mouth. Reservoir kernels enter through the internal inlet.
            if(!pouring[i]&&t>.85f)p=Vector3.Lerp(p,nozzle.position,(t-.85f)/.15f);
            streamMatrices[count] = Matrix4x4.TRS(p, Quaternion.Euler(t*540,i*37,0), Vector3.one * .008f);
            drawnSpecies[count++] = species[i];
        }
        if (count == 0) return;
        streamAppearance.Block.SetFloatArray(GrainTypeId, drawnSpecies);
        Graphics.DrawMeshInstanced(kernelMesh, 0, kernelMaterial, streamMatrices, count,
            streamAppearance.Block, UnityEngine.Rendering.ShadowCastingMode.Off, true, 0);
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
        kernelScale=spacing*.95f/Mathf.Max(kernelMesh.bounds.size.x,kernelMesh.bounds.size.y,kernelMesh.bounds.size.z);
        settledRotations=new Quaternion[packedCount];
        for(int k=0;k<packedCount;k++)
            settledRotations[k]=Quaternion.Euler((float)random.NextDouble()*360,(float)random.NextDouble()*360,(float)random.NextDouble()*360);
    }
    private void UpdateFill(float fill)
    {
        int count=Mathf.Clamp(Mathf.CeilToInt(packedCount*fill),0,packedCount);
        // The grain shader shapes each kernel around its own origin. Combining all
        // reservoir vertices into one mesh expands non-corn grains outside the tank.
        fillRenderer.enabled=false;
        for(int k=displayedCount;k<count;k++)arrivalTimes[k]=clock;
        Vector3 inlet=cylinderInlet!=null?fillFrame.InverseTransformPoint(cylinderInlet.position):new Vector3(0,0,-fillLength*.4f);
        Matrix4x4 world=fillFrame.localToWorldMatrix;
        for(int start=0;start<count;start+=fillMatrices.Length)
        {
            int batch=Mathf.Min(fillMatrices.Length,count-start);
            for(int i=0;i<batch;i++)
            {
                int k=start+i;
                float t=Mathf.Clamp01((clock-arrivalTimes[k])/.65f);
                float eased=t*t*(3-2*t);
                float swirl=Mathf.Sin(t*Mathf.PI)*fillRadius*.18f;
                Vector3 position=Vector3.Lerp(inlet,settledPositions[k],eased)+new Vector3(Mathf.Sin(t*13+k)*swirl,Mathf.Cos(t*13+k)*swirl,0);
                fillMatrices[i]=world*Matrix4x4.TRS(position,settledRotations[k],Vector3.one*kernelScale);
            }
            Graphics.DrawMeshInstanced(kernelMesh,0,kernelMaterial,fillMatrices,batch,
                appearance.Block,UnityEngine.Rendering.ShadowCastingMode.Off,true,fillFrame.gameObject.layer);
        }
        displayedCount=count;
    }
    public void Cancel()
    {
        requested=Sucking=Emptying=false;suctionBlend=0;pourPose=0;
        GameplayAudio.Instance?.VacuumMoving(transform,false,uprightCylinder);
        if(!initialized)return;
        dustTray.localPosition=trayPosition;dustTray.localRotation=trayRotation;
        for(int i=0;i<ages.Length;i++)ages[i]=1;

    }
    private void OnDisable()
    {
        SaveSystem.MoneyChanged -= OnUpgradeChanged;
        Cancel();
    }
}
