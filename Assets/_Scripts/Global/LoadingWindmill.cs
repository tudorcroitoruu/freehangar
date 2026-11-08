using UnityEngine;
using UnityEngine.UI;

/// <summary>Static mill divider with a separate rotor driven by real time.</summary>
[RequireComponent(typeof(Image))]
public sealed class LoadingWindmill : MonoBehaviour
{
    [SerializeField] private Texture2D artwork;
    [SerializeField] private float degreesPerSecond = 90f;
    private RectTransform rotor;
    private Sprite towerSprite, rotorSprite;

    private void Awake()
    {
        if (artwork == null) return;
        // The artwork contains the stationary divider above the isolated rotor.
        float width = artwork.width, height = artwork.height;
        towerSprite = Sprite.Create(artwork, new Rect(0, height * .5f, width, height * .5f), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        rotorSprite = Sprite.Create(artwork, new Rect(width * (663f / 1774f), height * (7f / 887f), width * (448f / 1774f), height * (448f / 887f)), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        var divider = GetComponent<Image>();
        divider.sprite = towerSprite;
        divider.preserveAspect = true;
        divider.raycastTarget = false;
        var wheel = new GameObject("Windmill rotor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        wheel.layer = gameObject.layer;
        rotor = wheel.GetComponent<RectTransform>();
        rotor.SetParent(transform, false);
        rotor.anchorMin = rotor.anchorMax = new Vector2(.5f, 1f - 170f / 443.5f);
        rotor.pivot = new Vector2(.5f, .5f);
        rotor.anchoredPosition = Vector2.zero;
        var image = wheel.GetComponent<Image>();
        image.sprite = rotorSprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        ResizeRotor();
    }

    private void ResizeRotor()
    {
        if (rotor != null)
            rotor.sizeDelta = Vector2.one * (((RectTransform)transform).rect.width * (448f / 1774f));
    }

    private void OnRectTransformDimensionsChange() => ResizeRotor();

    private void Update()
    {
        if (rotor != null) rotor.Rotate(0f, 0f, -degreesPerSecond * Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        if (towerSprite != null) Destroy(towerSprite);
        if (rotorSprite != null) Destroy(rotorSprite);
    }
}
