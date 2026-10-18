using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
#endif

namespace LogicStories.BackgroundBlurUI
{
    /// <summary>
    /// Captures the fully composed screen once, blurs the area behind this RectTransform
    /// on the GPU, and inserts the resulting image immediately behind this object.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("UI/Background Blur (One Shot)")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIBackgroundBlur : MonoBehaviour
    {
        private const string BlurShaderResourcePath =
            "BackgroundBlurUI/BackgroundBlurUIBlur";
        private const string SoftMaskShaderResourcePath =
            "BackgroundBlurUI/BackgroundBlurUISoftMask";
        private const int DefaultDownsample = 4;
        private const int DefaultFrostStrength = 3;
        private const int DefaultBlurIterations = 4;
        private const float DefaultBlurRadius = 1f;

        [Header("Quality / Performance")]
        [Tooltip("4 is the default for a fast, extra-soft frosted look.")]
        [SerializeField, Range(1, 4)]
        private int downsample = DefaultDownsample;

        [Tooltip("1 keeps the original look. 5 produces a very strong frosted-glass blur without adding shader samples or passes.")]
        [SerializeField, Range(1, 5)]
        private int frostStrength = DefaultFrostStrength;

        [Tooltip("Four iterations produce the default extra-soft frosted look.")]
        [SerializeField, Range(1, 4)]
        private int blurIterations = DefaultBlurIterations;

        [Tooltip("Blur radius in pixels of the downsampled texture.")]
        [SerializeField, Range(0.5f, 4f)]
        private float blurRadius = DefaultBlurRadius;

        [Header("Appearance")]
        [Tooltip("Optional color tint applied to the finished blur.")]
        [SerializeField]
        private Color tint = Color.white;

        [Tooltip("Extra source pixels around the UI element. Zero selects a safe value automatically.")]
        [SerializeField, Min(0)]
        private int edgePadding = 0;

        [Header("Capture Exclusion")]
        [Tooltip("Hide UI siblings rendered above this object while the blur snapshot is captured.")]
        [SerializeField]
        private bool hideFollowingSiblingsDuringCapture = true;

        [SerializeField, HideInInspector]
        private Transform captureExclusionRoot;

        [Tooltip("Optional UI roots to hide during the capture, except renderers preserved at runtime.")]
        [SerializeField]
        private Transform[] captureExclusionRoots = new Transform[0];

        private readonly List<CanvasRendererState> hiddenRenderers = new();
        private readonly HashSet<CanvasRenderer> hiddenRendererLookup = new();
        private readonly HashSet<CanvasRenderer> visibleRendererLookup = new();
        private CanvasRenderer[] visibleRenderersDuringCapture = new CanvasRenderer[0];

        private RectTransform targetRectTransform;
        private Coroutine captureCoroutine;
        private GameObject blurObject;
        private RenderTexture blurredTexture;
        private Material blurMaterial;
        private Material softMaskMaterial;
        private CanvasGroup generatedCanvasGroup;
        private CanvasGroup[] sourceCanvasGroups;
        private int generation;

#if UNITY_EDITOR
        private int editorScheduledGeneration;
        private int editorFramesUntilCapture;
        private int editorCaptureAttempts;
#endif

        private readonly struct CanvasRendererState
        {
            public readonly CanvasRenderer Renderer;
            public readonly float Alpha;

            public CanvasRendererState(CanvasRenderer renderer, float alpha)
            {
                Renderer = renderer;
                Alpha = alpha;
            }
        }

        private void Awake()
        {
            targetRectTransform = (RectTransform)transform;
        }

        private void Reset()
        {
            downsample = DefaultDownsample;
            frostStrength = DefaultFrostStrength;
            blurIterations = DefaultBlurIterations;
            blurRadius = DefaultBlurRadius;
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= RefreshAfterValidation;
#endif
            RefreshBlur();
        }

        private void LateUpdate()
        {
            SynchronizeLocalCanvasGroups();
        }

        private void OnDisable()
        {
            generation++;

#if UNITY_EDITOR
            CancelEditorCapture();
            EditorApplication.delayCall -= RefreshAfterValidation;
#endif

            if (captureCoroutine != null)
            {
                StopCoroutine(captureCoroutine);
                captureCoroutine = null;
            }

            RestoreTargetVisuals();
            DisposeGeneratedResources();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RequestEditorRepaint();
            }
#endif
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            CancelEditorCapture();
            EditorApplication.delayCall -= RefreshAfterValidation;
#endif

            RestoreTargetVisuals();
            DisposeGeneratedResources();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RequestEditorRepaint();
            }
#endif
        }

        /// <summary>
        /// Discards the current snapshot and captures a fresh one at the end of this frame.
        /// The target remains excluded from the captured image.
        /// </summary>
        [ContextMenu("Refresh Blur")]
        public void RefreshBlur()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            generation++;
            int requestGeneration = generation;

            if (captureCoroutine != null)
            {
                StopCoroutine(captureCoroutine);
                captureCoroutine = null;
            }

