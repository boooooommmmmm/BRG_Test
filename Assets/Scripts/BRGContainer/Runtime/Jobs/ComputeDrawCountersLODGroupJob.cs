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
    internal struct ComputeDrawCountersLODGroupJob : IJob
    {
        [WriteOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<int> DrawCounters; // 0 - is visible count, 1 - is draw ranges count, 2 - is draw command count

        [NativeDisableContainerSafetyRestriction]
        public NativeArray<BatchGroupDrawRange> DrawRangesData;

        // [ReadOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction] public NativeArray<BatchInstanceData> InstanceDataPerBatch;

        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<BatchLODGroup> BatchLODGroups;

        public int BatchGroupIndex;
        public int BatchOffset;

        public unsafe void Execute()
        {
            var batchLODGroup = BatchLODGroups[BatchGroupIndex];
            // var subBatchCount = batchLODGroup.GetWindowCount();
            // subBatchCount = 1; //assume single window 

            int lodCount = (int)batchLODGroup.LODCount;
            var validBatchCount = 0;
            var totalDrawCount = 0;

            for (uint lodIndex = 0; lodIndex < lodCount; lodIndex++)
            {
                // for loop for subBatch/window
                BatchLOD batchLOD = batchLODGroup[lodIndex];
                int visibleCountLOD = batchLOD.VisibleCount;
                
                if (visibleCountLOD == 0) // there is no any visible instances for this LOD
                {
                    continue;
                }
                else
                {
                    int subMeshCount = (int)batchLOD.SubMeshCount;
                    validBatchCount = math.select(validBatchCount, validBatchCount + subMeshCount, visibleCountLOD > 0);
                    totalDrawCount += validBatchCount;
                }
            }
            

            ref var drawRangeDataRef = ref UnsafeUtility.ArrayElementAsRef<BatchGroupDrawRange>(DrawRangesData.GetUnsafePtr(), BatchGroupIndex);
            if (validBatchCount == 0)
            {
                for (var i = BatchGroupIndex + 1; i < BatchLODGroups.Length; i++)
                {
                    ref var nextDrawRangeDataRef = ref UnsafeUtility.ArrayElementAsRef<BatchGroupDrawRange>(DrawRangesData.GetUnsafePtr(), i);

                    Interlocked.Decrement(ref nextDrawRangeDataRef.IndexOffset);
                    Interlocked.Add(ref nextDrawRangeDataRef.BatchIndex, validBatchCount);
                }

                return;
            }

            ref var visibleCountRef = ref UnsafeUtility.ArrayElementAsRef<int>(DrawCounters.GetUnsafePtr(), 0);
            ref var drawRangesCountRef = ref UnsafeUtility.ArrayElementAsRef<int>(DrawCounters.GetUnsafePtr(), 1);
            ref var drawCommandCountRef = ref UnsafeUtility.ArrayElementAsRef<int>(DrawCounters.GetUnsafePtr(), 2);

            Interlocked.Add(ref drawRangesCountRef, validBatchCount);
            Interlocked.Add(ref drawCommandCountRef, validBatchCount);
            Interlocked.Add(ref visibleCountRef, totalDrawCount);

            drawRangeDataRef.Count = validBatchCount;

            for (var i = BatchGroupIndex + 1; i < BatchLODGroups.Length; i++) // prefix sum
            {
                ref var nextDrawRangeDataRef = ref UnsafeUtility.ArrayElementAsRef<BatchGroupDrawRange>(DrawRangesData.GetUnsafePtr(), i);

                Interlocked.Add(ref nextDrawRangeDataRef.Begin, validSubBatchCount);
                Interlocked.Add(ref nextDrawRangeDataRef.VisibleIndexOffset, visibleCountPerBatchGroup);
                Interlocked.Add(ref nextDrawRangeDataRef.BatchIndex, subBatchCount);
            }
        }
    }
}