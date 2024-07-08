using UnityEngine;

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
    internal struct AllocateOutputDrawCommandsJob : IJob
    {
        [NativeDisableUnsafePtrRestriction]
        public unsafe BatchCullingOutputDrawCommands* OutputDrawCommands;
        [ReadOnly] public NativeArray<int> Counters;

        [ReadOnly] public int TotalBatchCount;
        [ReadOnly] public int MaxVisibleCount; 
        
        public unsafe void Execute()
        {
            var visibleCount = Counters[0];
            var drawRangesCount = Counters[1];
            var drawCommandCount = Counters[2];
            
            var maxVisibleCount = MaxVisibleCount;
            OutputDrawCommands->visibleInstanceCount = visibleCount;
            OutputDrawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>() * maxVisibleCount, UnsafeUtility.AlignOf<int>(), Allocator.TempJob);
            // UnsafeUtility.MemClear(OutputDrawCommands->visibleInstances, UnsafeUtility.SizeOf<int>() * maxVisibleCount); // no need memory clear

            // drawRangesCount = TotalBatchCount * (int)BRGConstants.MaxLODCount;
            OutputDrawCommands->drawRangeCount = drawRangesCount;
            OutputDrawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>() * drawRangesCount,
                UnsafeUtility.AlignOf<BatchDrawRange>(), Allocator.TempJob);
            
            // drawCommandCount = TotalBatchCount * (int)BRGConstants.MaxLODCount;
            OutputDrawCommands->drawCommandCount = drawCommandCount;
            OutputDrawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>() * drawCommandCount,
                UnsafeUtility.AlignOf<BatchDrawCommand>(), Allocator.TempJob);
            
            /*
            //*********fake draw range data*********
            OutputDrawCommands->drawRangeCount = 1;
            OutputDrawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>() * 1, UnsafeUtility.AlignOf<BatchDrawRange>(), Allocator.TempJob);
            RendererDescription rendererDescription = new RendererDescription(ShadowCastingMode.On, true, false, 1, 0, MotionVectorGenerationMode.Camera);
            var drawRange = new BatchDrawRange
            {
                drawCommandsBegin = (uint) (0),
                drawCommandsCount = (uint) drawCommandCount,
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
            OutputDrawCommands->drawRanges[0] = drawRange;
            */
            
            /*
            // fake draw command data
            OutputDrawCommands->drawCommandCount = 1;
            OutputDrawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>() * 1, UnsafeUtility.AlignOf<BatchDrawCommand>(), Allocator.TempJob);
            var batchDrawCommand = new BatchDrawCommand
            {
                visibleOffset = (uint)0,
                visibleCount = (uint)1,
                batchID = new BatchID(),
                materialID = new BatchMaterialID(),
                meshID = new BatchMeshID(),
                submeshIndex = (ushort)0,
                splitVisibilityMask = 0xff,
                flags = 0,
                sortingPosition = 0
            };
            OutputDrawCommands->drawCommands[0] = batchDrawCommand;
            */
        }
    }
}