namespace BRGContainer.Runtime
{
    using System;
    using System.Runtime.InteropServices;
    using UnityEngine;
    using UnityEngine.Rendering;

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct RendererDescription : IEquatable<RendererDescription>
    {
        public readonly ShadowCastingMode ShadowCastingMode;
        public readonly bool ReceiveShadows;
        public readonly bool StaticShadowCaster;
        public readonly uint RenderingLayerMask;
        public readonly byte Layer;
        public readonly MotionVectorGenerationMode MotionMode;

        public RendererDescription(ShadowCastingMode shadowCastingMode, bool receiveShadows, bool staticShadowCaster, uint renderingLayerMask, byte layer, MotionVectorGenerationMode motionMode)
        {
            ShadowCastingMode = shadowCastingMode;
            ReceiveShadows = receiveShadows;
            StaticShadowCaster = staticShadowCaster;
            RenderingLayerMask = renderingLayerMask;
            Layer = layer;
            MotionMode = motionMode;
        }

        public bool Equals(RendererDescription other)
        {
            return ShadowCastingMode == other.ShadowCastingMode && ReceiveShadows == other.ReceiveShadows && StaticShadowCaster == other.StaticShadowCaster && 
                   RenderingLayerMask == other.RenderingLayerMask && Layer == other.Layer && MotionMode == other.MotionMode;
        }

        public override bool Equals(object obj)
        {
            return obj is RendererDescription other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ShadowCastingMode, ReceiveShadows, StaticShadowCaster, RenderingLayerMask, Layer, MotionMode);
        }

        public static bool operator ==(RendererDescription left, RendererDescription right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RendererDescription left, RendererDescription right)
        {
            return !left.Equals(right);
        }
    }
}