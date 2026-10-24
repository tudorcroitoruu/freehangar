using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A bounded sphere solver inside a tapered bowl, plus a recycled ballistic pouring stream.</summary>
[DefaultExecutionOrder(100), DisallowMultipleComponent]
public sealed class ShovelGrainPhysics : MonoBehaviour
{
    [SerializeField] private Mesh kernelMesh;
    [SerializeField] private Material kernelMaterial;
    [SerializeField, Range(32, 256)] private int maximumKernels = 192;
    [SerializeField, Range(.055f, .1f)] private float maximumLoadHeight = .075f;
    [SerializeField, HideInInspector] private float[] bowlFloor;
    private const float Radius = .012f;
    private const int StreamCapacity = 256;
    private const int PackedCapacity = 432;
    private readonly Vector3[] packedPositions = new Vector3[PackedCapacity];
    private readonly Quaternion[] packedRotations = new Quaternion[PackedCapacity];
    private Vector3[] positions, velocities;
    private Quaternion[] rotations;
    private Matrix4x4[] matrices;
    private int count;
    private readonly Vector3[] streamPosition = new Vector3[StreamCapacity];
    private readonly Vector3[] streamVelocity = new Vector3[StreamCapacity];
    private readonly float[] streamLife = new float[StreamCapacity];
    private readonly bool[] cosmeticStream = new bool[StreamCapacity];
    private readonly Matrix4x4[] streamMatrices = new Matrix4x4[StreamCapacity];
    private int streamHead, streamCount;
    private float requestedFill;
    private float spawnCredit;
    private Vector3 previousPosition, previousVelocity;
    private Quaternion previousRotation;
    private float displayedFill, shakeCredit;
    private float quietTime;
    private bool loadSleeping;
    private Vector3 pourDestination;
    private Collider receiver;
    private System.Random random;

    public int KernelCount => count;
    public int AirborneCount => streamCount;
    public float MaximumBowlViolation { get; private set; }
    public int PackedKernelCount { get; private set; }
    public int CosmeticEmittedCount { get; private set; }
    public float HighestLoadPoint { get; private set; }
    public bool IsLoadSleeping => loadSleeping;

    private void Awake()
    {
        positions = new Vector3[maximumKernels]; velocities = new Vector3[maximumKernels];
        rotations = new Quaternion[maximumKernels]; matrices = new Matrix4x4[maximumKernels + PackedCapacity];
        random = new System.Random(213);
        previousPosition = transform.position;
        previousRotation = transform.rotation;
        for (int layer = 0; layer < 3; layer++)
        for (int z = 0; z < 12; z++)
        for (int x = 0; x < 12; x++)
        {
            int i = layer * 144 + z * 12 + x;
            float pz = Mathf.Lerp(-.155f, .16f, (z + Random(.2f, .8f)) / 12f);
            float px = Mathf.Lerp(-Width(pz), Width(pz), (x + Random(.15f, .85f)) / 12f);
            packedPositions[i] = new Vector3(px, .011f + layer * .021f + Random(-.006f, .006f), pz);
            packedRotations[i] = Quaternion.Euler(Random(-15, 15), Random(0, 360), Random(-15, 15));
        }
    }

    public void SetFill(float fill)
    {
        float next = Mathf.Clamp01(fill);
        if (Mathf.Abs(next - requestedFill) > .00001f) { loadSleeping = false; quietTime = 0; }
        requestedFill = next;
    }

    public void SetPourTarget(Vector3 worldPoint, Collider target)
    {
        pourDestination = worldPoint;
        receiver = target;
    }

