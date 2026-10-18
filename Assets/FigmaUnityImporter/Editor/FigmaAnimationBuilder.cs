using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Stanislaw.FigmaUnityImporter;

namespace Stanislaw.FigmaUnityImporter.Editor
{
    internal static class FigmaAnimationBuilder
    {
        private sealed class Binding
        {
            public FigmaNodeData data;
            public GameObject target;
            public FigmaAnimationTrackData track;
        }

        private sealed class Plan
        {
            public FigmaNodeData ownerData;
            public GameObject owner;
            public readonly List<Binding> bindings = new List<Binding>();
            public float duration;
        }

        public static void Build(GameObject root, FigmaNodeData frame, Vector2 importScale)
        {
            if (root == null || frame == null) return;
            var dataById = new Dictionary<string, FigmaNodeData>(StringComparer.Ordinal);
            var parentById = new Dictionary<string, FigmaNodeData>(StringComparer.Ordinal);
            Index(frame, null, dataById, parentById);
            var goById = root.GetComponentsInChildren<FigmaNodeMetadata>(true)
                .Where(metadata => !string.IsNullOrEmpty(metadata.NodeId))
                .GroupBy(metadata => metadata.NodeId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().gameObject, StringComparer.Ordinal);

            var animated = dataById.Values.Where(HasTracks).ToArray();
            if (animated.Length == 0) return;
            var opacityNodes = animated.Where(node => HasTrack(node, "OPACITY")).ToArray();
            var opacityCounts = dataById.Values.ToDictionary(
                node => node.id,
                node => opacityNodes.Count(candidate => IsDescendantOrSelf(candidate, node, parentById)),
                StringComparer.Ordinal);
            var plans = new Dictionary<string, Plan>(StringComparer.Ordinal);

            foreach (var node in animated)
            {
                if (!goById.TryGetValue(node.id, out var target)) continue;
                foreach (var track in node.animationTracks.Where(IsUsableTrack))
                {
                    var ownerData = IsProperty(track, "OPACITY")
                        ? ResolveOpacityOwner(node, frame, parentById, opacityCounts)
                        : node;
                    // The selected frame is a timeline container, never an
                    // animation owner. Unrelated root-level tracks remain local.
                    if (ownerData == null || ownerData == frame) ownerData = node;
                    if (!goById.TryGetValue(ownerData.id, out var owner) || owner == root && ownerData != node)
                    {
                        ownerData = node;
                        owner = target;
                    }
                    if (!plans.TryGetValue(ownerData.id, out var plan))
                    {
                        plan = new Plan { ownerData = ownerData, owner = owner };
                        plans.Add(ownerData.id, plan);
                    }
                    plan.duration = Mathf.Max(plan.duration, Mathf.Max(node.animationDuration,
                        track.keyframes.Max(key => key.time)));
                    plan.bindings.Add(new Binding { data = node, target = target, track = track });
                }
            }

            foreach (var plan in plans.Values)
                BuildPlan(root, frame, plan, importScale);
        }

        private static void BuildPlan(GameObject root, FigmaNodeData frame, Plan plan, Vector2 scale)
        {
            if (plan.owner == null || plan.owner == root || plan.bindings.Count == 0) return;
            var duration = Mathf.Max(1f / 60f, plan.duration);
            var clip = GetOrCreateClip(frame, plan.ownerData);
            clip.ClearCurves();
            clip.legacy = true;
            clip.frameRate = 60f;
            clip.wrapMode = WrapMode.Loop;

            foreach (var binding in plan.bindings)
            {
                var path = AnimationUtility.CalculateTransformPath(binding.target.transform,
                    plan.owner.transform);
                AddCurve(clip, path, binding, duration, scale);
            }

            EditorUtility.SetDirty(clip);
            var animation = GetOrAddComponent<Animation>(plan.owner, "Create Figma Animation");
            var oldClips = animation.Cast<AnimationState>().Select(state => state.clip).Where(item => item != null)
                .Distinct().ToArray();
            foreach (var oldClip in oldClips) animation.RemoveClip(oldClip);
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            animation.playAutomatically = true;
            animation.wrapMode = WrapMode.Loop;
            animation.enabled = true;
            EditorUtility.SetDirty(animation);
        }

