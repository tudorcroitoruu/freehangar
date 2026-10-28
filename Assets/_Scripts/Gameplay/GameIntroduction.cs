using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored, HUD-free arrival sequence; all temporary state is restored on interruption.</summary>
[DisallowMultipleComponent]
public sealed class GameIntroduction : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private BicycleController bicycle;
    [SerializeField] private Transform landscapeStart, landscapeEnd, arrival, grainLook, shovelLook;
    [SerializeField] private Vector3[] bicycleRoute = Array.Empty<Vector3>();
    [SerializeField] private GameObject purchasePaperPrefab;
    [SerializeField] private GameObject worldShovel;
    [SerializeField] private AudioSource birds;
    [Header("Shot timing")]
    [SerializeField, Min(.1f)] private float landscapeSeconds = 6f;
    [SerializeField, Min(.1f)] private float bicycleSeconds = 11f;
    [SerializeField, Min(.1f)] private float paperSeconds = 5.5f;
    [SerializeField, Min(.05f)] private float fadeSeconds = .65f;
    public bool IsPlaying { get; private set; }
    public bool Completed { get; private set; }
    public string CurrentShot { get; private set; } = "Gameplay";
    private Camera view;
    private Transform cameraParent;
    private Vector3 cameraLocalPosition, playerPosition, bikePosition;
    private Quaternion cameraLocalRotation, playerRotation, bikeRotation;
    private float originalFov;
    private GameObject paper, fadeObject;
    private Image fade;
    private bool instrumentActive, shovelEnabled, worldShovelActive, birdsWerePlaying;
    private readonly List<Behaviour> suspended = new List<Behaviour>();
    private readonly List<bool> enabledStates = new List<bool>();
    private readonly Dictionary<Canvas, bool> hud = new Dictionary<Canvas, bool>();
    private readonly List<Collider> bikeColliders = new List<Collider>();
    private readonly List<bool> colliderStates = new List<bool>();
    private readonly Dictionary<Transform, Quaternion> wheelRotations = new Dictionary<Transform, Quaternion>();
    private bool controllerEnabled;
    private Vector3 worldShovelPosition;
    private Quaternion worldShovelRotation;
    private float[] routeLengths;

    public bool Play()
    {
        if (IsPlaying || Completed || !isActiveAndEnabled || player == null || player.characterCamera == null || bicycle == null ||
            landscapeStart == null || landscapeEnd == null || arrival == null || grainLook == null || shovelLook == null || bicycleRoute.Length < 2 || purchasePaperPrefab == null)
            return false;
        IsPlaying = true; view = player.characterCamera;
        cameraParent = view.transform.parent; cameraLocalPosition = view.transform.localPosition; cameraLocalRotation = view.transform.localRotation;
        originalFov = view.fieldOfView; playerPosition = player.transform.position; playerRotation = player.transform.rotation;
        bikePosition = bicycle.transform.position; bikeRotation = bicycle.transform.rotation;
        controllerEnabled = player.characterController.enabled;
        instrumentActive = player.HeldInstrument != null && player.HeldInstrument.gameObject.activeSelf;
        shovelEnabled = player.Shovel != null && player.Shovel.enabled;
        worldShovelActive = worldShovel != null && worldShovel.activeSelf;
        if (worldShovel != null) { worldShovelPosition = worldShovel.transform.position; worldShovelRotation = worldShovel.transform.rotation; }
        Suspend(player); Suspend(player.GetComponent<PlayerInteractor>()); Suspend(player.GetComponent<PlayerBicycleInteraction>());
        Suspend(player.GetComponent<BagInteraction>()); Suspend(player.GetComponent<PlayerItemCarry>()); Suspend(player.Stamina); Suspend(bicycle);
        player.characterController.enabled = false;
        if (player.HeldInstrument != null) player.HeldInstrument.gameObject.SetActive(false);
        if (player.Shovel != null) player.Shovel.enabled = false;
        if (worldShovel != null) worldShovel.SetActive(true);
        foreach (var collider in bicycle.GetComponentsInChildren<Collider>()) { bikeColliders.Add(collider); colliderStates.Add(collider.enabled); collider.enabled = false; }
        foreach (var wheel in new[] { bicycle.frontWheel, bicycle.rearWheel, bicycle.crank }) if (wheel != null && !wheelRotations.ContainsKey(wheel)) wheelRotations.Add(wheel, wheel.localRotation);
        birdsWerePlaying = birds != null && birds.isPlaying;
        if (birds != null && !birdsWerePlaying) birds.Play();
        HideHud(); CreateFade();
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        view.transform.SetParent(null, true); view.fieldOfView = 60;
        routeLengths = new float[bicycleRoute.Length];
        for (int i = 1; i < routeLengths.Length; i++) routeLengths[i] = routeLengths[i - 1] + Vector3.Distance(bicycleRoute[i - 1], bicycleRoute[i]);
        StartCoroutine(Sequence()); return true;
    }
    private void Suspend(Behaviour component)
    {
        if (component == null || suspended.Contains(component)) return;
        suspended.Add(component); enabledStates.Add(component.enabled); component.enabled = false;
    }
    private void HideHud()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (fadeObject != null && canvas.gameObject == fadeObject) continue;
            if (!hud.ContainsKey(canvas)) hud.Add(canvas, canvas.enabled);
            canvas.enabled = false;
        }
    }
    private void LateUpdate() { if (IsPlaying) HideHud(); }
    private void CreateFade()
    {
        fadeObject = new GameObject("Introduction fade", typeof(Canvas), typeof(CanvasScaler));
        var canvas = fadeObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var panel = new GameObject("Fade", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(fadeObject.transform, false);
        fade = panel.GetComponent<Image>(); fade.color = Color.black; fade.raycastTarget = false;
        fade.rectTransform.anchorMin = Vector2.zero; fade.rectTransform.anchorMax = Vector2.one; fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;
    }
    private IEnumerator FadeTo(float alpha)
    {
        float start = fade.color.a;
        yield return Animate(fadeSeconds, t => { var c = Color.black; c.a = Mathf.Lerp(start, alpha, t); fade.color = c; });
    }
    private IEnumerator Animate(float duration, Action<float> apply)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            if (player == null || view == null) { Cancel(); yield break; }
            elapsed += Time.deltaTime; apply(Mathf.Clamp01(elapsed / duration)); yield return null;
        }
    }
    private IEnumerator Sequence()
    {
        CurrentShot = "Farm panorama";
        view.transform.SetPositionAndRotation(landscapeStart.position, landscapeStart.rotation);
        yield return FadeTo(0);
        yield return Animate(landscapeSeconds, t => {
            float eased = Mathf.SmoothStep(0, 1, t);
            view.transform.SetPositionAndRotation(Vector3.Lerp(landscapeStart.position, landscapeEnd.position, eased), Quaternion.Slerp(landscapeStart.rotation, landscapeEnd.rotation, eased));
        });
        yield return FadeTo(1);
        CurrentShot = "Bicycle approach"; SetBicycleShot(0);
        yield return FadeTo(0);
        yield return Animate(bicycleSeconds, SetBicycleShot);
        yield return FadeTo(1);
        CurrentShot = "Purchase paper";
        bicycle.transform.SetPositionAndRotation(arrival.position + arrival.right * -2.2f + arrival.forward * -1.2f, arrival.rotation);
        view.transform.SetPositionAndRotation(arrival.position + Vector3.up * cameraLocalPosition.y, arrival.rotation);
        paper = Instantiate(purchasePaperPrefab, view.transform, false); paper.transform.localPosition = new Vector3(.02f, -.8f, .7f);
        yield return FadeTo(0);
        yield return Animate(paperSeconds, t => {
            float lift = Mathf.SmoothStep(0, 1, t / .2f), lower = Mathf.SmoothStep(0, 1, (t - .72f) / .28f);
            paper.transform.localPosition = Vector3.Lerp(new Vector3(.02f, -.8f, .7f), new Vector3(.015f, -.025f, .65f), lift) - Vector3.up * (lower * .8f);
            paper.transform.localRotation = Quaternion.Euler(-8 + Mathf.Sin(t * Mathf.PI * 2) * 1.2f, -4, -3);
        });
        Destroy(paper); paper = null;
        CurrentShot = "Grain reveal";
        yield return LookAt(grainLook.position, 2.4f);
        yield return Animate(.7f, _ => { });
        CurrentShot = "First shovel";
        yield return LookAt(shovelLook.position, 2.2f);
        // The prop flies to the established held-tool pose, then the normal shovel takes over.
        if (worldShovel != null && player.HeldInstrument != null)
        {
            Vector3 from = worldShovel.transform.position; Quaternion fromRotation = worldShovel.transform.rotation;
            yield return Animate(1.25f, t => {
                float smooth = Mathf.SmoothStep(0, 1, t);
                worldShovel.transform.position = Vector3.Lerp(from, view.transform.TransformPoint(new Vector3(.28f, -.34f, .7f)), smooth);
                worldShovel.transform.rotation = Quaternion.Slerp(fromRotation, view.transform.rotation * Quaternion.Euler(15, 0, 0), smooth);
            });
            worldShovel.SetActive(false);
        }
        CurrentShot = "Ready";
        yield return LookAt(grainLook.position, 1.2f);
        Finish();
    }
    private IEnumerator LookAt(Vector3 target, float seconds)
    {
        Quaternion from = view.transform.rotation, to = Quaternion.LookRotation(target - view.transform.position, Vector3.up);
        yield return Animate(seconds, t => view.transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t)));
    }
    private Vector3 RoutePoint(float distance)
    {
        int i = 1; while (i < routeLengths.Length - 1 && routeLengths[i] < distance) i++;
        return Vector3.Lerp(bicycleRoute[i - 1], bicycleRoute[i], Mathf.InverseLerp(routeLengths[i - 1], routeLengths[i], distance));
    }
    private void SetBicycleShot(float t)
    {
        float total = routeLengths[routeLengths.Length - 1];
        float distance = total * t;
        Vector3 p = RoutePoint(distance), ahead = RoutePoint(Mathf.Min(total, distance + 2));
        Vector3 direction = ahead - p;
        if (direction.sqrMagnitude > .001f) bicycle.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.up);
        bicycle.transform.position = p;
        float spin = distance / Mathf.Max(.05f, bicycle.wheelRadius * bicycle.transform.lossyScale.x) * Mathf.Rad2Deg;
        foreach (var wheel in wheelRotations) if (wheel.Key != null) wheel.Key.localRotation = wheel.Value * Quaternion.Euler(spin * (wheel.Key == bicycle.crank ? .35f : 1), 0, 0);
        Vector3 rider = bicycle.riderView != null ? bicycle.riderView.position : p + Vector3.up * 1.65f;
        view.transform.position = rider + bicycle.transform.TransformVector(new Vector3(0, -.35f, -.05f)) + Vector3.up * (Mathf.Sin(t * 70) * .012f);
        view.transform.rotation = bicycle.transform.rotation * Quaternion.Euler(5 + Mathf.Sin(t * 70) * .18f, 0, Mathf.Sin(t * 25) * .25f);
    }
    public void Cancel()
    {
        if (!IsPlaying) return;
        StopAllCoroutines(); Restore(false);
    }
    private void Finish() { Completed = true; Restore(true); }
    private void Restore(bool finished)
    {
        if (!IsPlaying) return;
        IsPlaying = false; CurrentShot = "Gameplay";
        if (paper != null) Destroy(paper); if (fadeObject != null) Destroy(fadeObject);
        if (view != null)
        {
            Quaternion finalLook = view.transform.rotation;
            view.transform.SetParent(cameraParent, false); view.transform.localPosition = cameraLocalPosition;
            view.fieldOfView = originalFov;
            if (player != null)
            {
                player.transform.SetPositionAndRotation(finished ? arrival.position : playerPosition, finished ? Quaternion.Euler(0, finalLook.eulerAngles.y, 0) : playerRotation);
                view.transform.localRotation = finished ? Quaternion.Euler(Mathf.DeltaAngle(0, finalLook.eulerAngles.x), 0, 0) : cameraLocalRotation;
                player.ResetLook();
            }
        }
        foreach (var wheel in wheelRotations) if (wheel.Key != null) wheel.Key.localRotation = wheel.Value;
        if (!finished && bicycle != null) bicycle.transform.SetPositionAndRotation(bikePosition, bikeRotation);
        for (int i = 0; i < bikeColliders.Count; i++) if (bikeColliders[i] != null) bikeColliders[i].enabled = colliderStates[i];
        if (player != null)
        {
            player.characterController.enabled = controllerEnabled;
            if (player.HeldInstrument != null) player.HeldInstrument.gameObject.SetActive(finished || instrumentActive);
            if (player.Shovel != null) player.Shovel.enabled = finished || shovelEnabled;
        }
        if (worldShovel != null) { worldShovel.transform.SetPositionAndRotation(worldShovelPosition, worldShovelRotation); worldShovel.SetActive(finished ? false : worldShovelActive); }
        for (int i = 0; i < suspended.Count; i++) if (suspended[i] != null) suspended[i].enabled = enabledStates[i];
        foreach (var pair in hud) if (pair.Key != null) pair.Key.enabled = pair.Value;
        if (birds != null && !birdsWerePlaying) birds.Stop();
        suspended.Clear(); enabledStates.Clear(); hud.Clear(); bikeColliders.Clear(); colliderStates.Clear(); wheelRotations.Clear();
    }
    private void OnDisable() => Cancel();
}