    private float Random(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

    private void LateUpdate()
    {
        if (positions == null || kernelMesh == null || kernelMaterial == null) return;
        if (Time.deltaTime > 0) Simulate(Mathf.Min(Time.deltaTime, .05f));
        HighestLoadPoint = 0;
        for (int i = 0; i < count; i++)
        {
            matrices[i] = transform.localToWorldMatrix * Matrix4x4.TRS(positions[i], rotations[i], new Vector3(.013f, .009f, .017f));
            HighestLoadPoint = Mathf.Max(HighestLoadPoint, positions[i].y + .018f);
        }
        PackedKernelCount = 0;
        for (int i = 0; i < PackedCapacity; i++)
        {
            Vector3 p = packedPositions[i];
            float reveal = Mathf.Clamp01((displayedFill * .068f - p.y) / .012f);
            if (reveal <= 0) continue;
            p.y += SampleFloor(0, p.z);
            if (p.y < Floor(p.x, p.z) + .008f || p.y + .02f > maximumLoadHeight) continue;
            HighestLoadPoint = Mathf.Max(HighestLoadPoint, p.y + .02f);
            matrices[count + PackedKernelCount++] = transform.localToWorldMatrix * Matrix4x4.TRS(p, packedRotations[i],
                new Vector3(.016f, .011f, .019f) * reveal);
        }
        if (count + PackedKernelCount > 0) Graphics.DrawMeshInstanced(kernelMesh, 0, kernelMaterial, matrices, count + PackedKernelCount, null,
            ShadowCastingMode.On, true, gameObject.layer, null, LightProbeUsage.Off);
        streamCount = 0;
        for (int i = 0; i < StreamCapacity; i++)
        {
            if (streamLife[i] <= 0) continue;
            float scale = Mathf.Clamp01(streamLife[i] * 5f);
            streamMatrices[streamCount++] = Matrix4x4.TRS(streamPosition[i],
                Quaternion.Euler(streamLife[i] * 170f, i * 71f, streamLife[i] * 90f), new Vector3(.013f, .009f, .017f) * scale);
        }
        if (streamCount > 0) Graphics.DrawMeshInstanced(kernelMesh, 0, kernelMaterial, streamMatrices, streamCount, null,
            ShadowCastingMode.Off, true, gameObject.layer, null, LightProbeUsage.Off);
    }

    private void Simulate(float dt)
    {
        displayedFill = Mathf.MoveTowards(displayedFill, requestedFill, dt * 2.5f);
        int desired = Mathf.RoundToInt(requestedFill * maximumKernels);
        spawnCredit = Mathf.Min(spawnCredit + dt * 750f, 32f);
        while (count < desired && spawnCredit >= 1f)
        {
            // Material enters over the cutting edge, then rolls into the bowl under gravity.
            // It never appears as an already formed heap in the middle of the shovel.
            positions[count] = new Vector3(Random(-.15f, .15f), displayedFill * .065f + Random(.018f, .04f), .15f);
            velocities[count] = new Vector3(Random(-.08f, .08f), -.12f, Random(-.65f, -.35f));
            rotations[count] = Quaternion.Euler(Random(0, 180), Random(0, 360), Random(0, 180));
            count++; spawnCredit--;
        }
        while (count > desired)
        {
            // Let the frontmost kernels leave first, then recycle their slots.
            int front = 0;
            for (int i = 1; i < count; i++) if (positions[i].z > positions[front].z) front = i;
            Emit(transform.TransformPoint(new Vector3(positions[front].x, .005f, .17f)));
            count--;
            positions[front] = positions[count]; velocities[front] = velocities[count]; rotations[front] = rotations[count];
        }
        Vector3 worldVelocity = (transform.position - previousPosition) / Mathf.Max(dt, .001f);
        Vector3 rawAcceleration = (worldVelocity - previousVelocity) / Mathf.Max(dt, .001f);
        float angularSpeed = Quaternion.Angle(previousRotation, transform.rotation) / Mathf.Max(dt, .001f);
        bool teleported = (transform.position - previousPosition).sqrMagnitude > .65f * .65f;
        if (!teleported && count > 8)
        {
            float excitement = Mathf.Clamp01(Mathf.Max((angularSpeed - 160f) / 260f, (rawAcceleration.magnitude - 22f) / 55f));
            shakeCredit = Mathf.Min(2f, shakeCredit + excitement * dt * 10f);
            int activeCosmetic = 0;
            for (int i = 0; i < StreamCapacity; i++)
                if (cosmeticStream[i] && streamLife[i] > 0) activeCosmetic++;
            while (shakeCredit >= 1f)
            {
                if (activeCosmetic >= 8) { shakeCredit = 0; break; }
                int kernel = random.Next(count);
                Vector3 origin = transform.TransformPoint(positions[kernel] + Vector3.up * .005f);
                Vector3 velocity = Vector3.ProjectOnPlane(Vector3.ClampMagnitude(worldVelocity, 3f), Vector3.up) * .35f
                    + Vector3.up * Random(.25f, .55f) + Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized * Random(-.4f, .4f);
                EmitCosmetic(origin, velocity);
                activeCosmetic++;
                velocities[kernel] += Vector3.up * .15f;
                shakeCredit--;
            }
        }
        else shakeCredit = 0;
        Vector3 acceleration = teleported ? Vector3.zero : Vector3.ClampMagnitude(rawAcceleration, 8f);
        Vector3 gravity = transform.InverseTransformDirection(Physics.gravity - acceleration * .3f);
        previousPosition = transform.position; previousVelocity = teleported ? Vector3.zero : worldVelocity;
        previousRotation = transform.rotation;
        bool disturbed = teleported || angularSpeed > 25f || worldVelocity.sqrMagnitude > .16f || rawAcceleration.sqrMagnitude > 16f;
        if (disturbed || count != desired || Mathf.Abs(displayedFill - requestedFill) > .0001f)
        {
            quietTime = 0;
            loadSleeping = false;
        }
        else if (!loadSleeping)
        {
            quietTime += dt;
            if (quietTime >= .6f)
            {
                loadSleeping = true;
                System.Array.Clear(velocities, 0, count);
            }
        }
        float step = dt / 3f;
        for (int sub = 0; !loadSleeping && sub < 3; sub++)
        {
            for (int i = 0; i < count; i++)
            {
                velocities[i] += gravity * step;
                velocities[i] *= Mathf.Exp(-2.5f * step);
                positions[i] += velocities[i] * step;
                rotations[i] = Quaternion.Normalize(Quaternion.Euler(velocities[i].z * step * 400f, 0, -velocities[i].x * step * 400f) * rotations[i]);
                Constrain(i);
            }
            // Small bounded pair set, no allocations, scene searches or PhysX bodies.
            for (int i = 0; i < count; i++)
            for (int j = i + 1; j < count; j++)
            {
                Vector3 difference = positions[j] - positions[i];
                float squared = difference.sqrMagnitude;
                if (squared >= 4f * Radius * Radius || squared < .00000001f) continue;
                float distance = Mathf.Sqrt(squared);
                Vector3 normal = difference / distance;
                Vector3 correction = normal * ((Radius * 2f - distance) * .5f);
                positions[i] -= correction; positions[j] += correction;
                float impact = Vector3.Dot(velocities[j] - velocities[i], normal);
                if (impact < 0) { velocities[i] += normal * impact * .52f; velocities[j] -= normal * impact * .52f; }
            }
            for (int i = 0; i < count; i++) Constrain(i);
        }
        MaximumBowlViolation = 0;
        for (int i = 0; i < count; i++)
            MaximumBowlViolation = Mathf.Max(MaximumBowlViolation, Mathf.Max(Mathf.Abs(positions[i].x) - Width(positions[i].z),
                positions[i].y + .018f - maximumLoadHeight));
        for (int i = 0; i < StreamCapacity; i++)
        {
            if (streamLife[i] <= 0) continue;
            streamLife[i] -= dt;
            Vector3 old = streamPosition[i];
            streamVelocity[i] += Physics.gravity * dt;
            Vector3 delta = streamVelocity[i] * dt;
            if (Physics.Raycast(old, delta.normalized, out RaycastHit hit, delta.magnitude + .006f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!cosmeticStream[i] && receiver != null && hit.collider == receiver) { streamLife[i] = 0; continue; }
                streamPosition[i] = hit.point + hit.normal * .01f;
                streamVelocity[i] = Vector3.Reflect(streamVelocity[i], hit.normal) * .22f;
                streamLife[i] = Mathf.Min(streamLife[i], .5f);
            }
            else streamPosition[i] += delta;
        }
    }

    private static float Width(float z) => Mathf.Lerp(.135f, .197f, Mathf.InverseLerp(-.16f, .17f, z)) - .023f;
    private float Floor(float x, float z)
    {
        // A kernel has volume: use the highest floor under its footprint, not just its centre.
        const float extent = .018f;
        return Mathf.Max(Mathf.Max(SampleFloor(x - extent, z - extent), SampleFloor(x + extent, z - extent)),
            Mathf.Max(SampleFloor(x - extent, z + extent), SampleFloor(x + extent, z + extent)));
    }

    private float SampleFloor(float x, float z)
    {
        if (bowlFloor == null || bowlFloor.Length != 289)
            return Mathf.Lerp(-.031f, -.002f, Mathf.InverseLerp(-.16f, .17f, z)) + .02f * Mathf.Pow(Mathf.Abs(x) / .18f, 2f);
        float gx = Mathf.Clamp((x + .2f) / .4f * 16f, 0, 16), gz = Mathf.Clamp((z + .18f) / .37f * 16f, 0, 16);
        int ix = Mathf.Min((int)gx, 15), iz = Mathf.Min((int)gz, 15);
        return Mathf.Lerp(Mathf.Lerp(bowlFloor[iz * 17 + ix], bowlFloor[iz * 17 + ix + 1], gx - ix),
            Mathf.Lerp(bowlFloor[(iz + 1) * 17 + ix], bowlFloor[(iz + 1) * 17 + ix + 1], gx - ix), gz - iz);
    }

    private void Constrain(int i)
    {
        Vector3 p = positions[i], v = velocities[i];
        // End-wall projection changes the tapered bowl's width; constrain Z first.
        if (p.z < -.155f) { p.z = -.155f; v.z = Mathf.Max(0, v.z); }
        else if (p.z > .165f) { p.z = .165f; v.z = Mathf.Min(0, v.z); }
        float width = Width(p.z);
        if (p.x < -width) { p.x = -width; v.x = Mathf.Max(0, v.x); }
        else if (p.x > width) { p.x = width; v.x = Mathf.Min(0, v.x); }
        // A steep side wall is not a platform: move kernels inward instead of raising
        // the ceiling with the floor and allowing a floating stack above the bowl.
        float ceiling = maximumLoadHeight - .018f;
        if (Floor(p.x, p.z) + .018f > ceiling)
        {
            // Find the nearest valid point along the radial direction instead of
            // repeatedly snapping 25% inward and letting gravity push it back out.
            float low = 0, high = 1;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                float middle = (low + high) * .5f;
                if (Floor(p.x * middle, p.z * middle) + .018f <= ceiling) low = middle;
                else high = middle;
            }
            Vector3 normal = new Vector3(p.x, 0, p.z).normalized;
            p.x *= low; p.z *= low;
            v -= normal * Mathf.Max(0, Vector3.Dot(v, normal));
        }
        float floor = Mathf.Min(ceiling - .018f,
            Mathf.Max(Floor(p.x, p.z), SampleFloor(0, p.z) + displayedFill * .047f));
        if (p.y < floor + .018f) { p.y = floor + .018f; v.y = Mathf.Max(0, v.y); v.x *= .86f; v.z *= .86f; }
        if (p.y > ceiling) { p.y = ceiling; v.y = Mathf.Min(0, v.y); }
        positions[i] = p; velocities[i] = v;
    }

    private void Emit(Vector3 position)
    {
        int i = streamHead++ % StreamCapacity;
        cosmeticStream[i] = false;
        streamPosition[i] = position;
        float flightTime = Mathf.Clamp(Mathf.Sqrt(2f * Mathf.Max(.04f, position.y - pourDestination.y) / 9.81f), .09f, .4f);
        streamVelocity[i] = (pourDestination - position - Physics.gravity * (.5f * flightTime * flightTime)) / flightTime;
        streamVelocity[i].y = Mathf.Min(0, streamVelocity[i].y);
        streamVelocity[i] += new Vector3(Random(-.12f, .12f), Random(0, .12f), Random(-.12f, .12f));
        streamLife[i] = 1.5f;
    }

    private void EmitCosmetic(Vector3 position, Vector3 velocity)
    {
        // Extra visual copies: neither the load population nor gameplay kilograms are removed.
        int i = streamHead++ % StreamCapacity;
        cosmeticStream[i] = true;
        streamPosition[i] = position;
        streamVelocity[i] = velocity;
        streamLife[i] = Random(.8f, 1.3f);
        CosmeticEmittedCount++;
    }
}