        private static void AddCurve(AnimationClip clip, string path, Binding binding, float duration,
            Vector2 scale)
        {
            var property = (binding.track.property ?? string.Empty).ToUpperInvariant();
            Type componentType;
            string unityProperty;
            Func<float, float> convert;
            switch (property)
            {
                case "TRANSLATION_X":
                {
                    var baseline = binding.target.GetComponent<RectTransform>().anchoredPosition.x;
                    componentType = typeof(RectTransform);
                    unityProperty = "m_AnchoredPosition.x";
                    convert = value => baseline + value * scale.x;
                    break;
                }
                case "TRANSLATION_Y":
                {
                    var baseline = binding.target.GetComponent<RectTransform>().anchoredPosition.y;
                    componentType = typeof(RectTransform);
                    unityProperty = "m_AnchoredPosition.y";
                    convert = value => baseline - value * scale.y;
                    break;
                }
                case "SCALE_X":
                {
                    var sign = Mathf.Sign(binding.target.transform.localScale.x);
                    componentType = typeof(Transform);
                    unityProperty = "m_LocalScale.x";
                    convert = value => sign * value;
                    break;
                }
                case "SCALE_Y":
                {
                    var sign = Mathf.Sign(binding.target.transform.localScale.y);
                    componentType = typeof(Transform);
                    unityProperty = "m_LocalScale.y";
                    convert = value => sign * value;
                    break;
                }
                case "ROTATION":
                {
                    var baseline = binding.target.GetComponent<RectTransform>().localEulerAngles.z;
                    componentType = typeof(RectTransform);
                    unityProperty = "localEulerAnglesRaw.z";
                    convert = value => baseline - value;
                    break;
                }
                case "OPACITY":
                {
                    var group = GetOrAddComponent<CanvasGroup>(binding.target,
                        "Create Figma Animation CanvasGroup");
                    group.alpha = Mathf.Clamp01(binding.track.keyframes[0].value);
                    componentType = typeof(CanvasGroup);
                    unityProperty = "m_Alpha";
                    convert = Mathf.Clamp01;
                    break;
                }
                default:
                    return;
            }

            var curve = CreateCurve(binding.track.keyframes, duration, convert);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(path, componentType, unityProperty), curve);
        }

        private static T GetOrAddComponent<T>(GameObject target, string undoName) where T : Component
        {
            if (target == null) throw new MissingReferenceException("Animation target was destroyed.");
            var component = target.GetComponent<T>();
            // Do not use ?? with UnityEngine.Object: a destroyed native component
            // can retain a non-null managed wrapper and bypass the fallback.
            if (component != null) return component;
            component = target.AddComponent<T>();
            if (component == null)
                throw new MissingComponentException($"Could not add {typeof(T).Name} to '{target.name}'.");
            Undo.RegisterCreatedObjectUndo(component, undoName);
            return component;
        }

