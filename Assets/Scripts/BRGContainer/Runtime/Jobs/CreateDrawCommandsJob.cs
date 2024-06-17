namespace BRGContainer.Runtime
{
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine;
    using UnityEngine.Rendering;

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
    internal struct CreateDrawCommandsJob : IJobFor
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<BatchGroup> BatchGroups;

        [ReadOnly] public NativeArray<BatchGroupDrawRange> DrawRangeData;
        [ReadOnly] public NativeArray<int> VisibleCountPerBatch;
        // [ReadOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction] public NativeArray<BatchInstanceData> InstanceDataPerBatch;

        [NativeDisableUnsafePtrRestriction] public unsafe BatchCullingOutputDrawCommands* OutputDrawCommands;

        public unsafe void Execute(int index)
        {
            var drawRangeData = DrawRangeData[index];
            if (drawRangeData.Count == 0)
                return;

            var batchGroup = BatchGroups[index];
            var subBatchCount = batchGroup.GetWindowCount();

            var batchStartIndex = drawRangeData.BatchIndex;
            var drawCommandIndex = drawRangeData.Begin;
            var visibleOffset = drawRangeData.VisibleIndexOffset;
            for (var i = 0; i < subBatchCount; i++)
            {
                var batchIndex = batchStartIndex + i;

                var visibleCountPerBatch = VisibleCountPerBatch[batchIndex];
                if (visibleCountPerBatch == 0) // there is no any visible instances for this batch
                    continue;
                var instanceCount = visibleCountPerBatch; // assume only has one subbatch

                var rendererData = batchGroup.BatchRendererData;
                var batchDrawCommand = new BatchDrawCommand
                {
                    visibleOffset = (uint)visibleOffset,
                    visibleCount = (uint)instanceCount,
                    batchID = batchGroup[i],
                    materialID = rendererData.MaterialID,
                    meshID = rendererData.MeshID,
                    submeshIndex = (ushort)rendererData.SubMeshIndex,
                    splitVisibilityMask = 0xff,
                    flags = 0,
                    sortingPosition = 0
                };

                OutputDrawCommands->drawCommands[drawCommandIndex] = batchDrawCommand;
                drawCommandIndex++;
                visibleOffset += instanceCount;
            }
        }
    }
}