namespace BRGContainer.Runtime
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
    internal struct ComputeDrawCountersLODGroupJob : IJobParallelFor
    {
        [WriteOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<int> DrawCounters; // 0 - is visible count, 1 - is draw ranges count, 2 - is draw command count

        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<BatchLODGroup> BatchLODGroups;

        public unsafe void Execute(int index)
        {
            ref var visibleCountRef = ref UnsafeUtility.ArrayElementAsRef<int>(DrawCounters.GetUnsafePtr(), 0);
            ref var drawRangesCountRef = ref UnsafeUtility.ArrayElementAsRef<int>(DrawCounters.GetUnsafePtr(), 1);
            ref var drawCommandCountRef = ref UnsafeUtility.ArrayElementAsRef<int>(DrawCounters.GetUnsafePtr(), 2);
            
            ref var batchLODGroup = ref UnsafeUtility.ArrayElementAsRef<BatchLODGroup>(BatchLODGroups.GetUnsafePtr(), index);

            int lodCount = (int)batchLODGroup.LODCount;
            var validBatchCount = 0;
            var visibleCount = 0;

            for (uint lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                // for loop for subBatch/window
                ref var batchLOD = ref UnsafeUtility.ArrayElementAsRef<BatchLOD>(batchLODGroup.m_BatchLODs, (int)lodIndex);
                int visibleCountLOD = batchLOD.VisibleCount;
                
                if (visibleCountLOD == 0) // there is no any visible instances for this LOD
                {
                    continue;
                }

                int subMeshCount = (int)batchLOD.SubMeshCount;
                validBatchCount = math.select(validBatchCount, validBatchCount + subMeshCount, visibleCountLOD > 0);
                visibleCount += (visibleCountLOD * subMeshCount);
                // break;
            }

            Interlocked.Add(ref visibleCountRef, visibleCount);
            Interlocked.Add(ref drawRangesCountRef, validBatchCount);
            int batchOffset = Interlocked.Add(ref drawCommandCountRef, validBatchCount);
            batchOffset -= validBatchCount;
            
            //calculate index offset
            int batchDrawOffset = batchOffset;
            for (uint lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                ref var batchLOD = ref UnsafeUtility.ArrayElementAsRef<BatchLOD>(batchLODGroup.m_BatchLODs, (int)lodIndex);
                int visibleCountLOD = batchLOD.VisibleCount;
                int subMeshCount = (int)batchLOD.SubMeshCount;
                
                if (visibleCountLOD == 0) // there is no any visible instances for this LOD
                {
                    continue;
                }

                batchLOD.m_DrawBatchIndex = batchDrawOffset;
                batchLOD.m_VisibleIndexStartIndex = batchOffset * 50 * (int)BRGConstants.MaxLODCount + (int)lodIndex * 50 ; //@TODO 
                batchDrawOffset += subMeshCount;
                // break;
            }
        }
    }
}