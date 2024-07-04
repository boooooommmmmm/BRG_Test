namespace BRGContainer.Runtime
{
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine.Rendering;

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
    internal struct CreateDrawRangesJob : IJobFor
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<BatchLODGroup> BatchLODGroups;
        
        [NativeDisableUnsafePtrRestriction]
        public unsafe BatchCullingOutputDrawCommands* OutputDrawCommands;
        
        public unsafe void Execute(int index)
        {
            ref var batchLODGroup = ref UnsafeUtility.ArrayElementAsRef<BatchLODGroup>(BatchLODGroups.GetUnsafePtr(), index);
            
            for (uint lodIndex = 0; lodIndex < batchLODGroup.LODCount; lodIndex++)
            {
                ref var batchLOD = ref UnsafeUtility.ArrayElementAsRef<BatchLOD>(batchLODGroup.m_BatchLODs, (int)lodIndex);

                if (!batchLOD.IsInitialied || batchLOD.VisibleCount == 0)
                    continue;

                int drawRangeBeginIndex = batchLOD.m_DrawBatchIndex;
                for (uint subMeshIndex = 0; subMeshIndex < batchLOD.SubMeshCount; subMeshIndex++)
                {
                    var rendererDescription = batchLOD.RendererDescription;
                    var drawRange = new BatchDrawRange
                    {
                        drawCommandsBegin = (uint) (drawRangeBeginIndex + (int)subMeshIndex),
                        drawCommandsCount = (uint) 1,
                        filterSettings = new BatchFilterSettings
                        {
                            renderingLayerMask = rendererDescription.RenderingLayerMask,
                            layer = rendererDescription.Layer,
                            motionMode = rendererDescription.MotionMode,
                            shadowCastingMode = rendererDescription.ShadowCastingMode,
                            receiveShadows = rendererDescription.ReceiveShadows,
                            staticShadowCaster = rendererDescription.StaticShadowCaster,
                            allDepthSorted = false
                        }
                    };
                    OutputDrawCommands->drawRanges[drawRangeBeginIndex + (int)subMeshIndex] = drawRange;
                }
            }
        }
    }
}