using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Stanislaw.FigmaUnityImporter.Editor
{
    internal static class FigmaSceneBuilder
    {
        private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();
        private static Action<float, string> activeProgress;
        private static int progressCompleted;
        private static int progressTotal;

        public static IReadOnlyList<GameObject> BuildMany(FigmaImportPayload payload,
            Action<float, string> progress = null)
        {
            var frames = payload?.frames?.Where(frame => frame != null).ToArray();
            if (frames == null || frames.Length == 0)
                frames = payload?.frame != null ? new[] { payload.frame } : Array.Empty<FigmaNodeData>();
            if (frames.Length == 0)
                throw new ArgumentException("Payload does not contain any frames.");
            var duplicateIds = frames.Where(frame => !string.IsNullOrEmpty(frame.id))
                .GroupBy(frame => frame.id).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToArray();
            if (duplicateIds.Length > 0)
                throw new InvalidDataException("Duplicate selected Figma frame ids: " +
                                               string.Join(", ", duplicateIds));

            activeProgress = progress;
            progressCompleted = 0;
            progressTotal = Mathf.Max(1, frames.Sum(frame => CountNodes(frame)));
            ReportProgress("Preparing Canvas");
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(frames.Length == 1 ? "Import Figma Frame" : "Import Figma Frames");
            var canvas = FindOrCreateCanvas();
            EnsureResponsiveCanvas(canvas, frames[0]);
            Canvas.ForceUpdateCanvases();
            var canvasSize = canvas.GetComponent<RectTransform>().rect.size;
            var roots = new List<GameObject>(frames.Length);
            try
            {
                foreach (var frame in frames)
                    roots.Add(BuildFrame(frame, canvas, canvasSize));
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                activeProgress = null;
                throw;
            }

            try
            {
                Undo.CollapseUndoOperations(undoGroup);
                Selection.objects = roots.Cast<UnityEngine.Object>().ToArray();
                Selection.activeGameObject = roots[0];
                EditorGUIUtility.PingObject(roots[0]);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                foreach (var root in roots) LogImportSummary(root);
                activeProgress?.Invoke(1f, "Finishing Frame");
                return roots;
            }
            finally
            {
                activeProgress = null;
            }
        }

        private static GameObject BuildFrame(FigmaNodeData frame, Canvas canvas, Vector2 canvasSize)
        {
            // Never scale independent X/Y axes: that distorts every image on a
            // Canvas whose aspect differs from the selected Figma frame.
            var scaleX = frame.width > 0f ? canvasSize.x / frame.width : 1f;
            var scaleY = frame.height > 0f ? canvasSize.y / frame.height : 1f;
            var uniformScale = Mathf.Max(0.0001f, Mathf.Min(scaleX, scaleY));
            var scale = Vector2.one * uniformScale;
            var matchingRoots = canvas.GetComponentsInChildren<FigmaNodeMetadata>(true)
                .Where(x => x.NodeId == frame.id)
                .Select(x => x.gameObject)
                .Distinct()
                .ToArray();
            var existing = matchingRoots.FirstOrDefault();
            var replacedSpritePaths = existing != null
                ? CollectGeneratedSpritePaths(existing)
                : new HashSet<string>();
            for (var i = 1; i < matchingRoots.Length; i++)
                Undo.DestroyObjectImmediate(matchingRoots[i]);
            GameObject root;
            if (existing != null)
            {
                root = existing;
                Undo.RegisterFullObjectHierarchyUndo(root, "Update Figma Frame");
                for (var i = root.transform.childCount - 1; i >= 0; i--)
                    Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);
                RemoveGeneratedPresentation(root);
            }
            else
            {
                root = CreateUiObject(frame.name, canvas.transform);
                Undo.RegisterCreatedObjectUndo(root, "Import Figma Frame");
            }

            ValidateStrictPayload(frame);
            ConfigureNode(root, frame, true, scale, canvasSize, Vector2.zero, false);
            // Use Figma constraints and geometric edge/centre inference. Forcing
            // every descendant into percentage anchors makes all artwork stretch.
            BuildChildren(frame, root.transform, scale, false);
            FitRootToCanvas(root);
            FigmaAnimationBuilder.Build(root, frame, scale);
            ValidateBuiltStructure(root, frame);
            CleanupReplacedGeneratedSprites(replacedSpritePaths, CollectGeneratedSpritePaths(root));
            return root;
        }

        private static void BuildChildren(FigmaNodeData parentData, Transform parent, Vector2 scale,
            bool inheritedProportional = false)
        {
            if (parentData.children == null) return;
            var scrollAxis = DetectScrollAxis(parentData);
            var childParent = parent;
            var parentDesignSize = new Vector2(
                Mathf.Max(0.01f, parentData.width),
                Mathf.Max(0.01f, parentData.height));
            // Once a branch participates in responsive proportional layout, every
            // descendant must keep its normalized Figma bounds. Resetting this at
            // each group made nested fades/gradients drift to center or an edge.
            var proportionalChildren = inheritedProportional;
            if (scrollAxis != ScrollAxis.None)
            {
                var scroll = CreateScrollContent(parent.gameObject, parentData, scale, scrollAxis);
                childParent = scroll.Transform;
                parentDesignSize = scroll.DesignSize;
                proportionalChildren = true;
            }

            Transform activeMask = null;
            FigmaNodeData activeMaskData = null;
            foreach (var child in parentData.children)
            {
                if (child == null) continue;
                var go = CreateUiObject(child.name, childParent);
                ConfigureNode(go, child, false, scale, Vector2.zero, parentDesignSize, proportionalChildren);
                BuildChildren(child, go.transform, scale, proportionalChildren);
                go.SetActive(child.visible);

                if (child.isMask)
                {
                    activeMask = go.transform;
                    activeMaskData = child;
                }
                else if (activeMask != null)
                {
                    // Figma masks affect the following siblings in paint order.
                    // Rebuild the responsive rect in the mask's local coordinate
                    // system. Keeping the former world rect made the mask resize
                    // while its image stayed at the reference width, exposing gaps.
                    go.transform.SetParent(activeMask, false);
                    ApplyMaskRelativeRect(go.GetComponent<RectTransform>(), child, activeMaskData, scale,
                        proportionalChildren);
                    ApplyNodeFlip(go.GetComponent<RectTransform>(), child);
                    go.transform.SetAsLastSibling();
                    ConfigureMaskedImageScaling(go, child);
                }
            }
        }

        private static void ConfigureNode(GameObject go, FigmaNodeData data, bool isRoot, Vector2 scale,
            Vector2 canvasSize, Vector2 parentDesignSize, bool forceProportional)
        {
            progressCompleted++;
            ReportProgress(string.IsNullOrWhiteSpace(data.name) ? data.type : data.name);
            go.name = string.IsNullOrWhiteSpace(data.name) ? data.type : data.name;
            var rect = go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (isRoot)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = canvasSize;
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                ApplyResponsiveRect(rect, data, scale, parentDesignSize, forceProportional);
            }
            ApplyNodeFlip(rect, data);
            rect.localRotation = Quaternion.Euler(0f, 0f, -data.rotation);

            var metadata = go.GetComponent<FigmaNodeMetadata>() ?? Undo.AddComponent<FigmaNodeMetadata>(go);
            metadata.Set(data.id, data.type, data.name);

            Graphic graphic = null;
            if (data.renderMode == "text")
            {
                var text = Undo.AddComponent<Text>(go);
                text.text = data.text ?? string.Empty;
                text.fontSize = Mathf.Max(1, Mathf.RoundToInt(data.fontSize * scale.y));
                text.color = ToColor(data.color, Color.white);
                text.alignment = GetAlignment(data.textAlignHorizontal, data.textAlignVertical);
                text.fontStyle = FontStyle.Normal;
                text.resizeTextForBestFit = false;
                // Match Figma's text sizing mode: Auto width stays on one line,
                // while fixed-width and auto-height boxes wrap at their bounds.
                text.horizontalOverflow = string.Equals(
                    data.textAutoResize,
                    "WIDTH_AND_HEIGHT",
                    StringComparison.OrdinalIgnoreCase)
                    ? HorizontalWrapMode.Overflow
                    : HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.supportRichText = false;
                text.raycastTarget = data.interactive;
                text.lineSpacing = data.lineHeight > 0f && data.fontSize > 0f
                    ? data.lineHeight / data.fontSize
                    : 1f;
                var font = FindBestFont(data.fontFamily, data.fontStyle);
                if (font != null)
                {
                    text.font = font;
                    // Use a real face when the project/system provides one.
                    // Legacy UI Text has no weight axis, so synthesize the
                    // closest Bold/Italic style only for a family-only font.
                    text.fontStyle = FontContainsRequestedStyle(font, data.fontStyle)
                        ? FontStyle.Normal
                        : ResolveLegacyFontStyle(data.fontStyle);
                }
                ConfigureLegacyTextShadows(go, data, scale, metadata);
                graphic = text;
            }
            else if (!string.IsNullOrEmpty(data.imageBase64))
            {
                // A Unity UI Mask requires its Graphic on the same GameObject.
                // Mask effects/render overflow are irrelevant to the stencil shape.
                var imageTarget = data.isMask ? go : CreateImageTarget(go, data);
                var image = Undo.AddComponent<Image>(imageTarget);
                var sprite = FigmaAssetStore.GetOrCreateSprite(
                    data.imageBase64,
                    data.imageMime,
                    data.name,
                    data.imageIsTintMask || data.isMask,
                    data.assetReuseKey);
                image.sprite = sprite;
                if (data.isMask && IsDegenerateMaskSprite(sprite, data))
                {
                    // A failed isolated Figma mask export used to be a transparent
                    // 1x1 sprite. Never let that erase all masked descendants: a
                    // plain Image is an opaque rectangular stencil until the plugin
                    // supplies the real alpha shape on the next import.
                    image.sprite = null;
                    image.color = Color.white;
                }
                image.preserveAspect = string.Equals(
                    data.imageScaleMode,
                    "FIT",
                    StringComparison.OrdinalIgnoreCase);
                image.color = ToColor(data.imageTint, Color.white);
                graphic = image;
            }
            else if (data.color != null && data.color.Length >= 4)
            {
                var image = Undo.AddComponent<Image>(go);
                image.color = new Color(data.color[0], data.color[1], data.color[2], data.color[3]);
                graphic = image;
            }

            if (graphic != null)
            {
                var color = graphic.color;
                if ((data.children == null || data.children.Length == 0) &&
                    !HasAnimationTrack(data, "OPACITY"))
                    color.a *= Mathf.Clamp01(data.opacity);
                graphic.color = color;
                graphic.raycastTarget = data.interactive;
            }

            if (data.isMask)
            {
                var maskImage = go.GetComponent<Image>();
                if (maskImage == null)
                {
                    maskImage = Undo.AddComponent<Image>(go);
                    maskImage.color = Color.white;
                }
                maskImage.raycastTarget = false;
                var mask = Undo.AddComponent<Mask>(go);
                mask.showMaskGraphic = false;
            }

            // The selected screen root must never clip the whole imported UI.
            // Scroll groups receive their clipping only on the standard child
            // Viewport created by CreateScrollContent.
            if (!isRoot && data.clipsContent && DetectScrollAxis(data) == ScrollAxis.None)
                Undo.AddComponent<RectMask2D>(go);

            if (data.children != null && data.children.Length > 0 && data.opacity < 0.999f)
            {
                var group = Undo.AddComponent<CanvasGroup>(go);
                group.alpha = Mathf.Clamp01(data.opacity);
                group.interactable = true;
                group.blocksRaycasts = true;
            }

            if (data.interactive)
            {
                if (graphic == null)
                {
                    var hitArea = Undo.AddComponent<Image>(go);
                    hitArea.color = new Color(1f, 1f, 1f, 0f);
                    hitArea.raycastTarget = true;
                    graphic = hitArea;
                }
                var button = Undo.AddComponent<Button>(go);
                button.targetGraphic = graphic;
                button.transition = Selectable.Transition.ColorTint;
            }
        }

        private static void ReportProgress(string stage)
        {
            activeProgress?.Invoke(
                Mathf.Clamp01((float)progressCompleted / Mathf.Max(1, progressTotal)),
                stage);
        }

        private static Vector2 GetCenteredPosition(FigmaNodeData data, Vector2 scale)
        {
            var radians = data.rotation * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            var halfWidth = data.width * 0.5f * ScaleSign(data.scaleX);
            var halfHeight = data.height * 0.5f * ScaleSign(data.scaleY);
            return new Vector2(
                (data.x + cos * halfWidth - sin * halfHeight) * scale.x,
                -(data.y + sin * halfWidth + cos * halfHeight) * scale.y);
        }

        private static void ApplyNodeFlip(RectTransform rect, FigmaNodeData data)
        {
            rect.localScale = new Vector3(ScaleSign(data.scaleX), ScaleSign(data.scaleY), 1f);
        }

        private static float ScaleSign(float value)
        {
            return value < 0f ? -1f : 1f;
        }

        private static void ApplyResponsiveRect(RectTransform rect, FigmaNodeData data, Vector2 scale,
            Vector2 parentDesignSize, bool forceProportional)
        {
            var parentWidth = Mathf.Max(0.01f, parentDesignSize.x);
            var parentHeight = Mathf.Max(0.01f, parentDesignSize.y);
            if (forceProportional)
            {
                ApplyProportionalRect(rect, data, parentWidth, parentHeight);
                return;
            }

            var horizontal = ResolveHorizontalAnchors(data, parentWidth, forceProportional);
            var vertical = ResolveVerticalAnchors(data, parentHeight, forceProportional);
            var scrollAxis = DetectScrollAxis(data);
            if (scrollAxis == ScrollAxis.Vertical || scrollAxis == ScrollAxis.Both)
                horizontal = new Vector2(0f, 1f);
            if (scrollAxis == ScrollAxis.Horizontal || scrollAxis == ScrollAxis.Both)
                vertical = new Vector2(0f, 1f);
            rect.anchorMin = new Vector2(horizontal.x, vertical.x);
            rect.anchorMax = new Vector2(horizontal.y, vertical.y);

            var parentPixelSize = new Vector2(parentWidth * scale.x, parentHeight * scale.y);
            var desiredSize = new Vector2(
                Mathf.Max(0.01f, data.width * scale.x),
                Mathf.Max(0.01f, data.height * scale.y));
            var anchorSpan = rect.anchorMax - rect.anchorMin;
            rect.sizeDelta = desiredSize - Vector2.Scale(parentPixelSize, anchorSpan);

            var desiredCenter = GetCenteredPosition(data, scale);
            var anchorCenter = new Vector2(
                (rect.anchorMin.x + rect.anchorMax.x) * 0.5f * parentPixelSize.x,
                -(1f - (rect.anchorMin.y + rect.anchorMax.y) * 0.5f) * parentPixelSize.y);
            rect.anchoredPosition = desiredCenter - anchorCenter;
        }

        private static void ApplyProportionalRect(RectTransform rect, FigmaNodeData data,
            float parentWidth, float parentHeight)
        {
            // Figma x/y describe the transformed local origin. Derive the actual
            // pivot center before normalizing; using x + width/2 directly breaks
            // 180-degree/mirrored layers and moves their artwork across a group.
            var center = GetCenteredPosition(data, Vector2.one);
            var normalizedCenter = new Vector2(
                center.x / parentWidth,
                1f + center.y / parentHeight);
            var normalizedHalfSize = new Vector2(
                Mathf.Max(0.01f, data.width) / (2f * parentWidth),
                Mathf.Max(0.01f, data.height) / (2f * parentHeight));

            // Unity supports anchors outside 0..1. They are required for valid
            // Figma groups deliberately extending beyond the frame (HUD bars,
            // overscan backgrounds and edge decorations). Never clamp them.
            rect.anchorMin = normalizedCenter - normalizedHalfSize;
            rect.anchorMax = normalizedCenter + normalizedHalfSize;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private static Vector2 ResolveHorizontalAnchors(FigmaNodeData data, float parentWidth,
            bool forceProportional)
        {
            var left = data.x;
            var right = parentWidth - data.x - data.width;
            var normalizedLeft = Mathf.Clamp01(left / parentWidth);
            var normalizedRight = Mathf.Clamp01((data.x + data.width) / parentWidth);
            if (forceProportional || IsConstraint(data.constraintHorizontal, "SCALE"))
                return OrderedAnchors(normalizedLeft, normalizedRight);
            if (IsConstraint(data.constraintHorizontal, "STRETCH")) return new Vector2(0f, 1f);

            // Full-width geometry must stay full-width even when Figma carries a
            // stale CENTER constraint. This commonly happens to screen gradients
            // copied from another frame. The actual bounds are authoritative.
            var threshold = parentWidth * 0.1f;
            if (data.width / parentWidth >= 0.8f || left <= threshold && right <= threshold)
                return new Vector2(0f, 1f);

            if (IsConstraint(data.constraintHorizontal, "CENTER")) return new Vector2(0.5f, 0.5f);
            // Geometry wins over an inherited edge constraint. Groups such as a
            // bottom tab bar are often centered in Figma while their raw constraint
            // remains MAX/MIN from an earlier parent layout.
            var center = Mathf.Clamp01((data.x + data.width * 0.5f) / parentWidth);
            if (Mathf.Abs(center - 0.5f) <= 0.1f) return new Vector2(0.5f, 0.5f);
            if (IsConstraint(data.constraintHorizontal, "MAX")) return Vector2.one;

            if (left <= threshold) return Vector2.zero;
            if (right <= threshold) return Vector2.one;
            return new Vector2(center, center);
        }

        private static void ApplyMaskRelativeRect(RectTransform rect, FigmaNodeData data,
            FigmaNodeData maskData, Vector2 scale, bool forceProportional)
        {
            if (rect == null || maskData == null) return;
            var parentWidth = Mathf.Max(0.01f, maskData.width);
            var parentHeight = Mathf.Max(0.01f, maskData.height);
            var localData = new FigmaNodeData
            {
                x = data.x - maskData.x,
                y = data.y - maskData.y,
                width = data.width,
                height = data.height,
                rotation = data.rotation - maskData.rotation,
                constraintHorizontal = data.constraintHorizontal,
                constraintVertical = data.constraintVertical
            };

            var toleranceX = Mathf.Max(0.5f, parentWidth * 0.01f);
            var toleranceY = Mathf.Max(0.5f, parentHeight * 0.01f);
            var fillsWidth = Mathf.Abs(localData.x) <= toleranceX &&
                             Mathf.Abs(localData.x + localData.width - parentWidth) <= toleranceX;
            var fillsHeight = Mathf.Abs(localData.y) <= toleranceY &&
                              Mathf.Abs(localData.y + localData.height - parentHeight) <= toleranceY;
            if (fillsWidth) localData.constraintHorizontal = "STRETCH";
            if (fillsHeight) localData.constraintVertical = "STRETCH";
            ApplyResponsiveRect(rect, localData, scale, new Vector2(parentWidth, parentHeight),
                forceProportional);
            rect.localRotation = Quaternion.Euler(0f, 0f, -localData.rotation);
        }

        private static void ConfigureMaskedImageScaling(GameObject owner, FigmaNodeData data)
        {
            if (!string.Equals(data.renderMode, "image", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(data.imageScaleMode, "FILL", StringComparison.OrdinalIgnoreCase)) return;
            var image = owner.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(candidate => candidate.sprite != null);
            if (image == null || image.sprite == null || image.sprite.rect.height <= 0f) return;
            image.preserveAspect = false;
            var fitter = image.GetComponent<AspectRatioFitter>() ??
                         Undo.AddComponent<AspectRatioFitter>(image.gameObject);
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
        }

        private static Vector2 ResolveVerticalAnchors(FigmaNodeData data, float parentHeight,
            bool forceProportional)
        {
            var top = data.y;
            var bottom = parentHeight - data.y - data.height;
            var normalizedBottom = Mathf.Clamp01(1f - (data.y + data.height) / parentHeight);
            var normalizedTop = Mathf.Clamp01(1f - data.y / parentHeight);
            if (forceProportional || IsConstraint(data.constraintVertical, "SCALE"))
                return OrderedAnchors(normalizedBottom, normalizedTop);
            if (IsConstraint(data.constraintVertical, "STRETCH")) return new Vector2(0f, 1f);
            if (IsConstraint(data.constraintVertical, "CENTER")) return new Vector2(0.5f, 0.5f);
            if (IsConstraint(data.constraintVertical, "MAX")) return Vector2.zero;

            var threshold = parentHeight * 0.1f;
            if (data.height / parentHeight >= 0.8f || top <= threshold && bottom <= threshold)
                return new Vector2(0f, 1f);
            if (top <= threshold) return Vector2.one;
            if (bottom <= threshold) return Vector2.zero;
            var centerFromTop = Mathf.Clamp01((data.y + data.height * 0.5f) / parentHeight);
            var center = 1f - centerFromTop;
            if (Mathf.Abs(center - 0.5f) <= 0.1f) center = 0.5f;
            return new Vector2(center, center);
        }

        private static Vector2 OrderedAnchors(float min, float max)
        {
            min = Mathf.Clamp01(min);
            max = Mathf.Clamp01(max);
            if (max < min) (min, max) = (max, min);
            if (max - min < 0.0001f) max = Mathf.Min(1f, min + 0.0001f);
            return new Vector2(min, max);
        }

        private static bool IsConstraint(string value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static ScrollAxis DetectScrollAxis(FigmaNodeData data)
        {
            if (data?.children == null || data.children.Length == 0) return ScrollAxis.None;
            var overflow = (data.overflowDirection ?? string.Empty).ToUpperInvariant();
            var explicitBoth = overflow.Contains("BOTH") || overflow.Contains("HORIZONTAL_AND_VERTICAL");
            var explicitHorizontal = overflow.Contains("HORIZONTAL");
            var explicitVertical = overflow.Contains("VERTICAL");
            var name = (data.name ?? string.Empty).ToLowerInvariant();
            var namedScroll = name.Contains("scroll") || name.Contains("carousel") ||
                              name.Contains("gallery") || name.Contains("feed") ||
                              ContainsWord(name, "list");
            // clipsContent means clipping, not scrolling. It must never promote a
            // normal screen/card parent to ScrollRect by itself.
            if (!namedScroll && !explicitBoth && !explicitHorizontal && !explicitVertical)
                return ScrollAxis.None;

            var visible = data.children.Where(child => child != null && child.visible).ToArray();
            if (visible.Length == 0) return ScrollAxis.None;
            var minLeft = visible.Min(child => child.x);
            var minTop = visible.Min(child => child.y);
            var maxRight = visible.Max(child => child.x + child.width);
            var maxBottom = visible.Max(child => child.y + child.height);
            var excessX = Mathf.Max(0f, maxRight - data.width) + Mathf.Max(0f, -minLeft);
            var excessY = Mathf.Max(0f, maxBottom - data.height) + Mathf.Max(0f, -minTop);
            var thresholdX = Mathf.Max(1f, data.width * 0.01f);
            var thresholdY = Mathf.Max(1f, data.height * 0.01f);
            var hasHorizontalOverflow = excessX > thresholdX;
            var hasVerticalOverflow = excessY > thresholdY;
            if (hasVerticalOverflow && (!hasHorizontalOverflow || excessY > excessX * 1.15f))
                return ScrollAxis.Vertical;
            if (hasHorizontalOverflow && (!hasVerticalOverflow || excessX > excessY * 1.15f))
                return ScrollAxis.Horizontal;
            if (hasHorizontalOverflow && hasVerticalOverflow && explicitBoth)
                return ScrollAxis.Both;

            var layout = (data.layoutMode ?? string.Empty).ToUpperInvariant();
            if (layout.Contains("HORIZONTAL")) return ScrollAxis.Horizontal;
            if (layout.Contains("VERTICAL")) return ScrollAxis.Vertical;
            var minCenterX = visible.Min(child => child.x + child.width * 0.5f);
            var maxCenterX = visible.Max(child => child.x + child.width * 0.5f);
            var minCenterY = visible.Min(child => child.y + child.height * 0.5f);
            var maxCenterY = visible.Max(child => child.y + child.height * 0.5f);
            var spreadX = maxCenterX - minCenterX;
            var spreadY = maxCenterY - minCenterY;
            if (spreadX > spreadY * 1.25f) return ScrollAxis.Horizontal;
            if (spreadY > spreadX * 1.25f) return ScrollAxis.Vertical;
            if (explicitBoth) return ScrollAxis.Both;
            if (explicitVertical) return ScrollAxis.Vertical;
            if (explicitHorizontal) return ScrollAxis.Horizontal;
            return data.height >= data.width ? ScrollAxis.Vertical : ScrollAxis.Horizontal;
        }

        private static bool ContainsWord(string value, string word)
        {
            return (" " + value.Replace('_', ' ').Replace('-', ' ') + " ")
                .Contains(" " + word + " ");
        }

        private static ScrollContentSetup CreateScrollContent(GameObject owner, FigmaNodeData data,
            Vector2 scale, ScrollAxis axis)
        {
            var designSize = CalculateScrollContentSize(data, axis);
            var rootMask = owner.GetComponent<RectMask2D>();
            if (rootMask != null) Undo.DestroyObjectImmediate(rootMask);

            // Standard Unity Scroll View hierarchy. The source Figma group owns
            // ScrollRect only; clipping and hit testing live on its Viewport.
            var viewportObject = CreateUiObject("Viewport", owner.transform);
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.localScale = Vector3.one;
            viewport.localRotation = Quaternion.identity;
            var viewportImage = Undo.AddComponent<Image>(viewportObject);
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            Undo.AddComponent<RectMask2D>(viewportObject);

            var contentObject = CreateUiObject("Content", viewport);
            var content = contentObject.GetComponent<RectTransform>();
            content.localScale = Vector3.one;
            content.localRotation = Quaternion.identity;
            var horizontal = axis == ScrollAxis.Horizontal || axis == ScrollAxis.Both;
            var vertical = axis == ScrollAxis.Vertical || axis == ScrollAxis.Both;
            if (vertical && !horizontal)
            {
                content.pivot = new Vector2(0.5f, 1f);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = Vector2.one;
                content.sizeDelta = new Vector2(0f, designSize.y * scale.y);
                var fitter = Undo.AddComponent<AspectRatioFitter>(contentObject);
                fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                fitter.aspectRatio = Mathf.Max(0.0001f,
                    designSize.x * scale.x / Mathf.Max(0.0001f, designSize.y * scale.y));
            }
            else if (horizontal && !vertical)
            {
                content.pivot = new Vector2(0f, 0.5f);
                content.anchorMin = Vector2.zero;
                content.anchorMax = new Vector2(0f, 1f);
                content.sizeDelta = new Vector2(designSize.x * scale.x, 0f);
                var fitter = Undo.AddComponent<AspectRatioFitter>(contentObject);
                fitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                fitter.aspectRatio = Mathf.Max(0.0001f,
                    designSize.x * scale.x / Mathf.Max(0.0001f, designSize.y * scale.y));
            }
            else
            {
                content.pivot = new Vector2(0f, 1f);
                content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
                content.sizeDelta = Vector2.Scale(designSize, scale);
            }
            content.anchoredPosition = Vector2.zero;
            content.SetAsLastSibling();

            var scrollRect = owner.GetComponent<ScrollRect>() ?? Undo.AddComponent<ScrollRect>(owner);
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = horizontal;
            scrollRect.vertical = vertical;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 30f;
            scrollRect.horizontalNormalizedPosition = 0f;
            scrollRect.verticalNormalizedPosition = 1f;
            return new ScrollContentSetup(content, designSize);
        }

        private static Vector2 CalculateScrollContentSize(FigmaNodeData data, ScrollAxis axis)
        {
            var width = Mathf.Max(0.01f, data.width);
            var height = Mathf.Max(0.01f, data.height);
            foreach (var child in data.children.Where(child => child != null && child.visible))
            {
                width = Mathf.Max(width, child.x + child.width);
                height = Mathf.Max(height, child.y + child.height);
            }
            if (axis == ScrollAxis.Vertical) width = Mathf.Max(0.01f, data.width);
            if (axis == ScrollAxis.Horizontal) height = Mathf.Max(0.01f, data.height);
            return new Vector2(width, height);
        }

        private enum ScrollAxis
        {
            None,
            Horizontal,
            Vertical,
            Both
        }

        private readonly struct ScrollContentSetup
        {
            public readonly RectTransform Transform;
            public readonly Vector2 DesignSize;

            public ScrollContentSetup(RectTransform transform, Vector2 designSize)
            {
                Transform = transform;
                DesignSize = designSize;
            }
        }

        private static GameObject CreateImageTarget(GameObject owner, FigmaNodeData data)
        {
            var visualX = data.visualX;
            var visualY = data.visualY;
            var visualWidth = data.visualWidth;
            var visualHeight = data.visualHeight;
            var invalidLocalBounds = !IsFinite(visualX) || !IsFinite(visualY) ||
                !IsFinite(visualWidth) || !IsFinite(visualHeight) ||
                visualWidth <= 0.01f || visualHeight <= 0.01f;
            if (invalidLocalBounds)
            {
                visualWidth = Mathf.Max(0.01f, data.width);
                visualHeight = Mathf.Max(0.01f, data.height);
                visualX = 0f;
                visualY = 0f;
            }

            var hasSeparateRenderBounds =
                Mathf.Abs(visualX) > 0.01f || Mathf.Abs(visualY) > 0.01f ||
                Mathf.Abs(visualWidth - data.width) > 0.01f ||
                Mathf.Abs(visualHeight - data.height) > 0.01f;
            if (!hasSeparateRenderBounds) return owner;

            var visual = CreateUiObject("__FigmaVisual", owner.transform);
            var rect = visual.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
            var ownerWidth = Mathf.Max(0.01f, data.width);
            var ownerHeight = Mathf.Max(0.01f, data.height);
            rect.anchorMin = new Vector2(
                visualX / ownerWidth,
                1f - (visualY + visualHeight) / ownerHeight);
            rect.anchorMax = new Vector2(
                (visualX + visualWidth) / ownerWidth,
                1f - visualY / ownerHeight);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetAsFirstSibling();
            return visual;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            var go = new GameObject(string.IsNullOrWhiteSpace(name) ? "Figma Layer" : name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void FitRootToCanvas(GameObject root)
        {
            var rect = root.GetComponent<RectTransform>();
            foreach (var fitter in root.GetComponents<AspectRatioFitter>())
                Undo.DestroyObjectImmediate(fitter);
            foreach (var fitter in root.GetComponents<ContentSizeFitter>())
                Undo.DestroyObjectImmediate(fitter);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();
        }

        private static void EnsureResponsiveCanvas(Canvas canvas, FigmaNodeData referenceFrame)
        {
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace || referenceFrame == null)
                return;

            var scaler = canvas.GetComponent<CanvasScaler>() ?? Undo.AddComponent<CanvasScaler>(canvas.gameObject);
            // Preserve explicit project settings, but upgrade both Unity's blank
            // Canvas and the legacy Canvas created by older importer versions.
            var legacyImporterCanvas = canvas.name == "Canvas" &&
                                       scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize &&
                                       scaler.screenMatchMode == CanvasScaler.ScreenMatchMode.MatchWidthOrHeight &&
                                       Vector2.Distance(scaler.referenceResolution, new Vector2(1080f, 1920f)) < 0.1f &&
                                       Mathf.Abs(scaler.matchWidthOrHeight - 0.5f) < 0.001f;
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize && !legacyImporterCanvas) return;

            Undo.RecordObject(scaler, "Configure responsive Figma Canvas");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                Mathf.Max(1f, referenceFrame.width),
                Mathf.Max(1f, referenceFrame.height));
            // The logical Canvas never becomes smaller than the source frame.
            // Extra aspect is consumed by anchors instead of clipping/stretching.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
            EditorUtility.SetDirty(scaler);
        }

        private static void LogImportSummary(GameObject root)
        {
            var metadata = root.GetComponentsInChildren<FigmaNodeMetadata>(true);
            var nodes = metadata.Length;
            var texts = root.GetComponentsInChildren<Text>(true).Length;
            var images = root.GetComponentsInChildren<Image>(true).Length;
            var buttons = root.GetComponentsInChildren<Button>(true).Length;
            var masks = root.GetComponentsInChildren<Mask>(true).Length;
            var scrolls = root.GetComponentsInChildren<ScrollRect>(true).Length;
            var duplicateIds = metadata.Where(x => !string.IsNullOrEmpty(x.NodeId))
                .GroupBy(x => x.NodeId).Count(group => group.Count() > 1);
            var nonUnitScales = root.GetComponentsInChildren<RectTransform>(true)
                .Count(HasNonUnitScaleMagnitude);
            var flips = root.GetComponentsInChildren<RectTransform>(true)
                .Count(HasReflection);
            Debug.Log($"[Figma Importer] Structure: {nodes} nodes, {texts} Legacy UI texts, {images} images, " +
                      $"{buttons} buttons, {masks} masks, {scrolls} scroll views, {duplicateIds} duplicate node ids, " +
                      $"{flips} flipped transforms, {nonUnitScales} non-unit scale magnitudes.");
        }

        private static HashSet<string> CollectGeneratedSpritePaths(GameObject root)
        {
            var folder = (FigmaBridgeSettings.AssetFolder ?? FigmaBridgeSettings.DefaultAssetFolder)
                .Replace('\\', '/').TrimEnd('/');
            var prefix = folder + "/figma_";
            return root.GetComponentsInChildren<Image>(true)
                .Select(image => image.sprite == null ? string.Empty : AssetDatabase.GetAssetPath(image.sprite))
                .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
        }

        private static void CleanupReplacedGeneratedSprites(HashSet<string> previous, HashSet<string> current)
        {
            if (previous == null || previous.Count == 0) return;
            previous.ExceptWith(current);
            if (previous.Count == 0) return;

            // Protect references held by every currently loaded hierarchy,
            // including arbitrary MonoBehaviours rather than only UI Images.
            var loadedObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            var loadedDependencies = EditorUtility.CollectDependencies(loadedObjects)
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToHashSet(StringComparer.Ordinal);
            previous.ExceptWith(loadedDependencies);
            if (previous.Count == 0) return;

            // Protect references in unloaded scenes, prefabs and assets. Candidate
            // textures themselves are excluded from the dependency roots so an
            // asset is not mistaken for an external reference to itself.
            var loadedScenePaths = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).path)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToHashSet(StringComparer.Ordinal);
            var projectRoots = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal) &&
                               !AssetDatabase.IsValidFolder(path) &&
                               !loadedScenePaths.Contains(path) &&
                               !previous.Contains(path))
                .ToArray();
            var projectDependencies = AssetDatabase.GetDependencies(projectRoots, true)
                .ToHashSet(StringComparer.Ordinal);
            previous.ExceptWith(projectDependencies);

            var deleted = 0;
            foreach (var path in previous)
            {
                if (AssetDatabase.DeleteAsset(path)) deleted++;
            }
            if (deleted > 0)
                Debug.Log($"[Figma Importer] Removed {deleted} replaced, unreferenced generated texture(s).");
        }

        private static void RemoveGeneratedPresentation(GameObject root)
        {
            var animation = root.GetComponent<Animation>();
            if (animation != null) Undo.DestroyObjectImmediate(animation);
            var button = root.GetComponent<Button>();
            if (button != null) Undo.DestroyObjectImmediate(button);
            var image = root.GetComponent<Image>();
            if (image != null) Undo.DestroyObjectImmediate(image);
            var group = root.GetComponent<CanvasGroup>();
            if (group != null) Undo.DestroyObjectImmediate(group);
            var text = root.GetComponent<Text>();
            if (text != null) Undo.DestroyObjectImmediate(text);
            var mask = root.GetComponent<Mask>();
            if (mask != null) Undo.DestroyObjectImmediate(mask);
            var rectMask = root.GetComponent<RectMask2D>();
            if (rectMask != null) Undo.DestroyObjectImmediate(rectMask);
            var scrollRect = root.GetComponent<ScrollRect>();
            if (scrollRect != null) Undo.DestroyObjectImmediate(scrollRect);
        }

        private static bool HasAnimationTrack(FigmaNodeData data, string property)
        {
            return data?.animationTracks != null && data.animationTracks.Any(track =>
                track != null && string.Equals(track.property, property,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static void ValidateStrictPayload(FigmaNodeData frame)
        {
            var ids = new HashSet<string>();
            ValidateStrictNode(frame, ids);
        }

        private static void ValidateStrictNode(FigmaNodeData node, HashSet<string> ids)
        {
            if (node == null) throw new InvalidDataException("Figma payload contains a null layer.");
            if (string.IsNullOrWhiteSpace(node.id))
                throw new InvalidDataException($"Figma layer '{node.name}' has no node id.");
            if (!ids.Add(node.id))
                throw new InvalidDataException($"Figma layer '{node.name}' ({node.id}) was serialized more than once.");
            if (node.renderMode == "text" && !string.IsNullOrEmpty(node.imageBase64))
                throw new InvalidDataException($"Text layer '{node.name}' was incorrectly rasterized.");
            if (node.hasEffects && node.renderMode != "text" &&
                (node.children == null || node.children.Length == 0) &&
                string.IsNullOrEmpty(node.imageBase64))
                throw new InvalidDataException($"Effects on Figma layer '{node.name}' were not exported.");
            if (node.children == null) return;
            foreach (var child in node.children) ValidateStrictNode(child, ids);
        }

        private static void ValidateBuiltStructure(GameObject root, FigmaNodeData frame)
        {
            var expectedNodes = CountNodes(frame);
            var expectedTexts = CountNodes(frame, node => node.renderMode == "text");
            var expectedMasks = CountNodes(frame, node => node.isMask);
            var expectedScrolls = CountNodes(frame, node => DetectScrollAxis(node) != ScrollAxis.None);
            var expectedTextShadows = CountTextShadows(frame);
            var expectedFlips = CountNodes(frame, node => ScaleSign(node.scaleX) < 0f || ScaleSign(node.scaleY) < 0f);
            var metadata = root.GetComponentsInChildren<FigmaNodeMetadata>(true);
            var texts = root.GetComponentsInChildren<Text>(true);
            var textShadows = texts.SelectMany(text => text.GetComponents<Shadow>()).ToArray();
            var masks = root.GetComponentsInChildren<Mask>(true);
            var scrolls = root.GetComponentsInChildren<ScrollRect>(true);
            var duplicateIds = metadata.Where(x => !string.IsNullOrEmpty(x.NodeId))
                .GroupBy(x => x.NodeId).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
            var nonUnitScales = root.GetComponentsInChildren<RectTransform>(true)
                .Where(HasNonUnitScaleMagnitude).ToArray();
            var actualFlips = root.GetComponentsInChildren<RectTransform>(true).Count(HasReflection);

            if (metadata.Length != expectedNodes)
                throw new InvalidDataException($"Layer mismatch: Figma sent {expectedNodes}, Unity built {metadata.Length}.");
            if (texts.Length != expectedTexts)
                throw new InvalidDataException($"Text mismatch: Figma sent {expectedTexts}, Unity built {texts.Length} Legacy UI Text objects.");
            if (masks.Length != expectedMasks)
                throw new InvalidDataException($"Mask mismatch: Figma sent {expectedMasks}, Unity built {masks.Length} UI Masks.");
            if (scrolls.Length != expectedScrolls)
                throw new InvalidDataException($"Scroll mismatch: inferred {expectedScrolls}, Unity built {scrolls.Length} ScrollRects.");
            if (scrolls.Any(scroll => scroll.viewport == null || scroll.content == null))
                throw new InvalidDataException("Every inferred Figma scroll must have a Viewport and Content RectTransform.");
            if (textShadows.Length != expectedTextShadows)
                throw new InvalidDataException($"Text shadow mismatch: Figma sent {expectedTextShadows}, Unity built {textShadows.Length} UI Shadow components.");
            if (masks.Any(mask => mask.GetComponent<Image>() == null || mask.showMaskGraphic))
                throw new InvalidDataException("Every Figma mask must have an Image and Hide Mask Graphic enabled.");
            if (masks.Any(mask => !mask.transform.Cast<Transform>()
                    .Any(child => child.GetComponent<FigmaNodeMetadata>() != null)))
                throw new InvalidDataException("A Figma mask has no masked layer children. Fade/image layers must remain inside their UI Mask.");
            if (duplicateIds.Length > 0)
                throw new InvalidDataException("Duplicate Figma node ids in Unity: " + string.Join(", ", duplicateIds));
            if (nonUnitScales.Length > 0)
                throw new InvalidDataException($"Unity built {nonUnitScales.Length} objects with non-unit scale.");
            if (actualFlips != expectedFlips)
                throw new InvalidDataException($"Flip mismatch: payload has {expectedFlips}, Unity built {actualFlips} reflected RectTransforms.");
        }

        private static bool HasNonUnitScaleMagnitude(RectTransform rect)
        {
            var scale = rect.localScale;
            return Mathf.Abs(Mathf.Abs(scale.x) - 1f) > 0.0001f ||
                   Mathf.Abs(Mathf.Abs(scale.y) - 1f) > 0.0001f ||
                   Mathf.Abs(scale.z - 1f) > 0.0001f;
        }

        private static bool HasReflection(RectTransform rect)
        {
            return rect.localScale.x * rect.localScale.y < 0f;
        }

        private static int CountNodes(FigmaNodeData node, Func<FigmaNodeData, bool> predicate = null)
        {
            if (node == null) return 0;
            var count = predicate == null || predicate(node) ? 1 : 0;
            if (node.children == null) return count;
            foreach (var child in node.children) count += CountNodes(child, predicate);
            return count;
        }

        private static bool IsDegenerateMaskSprite(Sprite sprite, FigmaNodeData data)
        {
            if (sprite == null || sprite.texture == null) return true;
            if (data.width <= 1.5f || data.height <= 1.5f) return false;
            return sprite.texture.width <= 1 || sprite.texture.height <= 1;
        }

        private static void ConfigureLegacyTextShadows(GameObject go, FigmaNodeData data, Vector2 scale,
            FigmaNodeMetadata metadata)
        {
            if (data.textShadows == null || data.textShadows.Length == 0)
            {
                metadata.SetTextShadows(Array.Empty<FigmaTextShadowParameters>());
                return;
            }

            var parameters = new FigmaTextShadowParameters[data.textShadows.Length];
            for (var i = 0; i < data.textShadows.Length; i++)
            {
                var item = data.textShadows[i];
                var color = ToColor(item.color, new Color(0f, 0f, 0f, 0.25f));
                var offset = new Vector2(item.offsetX * scale.x, -item.offsetY * scale.y);
                // Legacy UI Shadow is intentionally hard-edged. Blur and spread
                // remain in metadata for diagnostics but do not affect rendering.
                parameters[i] = new FigmaTextShadowParameters(
                    color,
                    offset,
                    item.radius,
                    item.spread,
                    item.blendMode ?? "NORMAL");
            }

            // Unity's standard Shadow represents one hard shadow. Stacking the
            // component compounds generated mesh vertices, so use the first
            // Figma drop shadow deterministically and keep the rest as metadata.
            var rendered = parameters[0];
            var shadow = Undo.AddComponent<Shadow>(go);
            shadow.effectColor = rendered.color;
            shadow.effectDistance = rendered.offset;
            shadow.useGraphicAlpha = true;
            metadata.SetTextShadows(parameters);
        }

        private static int CountTextShadows(FigmaNodeData node)
        {
            if (node == null) return 0;
            var count = node.renderMode == "text" && node.textShadows != null && node.textShadows.Length > 0 ? 1 : 0;
            if (node.children == null) return count;
            foreach (var child in node.children) count += CountTextShadows(child);
            return count;
        }

        private static Color ToColor(float[] values, Color fallback)
        {
            return values != null && values.Length >= 4
                ? new Color(values[0], values[1], values[2], values[3])
                : fallback;
        }

        private static TextAnchor GetAlignment(string horizontal, string vertical)
        {
            var h = horizontal ?? "LEFT";
            var v = vertical ?? "TOP";
            if (v == "CENTER")
                return h == "CENTER" ? TextAnchor.MiddleCenter : h == "RIGHT" ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            if (v == "BOTTOM")
                return h == "CENTER" ? TextAnchor.LowerCenter : h == "RIGHT" ? TextAnchor.LowerRight : TextAnchor.LowerLeft;
            return h == "CENTER" ? TextAnchor.UpperCenter : h == "RIGHT" ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
        }

        private static Font FindBestFont(string family, string style)
        {
            if (string.IsNullOrWhiteSpace(family)) return GetLegacyFallbackFont();
            var key = family + "|" + style;
            if (FontCache.TryGetValue(key, out var cached)) return cached;

            Font best = null;
            var bestScore = int.MinValue;
            foreach (var guid in AssetDatabase.FindAssets("t:Font"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var assetPath = AssetDatabase.GetAssetPath(asset);
                var normalizedAsset = Normalize((asset.name ?? string.Empty) + " " +
                                                Path.GetFileNameWithoutExtension(assetPath));
                var normalizedFamily = Normalize(family);
                var score = ScoreFontCandidate(normalizedAsset, normalizedFamily, style);
                if (score > bestScore) { bestScore = score; best = asset; }
            }

            var imported = ImportSystemFont(family, style);
            if (imported != null && (bestScore <= 0 || FontContainsRequestedStyle(imported, style)))
                best = imported;
            if (best == null || bestScore <= 0 && imported == null) best = GetLegacyFallbackFont();
            FontCache[key] = best;
            return best;
        }

        private static Font ImportSystemFont(string family, string style)
        {
            var familyKey = Normalize(family);
            if (string.IsNullOrEmpty(familyKey)) return null;

            var roots = new List<string>();
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                roots.Add("/System/Library/Fonts");
                roots.Add("/Library/Fonts");
                roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Fonts"));
            }
            else if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
            }
            else
            {
                roots.Add("/usr/share/fonts");
                roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".fonts"));
            }

            string sourcePath = null;
            var bestScore = int.MinValue;
            foreach (var root in roots.Where(Directory.Exists))
            {
                try
                {
                    foreach (var path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                                 .Where(x => x.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                             x.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)))
                    {
                        var fileKey = Normalize(Path.GetFileNameWithoutExtension(path));
                        var score = ScoreFontCandidate(fileKey, familyKey, style);
                        if (score > bestScore) { bestScore = score; sourcePath = path; }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            if (bestScore <= 0 || string.IsNullOrEmpty(sourcePath)) return null;

            const string fontDirectory = "Assets/FigmaImports/Fonts";
            Directory.CreateDirectory(fontDirectory);
            var safeFamily = string.Concat(family.Where(char.IsLetterOrDigit));
            var sourceHasStyle = CandidateContainsRequestedStyle(
                Normalize(Path.GetFileNameWithoutExtension(sourcePath)), style);
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            var requestedStyle = string.Concat((style ?? string.Empty).Where(char.IsLetterOrDigit));
            var wantsConcreteStyle = GetFontWeight(style) != 0 || IsItalicStyle(style);
            var unityFontPath = $"{fontDirectory}/{safeFamily}{extension}";

            if (wantsConcreteStyle)
            {
                var styledFontPath = $"{fontDirectory}/{safeFamily}-{requestedStyle}{extension}";
                if (File.Exists(styledFontPath) ||
                    sourceHasStyle && TryCopyFont(sourcePath, styledFontPath) ||
                    TryCreateVariableFontInstance(sourcePath, styledFontPath, family, style))
                    unityFontPath = styledFontPath;
            }

            if (!File.Exists(unityFontPath)) File.Copy(sourcePath, unityFontPath, false);
            AssetDatabase.ImportAsset(unityFontPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Font>(unityFontPath);
        }

        private static bool TryCopyFont(string sourcePath, string destinationPath)
        {
            try
            {
                if (!File.Exists(destinationPath)) File.Copy(sourcePath, destinationPath, false);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static bool TryCreateVariableFontInstance(string sourcePath, string destinationPath,
            string family, string style)
        {
            var scriptGuid = AssetDatabase.FindAssets("figma_font_instance")
                .FirstOrDefault(guid => AssetDatabase.GUIDToAssetPath(guid)
                    .EndsWith("figma_font_instance.py", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(scriptGuid)) return false;

            var scriptAssetPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
            var scriptPath = Path.GetFullPath(scriptAssetPath);
            var outputPath = Path.GetFullPath(destinationPath);
            var python = Application.platform == RuntimePlatform.WindowsEditor ? "py" : "python3";
            var arguments = Application.platform == RuntimePlatform.WindowsEditor
                ? $"-3 {QuoteArgument(scriptPath)} {QuoteArgument(sourcePath)} {QuoteArgument(outputPath)} " +
                  $"{QuoteArgument(family)} {QuoteArgument(style)}"
                : $"{QuoteArgument(scriptPath)} {QuoteArgument(sourcePath)} {QuoteArgument(outputPath)} " +
                  $"{QuoteArgument(family)} {QuoteArgument(style)}";
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = python,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == null) return false;
                var standardError = process.StandardError.ReadToEnd();
                process.WaitForExit(30000);
                if (process.ExitCode == 0 && File.Exists(destinationPath)) return true;
                if (process.ExitCode != 3 && process.ExitCode != 4)
                    Debug.LogWarning($"[Figma Importer] Could not create static font '{family} {style}': {standardError}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Figma Importer] Static font generation unavailable: {exception.Message}");
            }
            return false;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static Font GetLegacyFallbackFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        private static int ScoreFontCandidate(string candidate, string family, string style)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(family)) return 0;
            var score = candidate == family ? 200 :
                candidate.Contains(family) || family.Contains(candidate) ? 100 : 0;
            if (score == 0) return 0;

            var requestedWeight = GetFontWeight(style);
            var candidateWeight = GetFontWeight(candidate);
            if (requestedWeight > 0 && candidateWeight > 0)
                score += requestedWeight == candidateWeight ? 80 : -Mathf.Min(60, Mathf.Abs(requestedWeight - candidateWeight) / 5);
            else if (requestedWeight <= 400 && candidateWeight == 0)
                score += 30;

            var wantsItalic = IsItalicStyle(style);
            var hasItalic = IsItalicStyle(candidate);
            score += wantsItalic == hasItalic ? 20 : -20;
            return score;
        }

        private static bool FontContainsRequestedStyle(Font font, string style)
        {
            if (font == null) return false;
            var identity = Normalize((font.name ?? string.Empty) + " " +
                                     Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(font)));
            return CandidateContainsRequestedStyle(identity, style);
        }

        private static bool CandidateContainsRequestedStyle(string candidate, string style)
        {
            var requestedWeight = GetFontWeight(style);
            var weightMatches = requestedWeight <= 400
                ? GetFontWeight(candidate) is 0 or 400
                : GetFontWeight(candidate) == requestedWeight;
            return weightMatches && IsItalicStyle(candidate) == IsItalicStyle(style);
        }

        private static FontStyle ResolveLegacyFontStyle(string style)
        {
            var bold = GetFontWeight(style) >= 600;
            var italic = IsItalicStyle(style);
            if (bold && italic) return FontStyle.BoldAndItalic;
            if (bold) return FontStyle.Bold;
            return italic ? FontStyle.Italic : FontStyle.Normal;
        }

        private static bool IsItalicStyle(string style)
        {
            var value = Normalize(style);
            return value.Contains("italic") || value.Contains("oblique");
        }

        private static int GetFontWeight(string style)
        {
            var value = Normalize(style);
            if (value.Contains("thin")) return 100;
            if (value.Contains("extralight") || value.Contains("ultralight")) return 200;
            if (value.Contains("light")) return 300;
            if (value.Contains("regular") || value.Contains("normal") || value.Contains("book")) return 400;
            if (value.Contains("medium")) return 500;
            if (value.Contains("semibold") || value.Contains("demibold")) return 600;
            if (value.Contains("extrabold") || value.Contains("ultrabold")) return 800;
            if (value.Contains("black") || value.Contains("heavy")) return 900;
            if (value.Contains("bold")) return 700;
            return 0;
        }

        private static Canvas FindOrCreateCanvas()
        {
            var canvas = FindFirstRootCanvas(SceneManager.GetActiveScene());
            if (canvas != null)
            {
                EnsureEventSystem();
                return canvas;
            }

            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas for Figma Import");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();
            return canvas;
        }

        private static Canvas FindFirstRootCanvas(Scene scene)
        {
            // Scene.GetRootGameObjects and Transform sibling indices follow the
            // visible Hierarchy order. Traverse depth-first so canvas selection is
            // deterministic and always chooses the first root Canvas in the scene.
            foreach (var root in scene.GetRootGameObjects())
            {
                var canvas = FindFirstRootCanvas(root.transform);
                if (canvas != null) return canvas;
            }
            return null;
        }

        private static Canvas FindFirstRootCanvas(Transform transform)
        {
            var canvas = transform.GetComponent<Canvas>();
            if (canvas != null && canvas.isRootCanvas) return canvas;
            for (var i = 0; i < transform.childCount; i++)
            {
                canvas = FindFirstRootCanvas(transform.GetChild(i));
                if (canvas != null) return canvas;
            }
            return null;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>(true) != null) return;
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(events, "Create EventSystem");
            SceneManager.MoveGameObjectToScene(events, SceneManager.GetActiveScene());
        }
    }
}
