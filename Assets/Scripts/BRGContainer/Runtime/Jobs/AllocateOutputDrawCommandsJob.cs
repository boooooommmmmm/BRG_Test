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
            
            // var maxVisibleCount = TotalBatchCount * BRGConstants.DefaultVisibleInstanceIndexCount * (int)BRGConstants.MaxLODCount;
            var maxVisibleCount = MaxVisibleCount;
            OutputDrawCommands->visibleInstanceCount = visibleCount;
            OutputDrawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>() * maxVisibleCount, UnsafeUtility.AlignOf<int>(), Allocator.TempJob);

            // drawRangesCount = TotalBatchCount * (int)BRGConstants.MaxLODCount;
            OutputDrawCommands->drawRangeCount = drawRangesCount;
            OutputDrawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>() * drawRangesCount,
                UnsafeUtility.AlignOf<BatchDrawRange>(), Allocator.TempJob);
            //
            // // drawCommandCount = TotalBatchCount * (int)BRGConstants.MaxLODCount;
            OutputDrawCommands->drawCommandCount = drawCommandCount;
            OutputDrawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>() * drawCommandCount,
                UnsafeUtility.AlignOf<BatchDrawCommand>(), Allocator.TempJob);
        }
    }
}