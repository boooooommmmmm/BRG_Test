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

        [NativeDisableUnsafePtrRestriction] public unsafe BatchCullingOutputDrawCommands* OutputDrawCommands;

        public unsafe void Execute(int index)
        {
            ref var batchLODGroup = ref UnsafeUtility.ArrayElementAsRef<BatchLODGroup>(BatchLODGroups.GetUnsafePtr(), index);

            for (uint lodIndex = 0; lodIndex < batchLODGroup.LODCount; lodIndex++)
            {
                ref var batchLOD = ref UnsafeUtility.ArrayElementAsRef<BatchLOD>(batchLODGroup.m_BatchLODs, (int)lodIndex);

                if (batchLOD.VisibleCount == 0)
                {
                    continue;
                }

                int drawRangeBeginIndex = batchLOD.m_DrawBatchIndex;
                for (uint subMeshIndex = 0; subMeshIndex < batchLOD.SubMeshCount; subMeshIndex++)
                {
                    ref var batchGroup = ref UnsafeUtility.ArrayElementAsRef<BatchGroup>(batchLOD.m_BatchGroups, (int)subMeshIndex);
                    var batchDrawCommand = new BatchDrawCommand
                    {
                        visibleOffset = (uint)batchLOD.m_VisibleInstanceIndexStartIndex,
                        visibleCount = (uint)batchLOD.VisibleCount,
                        batchID = batchGroup[0],
                        materialID = batchGroup.BatchRendererData.MaterialID,
                        meshID = batchGroup.BatchRendererData.MeshID,
                        submeshIndex = (ushort)batchGroup.BatchRendererData.SubMeshIndex,
                        splitVisibilityMask = 0xff,
                        flags = 0,
                        sortingPosition = 0
                    };

                    OutputDrawCommands->drawCommands[drawRangeBeginIndex + (int)subMeshIndex] = batchDrawCommand;
                }
            }
        }
    }
}