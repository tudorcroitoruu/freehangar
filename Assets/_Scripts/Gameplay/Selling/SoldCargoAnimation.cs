using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns paid cargo until its sale presentation completes, independently of the closed sale UI.</summary>
public sealed class SoldCargoAnimation : MonoBehaviour
{
    private struct Item
    {
        public Transform transform;
        public Vector3 position, scale;
        public Vector3 effectPosition;
        public float effectScale;
    }
    private readonly List<Item> items = new List<Item>();
    private float elapsed;
    private bool completed;
    private bool effectsStarted;
    private GameObject effectPrefab;
    private float effectDuration;
    private const float StartDelay = 1f;
    private const float AnimationDuration = .8f;

    public static SoldCargoAnimation Begin(IEnumerable<GrainContainer> containers, GameObject effectPrefab = null)
    {
        var animation = new GameObject("Sold cargo animation").AddComponent<SoldCargoAnimation>();
        animation.effectPrefab = effectPrefab;
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
            var bounds = container.WorldBounds;
            animation.items.Add(new Item { transform = container.transform,
                position = container.transform.position, scale = container.transform.localScale,
                effectPosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
                effectScale = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z), .35f, 1.5f) });
        }
        return animation;
    }

    private void Update() => Advance(Time.deltaTime);

    private void Advance(float deltaTime)
    {
        elapsed += Mathf.Max(0, deltaTime);
        if (elapsed < StartDelay) return;
        if (!effectsStarted) StartEffects();
        if (completed)
        {
            if (elapsed >= StartDelay + Mathf.Max(AnimationDuration, effectDuration)) Release();
            return;
        }
        float t = Mathf.Clamp01((elapsed - StartDelay) / AnimationDuration);
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
        if (elapsed >= StartDelay + Mathf.Max(AnimationDuration, effectDuration)) Release();
    }

    private void StartEffects()
    {
        effectsStarted = true;
        if (effectPrefab == null) return;
        foreach (var item in items)
        {
            if (item.transform == null) continue;
            var effect = Instantiate(effectPrefab, item.effectPosition, Quaternion.identity, transform);
            effect.transform.localScale = effectPrefab.transform.localScale * item.effectScale;
            effect.SetActive(true);
            foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.loop = false;
                effectDuration = Mathf.Max(effectDuration,
                    (main.startDelay.constantMax + main.duration + main.startLifetime.constantMax) / Mathf.Max(.01f, main.simulationSpeed));
                particles.Play(false);
            }
        }
    }

    private void Release()
    {
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}
