using UnityEngine;
using UnityEngine.UI;

/// <summary>Small gameplay reticle and the shared camera-center ray for tools.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAim : MonoBehaviour
{
    private PlayerController player;
    private Canvas canvas;
    private RectTransform dot;
    private Texture2D texture;

    public static Ray CenterRay(Camera camera) => camera.ViewportPointToRay(new Vector3(.5f, .5f));

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        var root = new GameObject("Aim dot HUD", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var imageObject = new GameObject("Aim dot", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(root.transform, false);
        dot = imageObject.GetComponent<RectTransform>();
        dot.sizeDelta = Vector2.one * 6;
        texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.name = "Aim dot circle";
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float radius = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
            float shade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(10, 12, radius));
            pixels[y * 32 + x] = new Color(shade, shade, shade, 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(14, 16, radius)));
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        var image = imageObject.GetComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        canvas.enabled = false;
    }

    private void LateUpdate()
    {
        var camera = player != null ? player.characterCamera : null;
        canvas.enabled = player != null && player.isActiveAndEnabled && camera != null && camera.isActiveAndEnabled
            && Time.timeScale > 0 && Cursor.lockState == CursorLockMode.Locked;
        if (!canvas.enabled) return;
        dot.anchorMin = dot.anchorMax = camera.rect.center;
        dot.anchoredPosition = Vector2.zero;
    }

    private void OnDisable() { if (canvas != null) canvas.enabled = false; }
    private void OnDestroy()
    {
        if (texture != null) Destroy(texture);
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