#if UNITY_EDITOR
            CancelEditorCapture();
#endif

            RestoreTargetVisuals();
            DisposeGeneratedResources();
            HideTargetVisuals();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ScheduleEditorCapture(requestGeneration);
                return;
            }
#endif

            captureCoroutine = StartCoroutine(CaptureAtEndOfFrame(requestGeneration));
        }

        /// <summary>
        /// Removes the generated image and frees its GPU texture.
        /// </summary>
        [ContextMenu("Clear Blur")]
        public void ClearBlur()
        {
            generation++;

            if (captureCoroutine != null)
            {
                StopCoroutine(captureCoroutine);
                captureCoroutine = null;
            }

#if UNITY_EDITOR
            CancelEditorCapture();
#endif

            RestoreTargetVisuals();
            DisposeGeneratedResources();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RequestEditorRepaint();
            }
#endif
        }

        private IEnumerator CaptureAtEndOfFrame(int requestGeneration)
        {
            // Canvas layout, all cameras, and Screen Space Overlay UI are complete here.
            yield return new WaitForEndOfFrame();

            if (requestGeneration != generation || !isActiveAndEnabled)
            {
                yield break;
            }

            captureCoroutine = null;
            CaptureNow(requestGeneration, null);
        }

        private void CaptureNow(
            int requestGeneration,
            RenderTexture editorGameViewSource)
        {
            if (requestGeneration != generation || !isActiveAndEnabled)
            {
                RestoreTargetVisuals();
                return;
            }

            if (targetRectTransform == null)
            {
                targetRectTransform = (RectTransform)transform;
            }

            Canvas.ForceUpdateCanvases();

            if (!TryGetScreenRect(targetRectTransform, out Rect targetScreenRect))
            {
                RestoreTargetVisuals();
                return;
            }

            Shader blurShader = Resources.Load<Shader>(BlurShaderResourcePath);
            if (blurShader == null)
            {
                blurShader = Shader.Find("Hidden/BackgroundBlurUI/GaussianBlur");
            }

            if (blurShader == null || !blurShader.isSupported)
            {
                Debug.LogError(
                    "Background Blur UI shader is missing or unsupported on this device.",
                    this);
                RestoreTargetVisuals();
                return;
            }

            int screenWidth = editorGameViewSource != null
                ? editorGameViewSource.width
                : Mathf.Max(1, Screen.width);
            int screenHeight = editorGameViewSource != null
                ? editorGameViewSource.height
                : Mathf.Max(1, Screen.height);
            Rect captureRect = GetPaddedCaptureRect(
                targetScreenRect,
                screenWidth,
                screenHeight);

            if (captureRect.width < 1f || captureRect.height < 1f)
            {
                RestoreTargetVisuals();
                return;
            }

            RenderTexture fullScreenCapture = null;
            RenderTexture croppedSource = null;
            RenderTexture horizontalPass = null;

            try
            {
                Texture fullScreenSource = editorGameViewSource;
                if (fullScreenSource == null)
                {
                    fullScreenCapture = GetTemporaryTexture(
                        screenWidth,
                        screenHeight);

                    // Unlike an opaque/camera texture, this captures the final
                    // backbuffer: game cameras plus every already-rendered
                    // Screen Space Overlay canvas.
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(
                        fullScreenCapture);
                    fullScreenSource = fullScreenCapture;
                }

                int effectiveDownsample = GetEffectiveDownsample();
                int textureWidth = Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        captureRect.width / effectiveDownsample));
                int textureHeight = Mathf.Max(
                    1,
                    Mathf.CeilToInt(
                        captureRect.height / effectiveDownsample));

                croppedSource = GetTemporaryTexture(textureWidth, textureHeight);
                horizontalPass = GetTemporaryTexture(textureWidth, textureHeight);
                blurredTexture = CreatePersistentTexture(textureWidth, textureHeight);

                Vector2 cropScale = new(
                    captureRect.width / screenWidth,
                    captureRect.height / screenHeight);
                Vector2 cropOffset = new(
                    captureRect.xMin / screenWidth,
                    captureRect.yMin / screenHeight);

                // Both the editor Game View target and the runtime
                // ScreenCapture RenderTexture use the native top-origin
                // orientation on Metal/Direct3D. Normalize either source to
                // bottom-left UI orientation while cropping.
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    cropScale.y = -cropScale.y;
                    cropOffset.y = 1f -
                        captureRect.yMin / screenHeight;
                }

                // Crop and downsample in the same GPU blit.
                Graphics.Blit(
                    fullScreenSource,
                    croppedSource,
                    cropScale,
                    cropOffset);

                blurMaterial = new Material(blurShader)
                {
                    name = "Background Blur UI (Generated)",
                    hideFlags = HideFlags.DontSave
                };

                Texture currentSource = croppedSource;
                for (int i = 0; i < blurIterations; i++)
                {
                    blurMaterial.SetVector(
                        "_Direction",
                        new Vector4(blurRadius, 0f, 0f, 0f));
                    Graphics.Blit(currentSource, horizontalPass, blurMaterial);

                    blurMaterial.SetVector(
                        "_Direction",
                        new Vector4(0f, blurRadius, 0f, 0f));
                    Graphics.Blit(horizontalPass, blurredTexture, blurMaterial);
                    currentSource = blurredTexture;
                }

                CreateBlurImage(
                    targetScreenRect,
                    captureRect,
                    screenWidth,
                    screenHeight);
            }
            finally
            {
                ReleaseTemporaryTexture(fullScreenCapture);
                ReleaseTemporaryTexture(croppedSource);
                ReleaseTemporaryTexture(horizontalPass);
                RestoreTargetVisuals();
            }
        }

        public void SetHideFollowingSiblingsDuringCapture(bool hide)
        {
            hideFollowingSiblingsDuringCapture = hide;
        }

        public void SetCaptureExclusionRoot(
            Transform root,
            params CanvasRenderer[] visibleRenderers)
        {
            captureExclusionRoot = root;
            captureExclusionRoots = root != null
                ? new[] { root }
                : new Transform[0];
            visibleRenderersDuringCapture =
                visibleRenderers ?? new CanvasRenderer[0];
        }

        public void SetCaptureExclusionRoots(
            IEnumerable<Transform> roots,
            params CanvasRenderer[] visibleRenderers)
        {
            if (roots == null)
            {
                captureExclusionRoots = new Transform[0];
            }
            else
            {
                List<Transform> validRoots = new();
                foreach (Transform root in roots)
                {
                    if (root != null)
                        validRoots.Add(root);
                }

                captureExclusionRoots = validRoots.ToArray();
            }

            captureExclusionRoot = null;
            visibleRenderersDuringCapture =
                visibleRenderers ?? new CanvasRenderer[0];
        }

