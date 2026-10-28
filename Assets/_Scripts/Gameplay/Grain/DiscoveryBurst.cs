using TMPro;
using UnityEngine;

/// <summary>Short-lived discovery particles and a readable world-space receipt.</summary>
public sealed class DiscoveryBurst : MonoBehaviour
{
    [SerializeField] private TMP_Text caption;
    [SerializeField, Min(.1f)] private float lifetime = 2.2f;
    private Camera view;
    private float age;
    private Vector3 origin;
    public static void Play(DiscoveryBurst prefab, Vector3 position, string message, Camera camera = null)
    {
        if (prefab == null) return;
        var burst = Instantiate(prefab, position, Quaternion.identity);
        burst.view = camera;
        if (burst.view == null) burst.view = Camera.main;
        if (burst.caption != null) burst.caption.text = message;
    }
    private void Awake() { origin = transform.position; Destroy(gameObject, lifetime + .5f); }
    private void Update()
    {
        age += Time.deltaTime;
        if (caption != null)
        {
            caption.transform.position = origin + Vector3.up * (.25f + age * .35f);
            if (view != null) caption.transform.rotation = view.transform.rotation;
            var color = caption.color; color.a = 1 - Mathf.SmoothStep(0, 1, (age / lifetime - .65f) / .35f); caption.color = color;
        }
        if (age >= lifetime) Destroy(gameObject);
    }
}
