using System;

namespace Stanislaw.FigmaUnityImporter.Editor
{
    [Serializable]
    internal sealed class FigmaImportPayload
    {
        public int protocolVersion;
        public string documentName;
        public FigmaNodeData frame;
        public FigmaNodeData[] frames;
    }

    [Serializable]
    internal sealed class FigmaNodeData
    {
        public string id;
        public string name;
        public string type;
        public string renderMode;
        public float x;
        public float y;
        public float width;
        public float height;
        public float rotation;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public string constraintHorizontal;
        public string constraintVertical;
        public string overflowDirection;
        public string layoutMode;
        public float opacity = 1f;
        public bool visible = true;
        public bool interactive;
        public bool clipsContent;
        public bool isMask;
        public bool hasEffects;
        public float cornerRadius;
        public float[] color;
        public string text;
        public string fontFamily;
        public string fontStyle;
        public float fontSize;
        public string textAlignHorizontal;
        public string textAlignVertical;
        public string textAutoResize;
        public float lineHeight;
        public float letterSpacing;
        public FigmaTextShadowData[] textShadows;
        public float animationDuration;
        public string animationTimelineId;
        public FigmaAnimationTrackData[] animationTracks;
        public string imageMime;
        public string imageBase64;
        public string imageScaleMode;
        public float[] imageTint;
        public bool imageIsTintMask;
        public string assetReuseKey;
        public float visualX;
        public float visualY;
        public float visualWidth;
        public float visualHeight;
        public FigmaNodeData[] children;
    }

    [Serializable]
    internal sealed class FigmaAnimationTrackData
    {
        public string property;
        public float baseValue;
        public FigmaAnimationKeyframeData[] keyframes;
    }

    [Serializable]
    internal sealed class FigmaAnimationKeyframeData
    {
        public float time;
        public float value;
        public string easing;
        public float x1;
        public float y1;
        public float x2 = 1f;
        public float y2 = 1f;
    }

    [Serializable]
    internal sealed class FigmaTextShadowData
    {
        public float[] color;
        public float offsetX;
        public float offsetY;
        public float radius;
        public float spread;
        public string blendMode;
    }
}