#if UNITY_EDITOR
        private void ScheduleEditorCapture(int requestGeneration)
        {
            if (EditorUtility.IsPersistent(gameObject) ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                RestoreTargetVisuals();
                return;
            }

            editorScheduledGeneration = requestGeneration;
            editorFramesUntilCapture = 2;
            editorCaptureAttempts = 0;

            EditorApplication.update -= EditorCaptureUpdate;
            EditorApplication.update += EditorCaptureUpdate;
            RequestEditorRepaint();
        }

        private void EditorCaptureUpdate()
        {
            if (this == null ||
                !isActiveAndEnabled ||
                editorScheduledGeneration != generation ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CancelEditorCapture();
                RestoreTargetVisuals();
                return;
            }

            Canvas.ForceUpdateCanvases();

            if (editorFramesUntilCapture > 0)
            {
                editorFramesUntilCapture--;
                RequestEditorRepaint();
                return;
            }

            RenderTexture gameViewTexture = GetGameViewTargetTexture();
            if (gameViewTexture == null || !gameViewTexture.IsCreated())
            {
                editorCaptureAttempts++;
                if (editorCaptureAttempts < 8)
                {
                    RequestEditorRepaint();
                    return;
                }

                CancelEditorCapture();
                RestoreTargetVisuals();
                Debug.LogWarning(
                    "Background Blur UI could not read the Game View. " +
                    "Open the Game View once and toggle this component again.",
                    this);
                return;
            }

            int requestGeneration = editorScheduledGeneration;
            CancelEditorCapture();
            CaptureNow(requestGeneration, gameViewTexture);
            RequestEditorRepaint();
        }

        private void CancelEditorCapture()
        {
            EditorApplication.update -= EditorCaptureUpdate;
            editorScheduledGeneration = 0;
            editorFramesUntilCapture = 0;
            editorCaptureAttempts = 0;
        }

        private static void RequestEditorRepaint()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static RenderTexture GetGameViewTargetTexture()
        {
            Assembly editorAssembly = typeof(EditorWindow).Assembly;
            System.Type playModeViewType =
                editorAssembly.GetType("UnityEditor.PlayModeView");
            if (playModeViewType == null)
            {
                return null;
            }

            const BindingFlags staticFlags =
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            MethodInfo getMainView = null;
            foreach (MethodInfo method in playModeViewType.GetMethods(staticFlags))
            {
                if (method.Name == "GetMainPlayModeView" &&
                    method.GetParameters().Length == 0)
                {
                    getMainView = method;
                    break;
                }
            }

            object playModeView = getMainView?.Invoke(null, null);
            if (playModeView == null)
            {
                return null;
            }

            const BindingFlags instanceFlags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;
            PropertyInfo targetTextureProperty = null;
            FieldInfo targetTextureField = null;
            for (System.Type type = playModeView.GetType();
                 type != null &&
                 targetTextureProperty == null &&
                 targetTextureField == null;
                 type = type.BaseType)
            {
                targetTextureProperty = type.GetProperty(
                    "targetTexture",
                    instanceFlags);
                targetTextureField = type.GetField(
                    "m_TargetTexture",
                    instanceFlags);
            }

            if (targetTextureProperty != null)
            {
                return targetTextureProperty.GetValue(playModeView)
                    as RenderTexture;
            }

            return targetTextureField?.GetValue(playModeView)
                as RenderTexture;
        }

        private void RefreshAfterValidation()
        {
            if (this != null &&
                !Application.isPlaying &&
                isActiveAndEnabled &&
                !EditorUtility.IsPersistent(gameObject))
            {
                RefreshBlur();
            }
        }
