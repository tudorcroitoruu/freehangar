using UnityEngine;

/// <summary>Returns loose cargo which escaped physics to its last supported position.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(GrainContainer))]
public sealed class CargoFallRecovery : MonoBehaviour
{
    private GrainContainer container;
    private Vector3 safePosition;
    private Quaternion safeRotation;
    private bool hasSupport;
    private float nextProbe;
    private static CargoRecoveryVolume volume;

    private void Awake()
    {
        container = GetComponent<GrainContainer>();
        safePosition = transform.position;
        safeRotation = transform.rotation;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        if (volume == null)
        {
            float floor = transform.position.y;
            foreach (var terrain in Terrain.activeTerrains)
                if (terrain != null) floor = Mathf.Min(floor, terrain.transform.position.y);
            var host = new GameObject("Cargo recovery trigger below map");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, gameObject.scene);
            host.transform.position = new Vector3(transform.position.x, floor - 30f, transform.position.z);
            var box = host.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(10000, 40, 10000);
            volume = host.AddComponent<CargoRecoveryVolume>();
        }
        ProbeSupport();
    }

    public void RememberSupport(Vector3 point)
    {
        if (container == null) container = GetComponent<GrainContainer>();
        var bounds = container.WorldBounds;
        safePosition = transform.position + point - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) + Vector3.up * .04f;
        safeRotation = transform.rotation;
        hasSupport = true;
    }

    private void FixedUpdate()
    {
        if (container.IsCarried || VehicleCargoOwner.Resolve(container) != null || WorldSaveRuntime.IsRestoring) return;
        // Also catch objects which cross the entire trigger in a single physics step.
        if (volume != null && container.WorldBounds.max.y < volume.Top)
        { Recover(); return; }
        if (Time.time < nextProbe) return;
        nextProbe = Time.time + .5f;
        ProbeSupport();
    }

    private void ProbeSupport()
    {
        var bounds = container.WorldBounds;
        var origin = new Vector3(bounds.center.x, bounds.min.y + .15f, bounds.center.z);
        float nearest = float.PositiveInfinity;
        RaycastHit support = default;
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, hasSupport ? .35f : 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.normal.y < .7f || hit.distance >= nearest ||
                hit.collider.attachedRigidbody != null || hit.collider.GetComponentInParent<GrainContainer>() != null ||
                VehicleCargoOwner.Resolve(hit.collider) != null) continue;
            nearest = hit.distance; support = hit;
        }
        if (nearest < float.PositiveInfinity) RememberSupport(support.point);
    }

    public bool Recover()
    {
        if (container == null || container.IsCarried || VehicleCargoOwner.Resolve(container) != null ||
            WorldSaveRuntime.IsRestoring || !hasSupport) return false;
        var body = GetComponent<Rigidbody>();
        if (body == null) return false;
        body.isKinematic = true;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(safePosition, safeRotation);
        // Re-find the surface in case grain at the checkpoint has been scooped away.
        var bounds = container.WorldBounds;
        float highest = float.NegativeInfinity;
        var origin = new Vector3(bounds.center.x, bounds.min.y + .75f, bounds.center.z);
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 12, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.normal.y < .7f ||
                hit.collider.attachedRigidbody != null || VehicleCargoOwner.Resolve(hit.collider) != null ||
                hit.collider.GetComponentInParent<GrainContainer>() != null) continue;
            highest = Mathf.Max(highest, hit.point.y);
        }
        if (highest > float.NegativeInfinity) transform.position += Vector3.up * (highest + .04f - bounds.min.y);
        container.SetCarried(false);
        body.position = transform.position; body.rotation = transform.rotation;
        body.isKinematic = false; body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
        body.WakeUp();
        nextProbe = Time.time + .5f;
        return true;
    }
}

public sealed class CargoRecoveryVolume : MonoBehaviour
{
    public float Top => transform.position.y + 20f;
    private void OnTriggerEnter(Collider other) => other.GetComponentInParent<CargoFallRecovery>()?.Recover();
}
