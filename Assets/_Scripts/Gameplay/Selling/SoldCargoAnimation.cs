using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns paid cargo until its sale presentation completes, independently of the closed sale UI.</summary>
public sealed class SoldCargoAnimation : MonoBehaviour
{
    private struct Item
    {
        public Transform transform;
        public Vector3 position, scale;
    }
    private readonly List<Item> items = new List<Item>();
    private float elapsed;
    private bool completed;

    public static SoldCargoAnimation Begin(IEnumerable<GrainContainer> containers)
    {
        var animation = new GameObject("Sold cargo animation").AddComponent<SoldCargoAnimation>();
        foreach (var container in containers)
        {
            if (container == null) continue;
            var wheelbarrow = container.GetComponentInParent<WheelbarrowCargo>();
            container.transform.SetParent(animation.transform, true);
            if (wheelbarrow != null) wheelbarrow.NotifyBagTaken(container);
            container.SetCarried(true);
            container.enabled = false;
            foreach (var collider in container.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in container.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            animation.items.Add(new Item { transform = container.transform,
                position = container.transform.position, scale = container.transform.localScale });
        }
        return animation;
    }

    private void Update() => Advance(Time.deltaTime);

    private void Advance(float deltaTime)
    {
        if (completed) return;
        elapsed += Mathf.Max(0, deltaTime);
        float t = Mathf.Clamp01(elapsed / .8f);
        float pop = t < .22f ? Mathf.Lerp(1, 1.15f, t / .22f)
            : Mathf.Lerp(1.15f, 0, Mathf.SmoothStep(0, 1, (t - .22f) / .78f));
        foreach (var item in items)
        {
            if (item.transform == null) continue;
            item.transform.position = item.position + Vector3.up * (1.4f * Mathf.Sin(t * Mathf.PI * .5f));
            item.transform.localScale = item.scale * pop;
        }
        if (t < 1) return;
        completed = true;
        foreach (var item in items) if (item.transform != null) item.transform.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}
