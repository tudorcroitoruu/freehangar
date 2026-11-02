using System;
using System.Reflection;
using UnityEngine;

public static class ValidateBodyCollision
{
    public static string Run()
    {
        if (Application.isPlaying) throw new Exception("Run in Edit Mode.");
        BicycleController source = null;
        foreach (var rig in UnityEngine.Object.FindObjectsByType<BicycleController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rig.GetComponent<MotorollerController>() == null) { source = rig; break; }
        if (source == null) throw new Exception("Bicycle missing.");
        GameObject clone = null, post = null, floor = null;
        int checks = 0;
        try
        {
            clone = UnityEngine.Object.Instantiate(source.gameObject);
            clone.name = "Temporary bicycle collision validation";
            clone.transform.SetPositionAndRotation(new Vector3(25000, 1000, 25000), Quaternion.identity);
            var rig = clone.GetComponent<BicycleController>();
            rig.Initialize();
            var body = clone.GetComponentInChildren<BoxCollider>();
            if (body == null) throw new Exception("Missing parked body collider.");
            Physics.SyncTransforms();
            Bounds bounds = body.bounds;
            if (bounds.size.z <= clone.GetComponent<CharacterController>().bounds.size.z) throw new Exception("Body does not cover wheelbase.");
            checks++;
            var sweep = typeof(BicycleController).GetMethod("ConstrainBicycleMotion", BindingFlags.Instance | BindingFlags.NonPublic);
            var turn = typeof(BicycleController).GetMethod("ConstrainBicycleTurn", BindingFlags.Instance | BindingFlags.NonPublic);
            Func<Vector3, Vector3> constrain = motion => (Vector3)sweep.Invoke(rig, new object[] { motion });
            post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.transform.localScale = new Vector3(.15f, 4f, .15f);
            foreach (float direction in new[] { 1f, -1f })
            {
                post.transform.position = new Vector3(bounds.center.x, bounds.center.y,
                    bounds.center.z + direction * (bounds.extents.z + .3f));
                Physics.SyncTransforms();
                Vector3 limited = constrain(Vector3.forward * direction * 3f);
                if (limited.magnitude <= .1f || limited.magnitude > .23f) throw new Exception("Nose/tail sweep failed: " + limited);
                checks++;
            }
            // At full lock the nose must not rotate through a post beside the front wheel.
            post.transform.position = new Vector3(bounds.max.x + .12f, bounds.center.y, bounds.max.z - .15f);
            Physics.SyncTransforms();
            float allowed = (float)turn.Invoke(rig, new object[] { 35f });
            if (allowed >= 35f) throw new Exception("Turning nose passed through post.");
            checks++;
            post.SetActive(false);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.localScale = new Vector3(20f, .2f, 20f);
            floor.transform.position = new Vector3(bounds.center.x, bounds.min.y - .2f, bounds.center.z);
            Physics.SyncTransforms();
            if ((constrain(Vector3.forward * 3f) - Vector3.forward * 3f).sqrMagnitude > .00001f)
                throw new Exception("Clear path blocked by own colliders or floor.");
            checks++;
            rig.SetRidden(true);
            if (body.enabled) throw new Exception("Parked collider was not disabled while riding.");
            post.SetActive(true);
            post.transform.position = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + .3f);
            Physics.SyncTransforms();
            if (constrain(Vector3.forward * 3f).magnitude > .23f) throw new Exception("Disabled collider lost sweep coverage.");
            checks++;
            rig.SetRidden(false);
            if (!body.enabled) throw new Exception("Parked collider was not restored.");
            checks++;
            return checks + " bicycle body collision checks passed.";
        }
        finally
        {
            if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            if (post != null) UnityEngine.Object.DestroyImmediate(post);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
            Physics.SyncTransforms();
        }
    }
}