#endif

        private void HideTargetVisuals()
        {
            hiddenRenderers.Clear();
            hiddenRendererLookup.Clear();
            visibleRendererLookup.Clear();

            for (int i = 0; i < visibleRenderersDuringCapture.Length; i++)
            {
                if (visibleRenderersDuringCapture[i] != null)
                    visibleRendererLookup.Add(visibleRenderersDuringCapture[i]);
            }

            HideCanvasRenderersIn(transform, false);

            HideCaptureExclusionRootVisuals();

            if (hideFollowingSiblingsDuringCapture)
            {
                HideFollowingSiblingVisuals();
            }
        }

        private bool HideCaptureExclusionRootVisuals()
        {
            bool hidAnyRoot = false;
            if (captureExclusionRoots != null)
            {
                for (int i = 0; i < captureExclusionRoots.Length; i++)
                {
                    Transform root = captureExclusionRoots[i];
                    if (root == null)
                        continue;

                    HideCanvasRenderersIn(root, true);
                    hidAnyRoot = true;
                }
            }

            if (hidAnyRoot == false && captureExclusionRoot != null)
            {
                HideCanvasRenderersIn(captureExclusionRoot, true);
                hidAnyRoot = true;
            }

            return hidAnyRoot;
        }

        private void HideFollowingSiblingVisuals()
        {
            Transform parent = transform.parent;
            if (parent == null)
                return;

            int startIndex = transform.GetSiblingIndex() + 1;
            for (int i = startIndex; i < parent.childCount; i++)
            {
                Transform sibling = parent.GetChild(i);
                if (sibling != null)
                    HideCanvasRenderersIn(sibling, true);
            }
        }

        private void HideCanvasRenderersIn(
            Transform root,
            bool keepVisibleRenderers)
        {
            if (root == null)
                return;

            CanvasRenderer[] renderers =
                root.GetComponentsInChildren<CanvasRenderer>(true);

            foreach (CanvasRenderer canvasRenderer in renderers)
            {
                if (canvasRenderer == null ||
                    (keepVisibleRenderers &&
                     visibleRendererLookup.Contains(canvasRenderer)) ||
                    hiddenRendererLookup.Add(canvasRenderer) == false)
                {
                    continue;
                }

                hiddenRenderers.Add(
                    new CanvasRendererState(
                        canvasRenderer,
                        canvasRenderer.GetAlpha()));
                canvasRenderer.SetAlpha(0f);
            }
        }

        private void RestoreTargetVisuals()
        {
            foreach (CanvasRendererState state in hiddenRenderers)
            {
                if (state.Renderer != null)
                {
                    state.Renderer.SetAlpha(state.Alpha);
                }
            }

            hiddenRenderers.Clear();
            hiddenRendererLookup.Clear();
            visibleRendererLookup.Clear();
        }

        private bool TryGetScreenRect(
            RectTransform rectTransform,
            out Rect screenRect)
        {
            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera eventCamera = null;

            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = canvas.worldCamera != null
                    ? canvas.worldCamera
                    : Camera.main;
            }

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            Vector2 first = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                corners[0]);
            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    corners[i]);
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            screenRect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return screenRect.width > 0.5f && screenRect.height > 0.5f;
        }

        private Rect GetPaddedCaptureRect(
            Rect targetScreenRect,
            int screenWidth,
            int screenHeight)
        {
            int padding = edgePadding > 0
                ? edgePadding
                : Mathf.CeilToInt(
                    blurRadius *
                    blurIterations *
                    GetEffectiveDownsample() *
                    4f);

            float minX = Mathf.Clamp(
                targetScreenRect.xMin - padding,
                0f,
                screenWidth);
            float minY = Mathf.Clamp(
                targetScreenRect.yMin - padding,
                0f,
                screenHeight);
            float maxX = Mathf.Clamp(
                targetScreenRect.xMax + padding,
                0f,
                screenWidth);
            float maxY = Mathf.Clamp(
                targetScreenRect.yMax + padding,
                0f,
                screenHeight);

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private int GetEffectiveDownsample()
        {
            // Stronger frost uses fewer internal pixels. The shader still
            // performs exactly the same five samples and the same number of
            // passes, while each blur texel covers a larger screen area.
            return Mathf.Max(1, downsample * frostStrength);
        }

        private void CreateBlurImage(
            Rect targetScreenRect,
            Rect captureRect,
            int screenWidth,
            int screenHeight)
        {
            Transform parent = targetRectTransform.parent;
            if (parent == null)
            {
                Debug.LogError(
                    "Background Blur UI needs a parent Canvas transform.",
                    this);
                return;
            }

            blurObject = new GameObject(
                $"{gameObject.name} (Generated Blur)",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(LayoutElement));
            blurObject.hideFlags = Application.isPlaying
                ? HideFlags.DontSave
                : HideFlags.HideAndDontSave;

            RectTransform blurRectTransform =
                (RectTransform)blurObject.transform;
            blurRectTransform.SetParent(parent, false);
            CopyRectTransform(targetRectTransform, blurRectTransform);
            blurRectTransform.SetSiblingIndex(targetRectTransform.GetSiblingIndex());

            LayoutElement layoutElement = blurObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            ConfigureLocalCanvasGroupFollowing();

            Rect blurUvRect = new(
                (targetScreenRect.xMin - captureRect.xMin) / captureRect.width,
                (targetScreenRect.yMin - captureRect.yMin) / captureRect.height,
                targetScreenRect.width / captureRect.width,
                targetScreenRect.height / captureRect.height);

            if (TryCreateSoftMaskedBlurGraphic(
                    blurObject,
                    captureRect,
                    screenWidth,
                    screenHeight))
            {
                return;
            }

            RawImage rawImage = blurObject.AddComponent<RawImage>();
            rawImage.texture = blurredTexture;
            rawImage.color = tint;
            rawImage.raycastTarget = false;
            rawImage.maskable = true;
            rawImage.uvRect = blurUvRect;
        }

        private void ConfigureLocalCanvasGroupFollowing()
        {
            sourceCanvasGroups = GetComponents<CanvasGroup>();
            if (sourceCanvasGroups.Length == 0)
            {
                generatedCanvasGroup = null;
                return;
            }

            generatedCanvasGroup = blurObject.AddComponent<CanvasGroup>();
            generatedCanvasGroup.interactable = false;
            generatedCanvasGroup.blocksRaycasts = false;
            SynchronizeLocalCanvasGroups();
        }

        private void SynchronizeLocalCanvasGroups()
        {
            if (generatedCanvasGroup == null ||
                sourceCanvasGroups == null)
            {
                return;
            }

            float combinedAlpha = 1f;
            bool ignoreParentGroups = false;

            foreach (CanvasGroup sourceGroup in sourceCanvasGroups)
            {
                if (sourceGroup == null || !sourceGroup.isActiveAndEnabled)
                {
                    continue;
                }

                combinedAlpha *= sourceGroup.alpha;
                ignoreParentGroups |= sourceGroup.ignoreParentGroups;
            }

            generatedCanvasGroup.alpha = combinedAlpha;
            generatedCanvasGroup.ignoreParentGroups = ignoreParentGroups;
        }

        private bool TryCreateSoftMaskedBlurGraphic(
            GameObject imageObject,
            Rect captureRect,
            int screenWidth,
            int screenHeight)
        {
            Image sourceImage = GetComponent<Image>();
            Sprite sourceSprite = sourceImage != null
                ? sourceImage.overrideSprite
                : null;
            RawImage sourceRawImage = GetComponent<RawImage>();

            bool hasImageMask =
                sourceImage != null &&
                sourceImage.enabled &&
                sourceSprite != null;
            bool hasRawImageMask =
                sourceRawImage != null &&
                sourceRawImage.enabled &&
                sourceRawImage.texture != null;

            if (!hasImageMask && !hasRawImageMask)
            {
                return false;
            }

            Shader softMaskShader =
                Resources.Load<Shader>(SoftMaskShaderResourcePath);
            if (softMaskShader == null)
            {
                softMaskShader = Shader.Find(
                    "Hidden/BackgroundBlurUI/SoftAlphaMask");
            }

            if (softMaskShader == null || !softMaskShader.isSupported)
            {
                Debug.LogWarning(
                    "Background Blur UI soft-mask shader is missing or unsupported. " +
                    "Falling back to a rectangular blur.",
                    this);
                return false;
            }

            MaskableGraphic blurGraphic;
            if (hasImageMask)
            {
                Image blurImage = imageObject.AddComponent<Image>();
                blurImage.sprite = sourceSprite;
                blurImage.type = sourceImage.type;
                blurImage.preserveAspect = sourceImage.preserveAspect;
                blurImage.fillCenter = sourceImage.fillCenter;
                blurImage.fillMethod = sourceImage.fillMethod;
                blurImage.fillAmount = sourceImage.fillAmount;
                blurImage.fillClockwise = sourceImage.fillClockwise;
                blurImage.fillOrigin = sourceImage.fillOrigin;
                blurImage.pixelsPerUnitMultiplier =
                    sourceImage.pixelsPerUnitMultiplier;
                blurImage.useSpriteMesh = sourceImage.useSpriteMesh;
                blurImage.maskable = sourceImage.maskable;
                blurGraphic = blurImage;
            }
            else
            {
                RawImage blurRawImage = imageObject.AddComponent<RawImage>();
                blurRawImage.texture = sourceRawImage.texture;
                blurRawImage.uvRect = sourceRawImage.uvRect;
                blurRawImage.maskable = sourceRawImage.maskable;
                blurGraphic = blurRawImage;
            }

            softMaskMaterial = new Material(softMaskShader)
            {
                name = "Background Blur UI Soft Mask (Generated)",
                hideFlags = HideFlags.DontSave
            };

            softMaskMaterial.SetTexture("_BlurTex", blurredTexture);
            softMaskMaterial.SetVector(
                "_CaptureScreenRect",
                new Vector4(
                    captureRect.xMin / screenWidth,
                    captureRect.yMin / screenHeight,
                    captureRect.width / screenWidth,
                    captureRect.height / screenHeight));

            blurGraphic.color = tint;
            blurGraphic.material = softMaskMaterial;
            blurGraphic.raycastTarget = false;
            return true;
        }

        private static void CopyRectTransform(
            RectTransform source,
            RectTransform destination)
        {
            destination.anchorMin = source.anchorMin;
            destination.anchorMax = source.anchorMax;
            destination.pivot = source.pivot;
            destination.sizeDelta = source.sizeDelta;
            destination.anchoredPosition3D = source.anchoredPosition3D;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }

        private static RenderTexture GetTemporaryTexture(int width, int height)
        {
            RenderTexture texture = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }

        private static RenderTexture CreatePersistentTexture(
            int width,
            int height)
        {
            RenderTexture texture = new(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                name = "Background Blur UI Snapshot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1,
                hideFlags = HideFlags.DontSave
            };
            texture.Create();
            return texture;
        }

        private void DisposeGeneratedResources()
        {
            if (blurObject != null)
            {
                if (Application.isPlaying)
                {
                    blurObject.SetActive(false);
                }
#if UNITY_EDITOR
                else
                {
                    // SetActive/DestroyImmediate are forbidden while Unity is
                    // activating or deactivating this object's parent. Cull
                    // the generated graphics now, then destroy safely on the
                    // next editor tick.
                    CanvasRenderer[] generatedRenderers =
                        blurObject.GetComponentsInChildren<CanvasRenderer>(true);
                    foreach (CanvasRenderer generatedRenderer
                             in generatedRenderers)
                    {
                        if (generatedRenderer != null)
                        {
                            generatedRenderer.cull = true;
                        }
                    }
                }
#endif

                DestroyRuntimeObject(blurObject);
                blurObject = null;
                generatedCanvasGroup = null;
                sourceCanvasGroups = null;
            }

            if (blurredTexture != null)
            {
                blurredTexture.Release();
                DestroyRuntimeObject(blurredTexture);
                blurredTexture = null;
            }

            if (blurMaterial != null)
            {
                DestroyRuntimeObject(blurMaterial);
                blurMaterial = null;
            }

            if (softMaskMaterial != null)
            {
                DestroyRuntimeObject(softMaskMaterial);
                softMaskMaterial = null;
            }
        }

        private static void ReleaseTemporaryTexture(RenderTexture texture)
        {
            if (texture != null)
            {
                RenderTexture.ReleaseTemporary(texture);
            }
        }

        private static void DestroyRuntimeObject(Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
#if UNITY_EDITOR
            else if (value is GameObject)
            {
                Object pendingDestroy = value;
                EditorApplication.delayCall += () =>
                {
                    if (pendingDestroy != null)
                    {
                        DestroyImmediate(pendingDestroy);
                    }
                };
            }
#endif
            else
            {
                DestroyImmediate(value);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            downsample = Mathf.Clamp(downsample, 1, 4);
            frostStrength = Mathf.Clamp(frostStrength, 1, 5);
            blurIterations = Mathf.Clamp(blurIterations, 1, 4);
            blurRadius = Mathf.Clamp(blurRadius, 0.5f, 4f);
            edgePadding = Mathf.Max(0, edgePadding);

            if (!Application.isPlaying)
            {
                EditorApplication.delayCall -= RefreshAfterValidation;
                EditorApplication.delayCall += RefreshAfterValidation;
            }
        }
#endif
    }
}
