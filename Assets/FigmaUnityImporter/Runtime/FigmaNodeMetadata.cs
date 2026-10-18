using System;
using UnityEngine;

namespace Stanislaw.FigmaUnityImporter
{
    [DisallowMultipleComponent]
    public sealed class FigmaNodeMetadata : MonoBehaviour
    {
        [SerializeField] private string nodeId;
        [SerializeField] private string nodeType;
        [SerializeField] private string sourceName;
        [SerializeField] private FigmaTextShadowParameters[] textShadows = Array.Empty<FigmaTextShadowParameters>();

        public string NodeId => nodeId;
        public FigmaTextShadowParameters[] TextShadows => textShadows;

        public void Set(string id, string type, string figmaName)
        {
            nodeId = id;
            nodeType = type;
            sourceName = figmaName;
        }

        public void SetTextShadows(FigmaTextShadowParameters[] shadows)
        {
            textShadows = shadows ?? Array.Empty<FigmaTextShadowParameters>();
        }
    }

    [Serializable]
    public struct FigmaTextShadowParameters
    {
        public Color color;
        public Vector2 offset;
        public float blurRadius;
        public float spread;
        public string blendMode;

        public FigmaTextShadowParameters(Color color, Vector2 offset, float blurRadius, float spread, string blendMode)
        {
            this.color = color;
            this.offset = offset;
            this.blurRadius = blurRadius;
            this.spread = spread;
            this.blendMode = blendMode;
        }
    }

}
