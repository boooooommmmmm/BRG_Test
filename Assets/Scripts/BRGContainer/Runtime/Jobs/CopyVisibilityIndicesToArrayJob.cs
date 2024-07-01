namespace BRGContainer.Runtime
{
    using System;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine.Rendering;

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
    internal struct CopyVisibilityIndicesToArrayJob : IJobFor
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<BatchLODGroup> BatchLODGroups;

        [ReadOnly] public NativeArray<BatchGroupDrawRange> DrawRangesData;

        [NativeDisableUnsafePtrRestriction] public unsafe BatchCullingOutputDrawCommands* OutputDrawCommands;

        public unsafe void Execute(int index)
        {
            var drawRangeData = DrawRangesData[index];
            if (drawRangeData.Count == 0)
                return; // there is no any visible batches

            // var batchGroup = BatchGroups[index];
            var batchLODGroup = BatchLODGroups[index];
            for (uint lodIndex = 0u; lodIndex < batchLODGroup.LODCount; lodIndex++)
            {
                var batchLOD = batchLODGroup[lodIndex];
                // var windowCount = batchGroup.GetWindowCount();
                var windowCount = 1;
                var visibleOffset = drawRangeData.VisibleIndexOffset;
                var visibleIndices = batchLOD.VisibleArrayPtr();
                var visibleCount = batchLOD.VisibleCount;

                var batchStartIndex = drawRangeData.BatchIndex;
                // for (var i = 0; i < windowCount; i++)
                // {
                //     var batchIndex = batchStartIndex + i;
                //     var visibleCountPerBatch = VisibleCountPerBatch[batchIndex];
                //
                //
                //     visibleOffset += visibleCountPerBatch;
                // }
                
                if (visibleCount == 0) // there is no any visible instances for this batch
                    continue;

                for (int subMeshIndex = 0; subMeshIndex < batchLOD.SubMeshCount; subMeshIndex++)
                {
                    UnsafeUtility.MemCpy((void*)((IntPtr)OutputDrawCommands->visibleInstances + (visibleOffset + subMeshIndex) * UnsafeUtility.SizeOf<int>()), visibleIndices, visibleCount * UnsafeUtility.SizeOf<int>());
                }
            }
        }
    }
}