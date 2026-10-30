using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CodexBarrelAssets
{
    // Uses Unity's supported mesh API. The source FBX remains an ordinary Blender export.
    public sealed class BarrelFillImport : AssetPostprocessor
    {
        internal const string FileStem = "Barrel_Grain_Fitted_v4";
        internal const string StagePrefix = "__FillStage_";

        private sealed class Frame
        {
            public float weight;
            public Vector3[] positions, normals, tangents;
        }

        public override uint GetVersion() { return 1; }

        private void OnPostprocessModel(GameObject model)
        {
            if (!Path.GetFileNameWithoutExtension(assetPath).Equals(FileStem, StringComparison.OrdinalIgnoreCase)) return;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.name.StartsWith("Barrel_Grain", StringComparison.Ordinal)) continue;
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                var frames = new List<Frame>();
                bool foundStages = false;
                for (int index = 0; index < mesh.blendShapeCount; index++)
                {
                    string name = mesh.GetBlendShapeName(index);
                    float weight;
                    int start = name.LastIndexOf(StagePrefix, StringComparison.Ordinal);
                    if (start >= 0)
                    {
                        double encoded;
                        string digits = name.Substring(start + StagePrefix.Length);
                        if (!double.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out encoded))
                            throw new InvalidOperationException("Invalid barrel Fill stage: " + name);
                        weight = (float)(encoded / 10000000.0);
                        foundStages = true;
                    }
                    else if (name == "Fill" || name.EndsWith(".Fill", StringComparison.Ordinal)) weight = 100f;
                    else throw new InvalidOperationException("Unexpected shape on Barrel_Grain: " + name);

                    var frame = new Frame
                    {
                        weight = weight,
                        positions = new Vector3[mesh.vertexCount],
                        normals = new Vector3[mesh.vertexCount],
                        tangents = new Vector3[mesh.vertexCount]
                    };
                    mesh.GetBlendShapeFrameVertices(index, mesh.GetBlendShapeFrameCount(index) - 1,
                        frame.positions, frame.normals, frame.tangents);
                    frames.Add(frame);
                }
                if (!foundStages) continue;
                frames.Sort((a, b) => a.weight.CompareTo(b.weight));
                if (frames.Count < 2 || frames[frames.Count - 1].weight != 100f)
                    throw new InvalidOperationException("Incomplete barrel Fill stages.");

                Vector3[] basis = mesh.vertices;
                Bounds bounds = mesh.bounds;
                mesh.ClearBlendShapes();
                float previous = 0f;
                foreach (Frame frame in frames)
                {
                    if (frame.weight <= previous) throw new InvalidOperationException("Duplicate barrel Fill weights.");
                    mesh.AddBlendShapeFrame("Fill", frame.weight, frame.positions, frame.normals, frame.tangents);
                    for (int i = 0; i < basis.Length; i++) bounds.Encapsulate(basis[i] + frame.positions[i]);
                    previous = frame.weight;
                }
                bounds.Expand(.02f);
                mesh.bounds = bounds;
                renderer.localBounds = bounds;
                Debug.Log("Barrel import ready: one Fill blendshape with " + frames.Count + " stages.");
            }
        }
    }

    // A newly copied Editor script may compile after the FBX's first import.
    [InitializeOnLoad]
    internal static class BarrelFillImportRefresh
    {
        static BarrelFillImportRefresh() { EditorApplication.delayCall += RefreshPending; }

        private static void RefreshPending()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RefreshPending;
                return;
            }
            foreach (string id in AssetDatabase.FindAssets(BarrelFillImport.FileStem + " t:Model"))
            {
                string path = AssetDatabase.GUIDToAssetPath(id);
                if (!Path.GetFileNameWithoutExtension(path).Equals(BarrelFillImport.FileStem, StringComparison.OrdinalIgnoreCase)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) continue;
                bool pending = false;
                foreach (var renderer in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Mesh mesh = renderer.sharedMesh;
                    if (mesh == null) continue;
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                        pending |= mesh.GetBlendShapeName(i).Contains(BarrelFillImport.StagePrefix);
                }
                if (pending) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
