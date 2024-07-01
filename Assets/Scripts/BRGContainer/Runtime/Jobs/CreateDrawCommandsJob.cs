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
        public NativeArray<BatchLODGroup> BatchLODGroups;

        [ReadOnly] public NativeArray<BatchGroupDrawRange> DrawRangeData;

        [NativeDisableUnsafePtrRestriction] public unsafe BatchCullingOutputDrawCommands* OutputDrawCommands;

        public unsafe void Execute(int index)
        {
            var drawRangeData = DrawRangeData[index];
            if (drawRangeData.Count == 0)
                return;

            var batchLODGroup = BatchLODGroups[index];
            var subBatchCount = batchLODGroup.GetWindowCount();
            subBatchCount = 1;

            var batchStartIndex = drawRangeData.BatchIndex;
            var drawCommandIndex = drawRangeData.Begin;
            var visibleOffset = drawRangeData.VisibleIndexOffset;

            for (uint lodIndex = 0; lodIndex < batchLODGroup.LODCount; lodIndex++)
            {
                var batchLOD = batchLODGroup[lodIndex];
                var visibleCountPreLOD = batchLOD.VisibleCount;
                if (visibleCountPreLOD == 0) // there is no any visible instances for this batch
                    continue;
                var instanceCount = visibleCountPreLOD; // assume only has one subBatch

                for (uint subMeshIndex = 0; subBatchCount < batchLOD.SubMeshCount; subMeshIndex++)
                {
                    for (var i = 0; i < subBatchCount; i++)
                    {
                        // var batchIndex = batchStartIndex + i;
                        
                        // drawCommandIndex++;
                        // visibleOffset += instanceCount;
                    }

                    var batchGroup = batchLOD[subMeshIndex];
                    var batchDrawCommand = new BatchDrawCommand
                    {
                        visibleOffset = (uint)0,
                        visibleCount = (uint)visibleCountPreLOD,
                        batchID = batchGroup[0],
                        materialID = batchGroup.BatchRendererData.MaterialID,
                        meshID = batchGroup.BatchRendererData.MeshID,
                        submeshIndex = (ushort)batchGroup.BatchRendererData.SubMeshIndex,
                        splitVisibilityMask = 0xff,
                        flags = 0,
                        sortingPosition = 0
                    };

                    OutputDrawCommands->drawCommands[drawCommandIndex] = batchDrawCommand;
                    drawCommandIndex++;
                }
            }
        }
    }
}