        private static AnimationCurve CreateCurve(FigmaAnimationKeyframeData[] source, float duration,
            Func<float, float> convert)
        {
            var ordered = source.Where(key => key != null).OrderBy(key => key.time).ToList();
            if (ordered.Count == 0) return new AnimationCurve();
            var keys = ordered.Select(key => new Keyframe(key.time, convert(key.value))).ToList();
            if (keys[keys.Count - 1].time < duration - 0.0001f)
                keys.Add(new Keyframe(duration, keys[keys.Count - 1].value));
            var curve = new AnimationCurve(keys.ToArray());

            for (var right = 1; right < ordered.Count; right++)
                ApplySegmentEasing(curve, right - 1, right, ordered[right]);
            if (curve.length > ordered.Count)
            {
                AnimationUtility.SetKeyRightTangentMode(curve, ordered.Count - 1,
                    AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyLeftTangentMode(curve, ordered.Count,
                    AnimationUtility.TangentMode.Constant);
            }
            return curve;
        }

        private static void ApplySegmentEasing(AnimationCurve curve, int leftIndex, int rightIndex,
            FigmaAnimationKeyframeData destination)
        {
            var easing = (destination.easing ?? string.Empty).ToUpperInvariant();
            if (easing == "HOLD")
            {
                AnimationUtility.SetKeyRightTangentMode(curve, leftIndex,
                    AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyLeftTangentMode(curve, rightIndex,
                    AnimationUtility.TangentMode.Constant);
                return;
            }

            var left = curve.keys[leftIndex];
            var right = curve.keys[rightIndex];
            var dt = Mathf.Max(0.00001f, right.time - left.time);
            var dv = right.value - left.value;
            if (easing == "LINEAR")
            {
                var slope = dv / dt;
                left.outTangent = slope;
                right.inTangent = slope;
            }
            else
            {
                var x1 = Mathf.Clamp01(destination.x1);
                var y1 = Mathf.Clamp01(destination.y1);
                var x2 = Mathf.Clamp01(destination.x2);
                var y2 = Mathf.Clamp01(destination.y2);
                left.outTangent = x1 > 0.00001f ? y1 / x1 * dv / dt : 0f;
                right.inTangent = 1f - x2 > 0.00001f
                    ? (1f - y2) / (1f - x2) * dv / dt
                    : 0f;
                left.outWeight = Mathf.Clamp(x1, 0.0001f, 0.9999f);
                right.inWeight = Mathf.Clamp(1f - x2, 0.0001f, 0.9999f);
                left.weightedMode |= WeightedMode.Out;
                right.weightedMode |= WeightedMode.In;
            }
            curve.MoveKey(leftIndex, left);
            curve.MoveKey(rightIndex, right);
            AnimationUtility.SetKeyRightTangentMode(curve, leftIndex, AnimationUtility.TangentMode.Free);
            AnimationUtility.SetKeyLeftTangentMode(curve, rightIndex, AnimationUtility.TangentMode.Free);
        }

        private static AnimationClip GetOrCreateClip(FigmaNodeData frame, FigmaNodeData owner)
        {
            var baseFolder = (FigmaBridgeSettings.AssetFolder ?? FigmaBridgeSettings.DefaultAssetFolder)
                .Replace('\\', '/').TrimEnd('/');
            var folder = baseFolder + "/Animations";
            EnsureFolder(folder);
            var path = $"{folder}/figma_anim_{Sanitize(frame.id)}_{Sanitize(owner.id)}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;
            clip = new AnimationClip { name = Sanitize(owner.name) + " Animation", legacy = true };
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "animation";
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) || character == ':'
                ? '_'
                : character).ToArray()).Trim();
        }

        private static FigmaNodeData ResolveOpacityOwner(FigmaNodeData node, FigmaNodeData root,
            IReadOnlyDictionary<string, FigmaNodeData> parents, IReadOnlyDictionary<string, int> counts)
        {
            if (!parents.TryGetValue(node.id, out var parent)) return node;
            while (parent != null && parent != root)
            {
                if (counts.TryGetValue(parent.id, out var count) && count >= 2) return parent;
                if (!parents.TryGetValue(parent.id, out parent)) break;
            }
            return node;
        }

        private static bool IsDescendantOrSelf(FigmaNodeData node, FigmaNodeData ancestor,
            IReadOnlyDictionary<string, FigmaNodeData> parents)
        {
            var current = node;
            while (current != null)
            {
                if (current == ancestor) return true;
                if (!parents.TryGetValue(current.id, out current)) break;
            }
            return false;
        }

        private static void Index(FigmaNodeData node, FigmaNodeData parent,
            IDictionary<string, FigmaNodeData> nodes, IDictionary<string, FigmaNodeData> parents)
        {
            if (node == null || string.IsNullOrEmpty(node.id)) return;
            nodes[node.id] = node;
            if (parent != null) parents[node.id] = parent;
            if (node.children == null) return;
            foreach (var child in node.children) Index(child, node, nodes, parents);
        }

        private static bool HasTracks(FigmaNodeData node)
        {
            return node?.animationTracks != null && node.animationTracks.Any(IsUsableTrack);
        }

        private static bool HasTrack(FigmaNodeData node, string property)
        {
            return node?.animationTracks != null && node.animationTracks.Any(track =>
                IsUsableTrack(track) && IsProperty(track, property));
        }

        private static bool IsUsableTrack(FigmaAnimationTrackData track)
        {
            return track?.keyframes != null && track.keyframes.Length > 0;
        }

        private static bool IsProperty(FigmaAnimationTrackData track, string property)
        {
            return string.Equals(track?.property, property, StringComparison.OrdinalIgnoreCase);
        }
    }
}
