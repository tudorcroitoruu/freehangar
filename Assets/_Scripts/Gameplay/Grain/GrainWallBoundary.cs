using System.Collections.Generic;
using UnityEngine;

/// <summary>Static upright wall half-planes and a clipped height-field topology.</summary>
public sealed class GrainWallBoundary
{
    public readonly List<Plane> Walls = new List<Plane>(8);
    public readonly List<Vector3> Positions = new List<Vector3>();
    public readonly List<int> Triangles = new List<int>();
    public readonly List<Vector2> UV = new List<Vector2>();
    private readonly List<Binding> bindings = new List<Binding>();
    private readonly List<Corner> polygon = new List<Corner>(12), clipped = new List<Corner>(12);
    private struct Corner { public Vector3 point, weights; public Corner(Vector3 p, Vector3 w) { point = p; weights = w; } }
    private struct Binding { public int a, b, c; public Vector3 weights; }

    public static void Detect(Transform pile, float size, float height, LayerMask mask, List<Plane> result, RaycastHit[] hits)
    {
        result.Clear();
        Physics.SyncTransforms();
        Vector3 scale = pile.lossyScale;
        float reach = size * .5f * new Vector2(scale.x, scale.z).magnitude + .08f;
        for (int level = 0; level < 2; level++)
        for (int ray = 0; ray < 32; ray++)
        {
            float angle = ray * Mathf.PI * 2f / 32f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 origin = pile.position + Vector3.up * (level == 0 ? .25f : Mathf.Max(.5f, height * scale.y * .6f));
            int count = Physics.RaycastNonAlloc(origin, direction, hits, reach, mask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity; RaycastHit selected = default;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i]; var collider = hit.collider;
                if (collider.transform.IsChildOf(pile) || collider.attachedRigidbody != null ||
                    collider.GetComponentInParent<GrainContainer>() != null || collider.GetComponentInParent<PlayerController>() != null ||
                    collider.GetComponentInParent<GrainPile>() != null || Mathf.Abs(hit.normal.y) > .12f) continue;
                Bounds bounds = collider.bounds;
                if (bounds.size.y < 1f || Mathf.Max(bounds.size.x, bounds.size.z) < 1.5f || hit.distance >= nearest) continue;
                nearest = hit.distance; selected = hit;
            }
            if (float.IsPositiveInfinity(nearest)) continue;
            Vector3 normal = Vector3.ProjectOnPlane(selected.normal, Vector3.up).normalized;
            if (Vector3.Dot(normal, pile.position - selected.point) < 0) normal = -normal;
            Plane plane = new Plane(normal, selected.point);
            bool duplicate = false;
            for (int i = 0; i < result.Count; i++)
            {
                if (Vector3.Dot(result[i].normal, normal) < .999f) continue;
                // Parallel layers of the same wall: retain the innermost face.
                if (plane.GetDistanceToPoint(pile.position) < result[i].GetDistanceToPoint(pile.position)) result[i] = plane;
                duplicate = true; break;
            }
            if (!duplicate && result.Count < 12) result.Add(plane);
        }
    }

    public bool Matches(List<Plane> other)
    {
        if (Walls.Count != other.Count) return false;
        for (int i = 0; i < Walls.Count; i++)
            if (Vector3.Dot(Walls[i].normal, other[i].normal) < .99999f || Mathf.Abs(Walls[i].distance - other[i].distance) > .001f) return false;
        return true;
    }

    public bool Contains(Vector3 point, float margin)
    {
        foreach (Plane wall in Walls) if (wall.GetDistanceToPoint(point) < margin - .0001f) return false;
        return true;
    }

    public void Build(GrainHeightField field, Transform transform, List<Plane> walls, float clearance)
    {
        Walls.Clear(); Walls.AddRange(walls);
        Positions.Clear(); Triangles.Clear(); UV.Clear(); bindings.Clear();
        var localPlanes = new List<Plane>(walls.Count);
        foreach (Plane wall in walls)
        {
            Vector3 point = -wall.distance * wall.normal + wall.normal * clearance;
            Vector3 normal = transform.localToWorldMatrix.transpose.MultiplyVector(wall.normal).normalized;
            localPlanes.Add(new Plane(normal, transform.InverseTransformPoint(point)));
        }
        float[] areas = new float[field.Heights.Length];
        for (int t = 0; t < field.Triangles.Length; t += 3)
        {
            int a = field.Triangles[t], b = field.Triangles[t + 1], c = field.Triangles[t + 2];
            polygon.Clear();
            polygon.Add(new Corner(Point(a, field), Vector3.right));
            polygon.Add(new Corner(Point(b, field), Vector3.up));
            polygon.Add(new Corner(Point(c, field), Vector3.forward));
            foreach (Plane plane in localPlanes)
            {
                clipped.Clear();
                for (int i = 0; i < polygon.Count; i++)
                {
                    Corner from = polygon[i], to = polygon[(i + 1) % polygon.Count];
                    float da = plane.GetDistanceToPoint(from.point), db = plane.GetDistanceToPoint(to.point);
                    if (da >= 0) clipped.Add(from);
                    if ((da >= 0) != (db >= 0))
                    {
                        float f = da / (da - db);
                        clipped.Add(new Corner(Vector3.Lerp(from.point, to.point, f), Vector3.Lerp(from.weights, to.weights, f)));
                    }
                }
                polygon.Clear(); polygon.AddRange(clipped);
            }
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                Corner x = polygon[0], y = polygon[i], z = polygon[i + 1];
                float area = Mathf.Abs(Vector3.Cross(y.point - x.point, z.point - x.point).y) * .5f;
                if (area < .00000001f) continue;
                Vector3 weights = (x.weights + y.weights + z.weights) * (area / 3f);
                areas[a] += weights.x; areas[b] += weights.y; areas[c] += weights.z;
                Add(x, a, b, c); Add(y, a, b, c); Add(z, a, b, c);
            }
        }
        field.SetAreaWeights(areas);
        UpdateHeights(field, 0);
    }

    private static Vector3 Point(int i, GrainHeightField f) => new Vector3((i % f.Resolution) * f.Spacing - f.Size * .5f, 0, (i / f.Resolution) * f.Spacing - f.Size * .5f);
    private void Add(Corner corner, int a, int b, int c)
    {
        Triangles.Add(Positions.Count); Positions.Add(corner.point); UV.Add(new Vector2(corner.point.x, corner.point.z));
        bindings.Add(new Binding { a = a, b = b, c = c, weights = corner.weights });
    }
    public void UpdateHeights(GrainHeightField field, float recess)
    {
        for (int i = 0; i < Positions.Count; i++)
        {
            Binding b = bindings[i]; Vector3 p = Positions[i];
            p.y = b.weights.x * field.Heights[b.a] + b.weights.y * field.Heights[b.b] + b.weights.z * field.Heights[b.c] - recess;
            Positions[i] = p;
        }
        Triangles.Clear();
        for (int i = 0; i < Positions.Count; i += 3)
            if (Positions[i].y + recess > .00001f || Positions[i + 1].y + recess > .00001f || Positions[i + 2].y + recess > .00001f)
            { Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2); }
    }
}
