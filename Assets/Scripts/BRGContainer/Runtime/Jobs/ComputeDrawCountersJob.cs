﻿// namespace BRGContainer.Runtime
// {
//     using System.Runtime.InteropServices;
//     using System.Threading;
//     using Unity.Burst;
//     using Unity.Collections;
//     using Unity.Collections.LowLevel.Unsafe;
//     using Unity.Jobs;
//     using Unity.Mathematics;
//
//     [StructLayout(LayoutKind.Sequential)]
//     [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
//     internal struct ComputeDrawCountersJob : IJob
//     {
//         [WriteOnly, NativeDisableContainerSafetyRestriction]
//         public NativeArray<int> DrawCounters; // 0 - is visible count, 1 - is draw ranges count, 2 - is draw command count
//
//         [NativeDisableContainerSafetyRestriction]
//         public NativeArray<int> VisibleCountPerBatch;
//
//         [NativeDisableContainerSafetyRestriction]
//         public NativeArray<BatchGroupDrawRange> DrawRangesData;
//
//         // [ReadOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction] public NativeArray<BatchInstanceData> InstanceDataPerBatch;
//
//         [ReadOnly, NativeDisableContainerSafetyRestriction]
//         public NativeArray<BatchLODGroup> BatchLODGroups;
//
//         public int BatchGroupIndex;
//         public int BatchOffset;
//
//         public unsafe void Execute()
//         {
//             var batchLODGroup = BatchLODGroups[BatchGroupIndex];
//             var subBatchCount = batchLODGroup.GetWindowCount();
//
//             var validSubBatchCount = 0;
//             var visibleCountPerBatchGroup = 0;
//             for (var i = 0; i < subBatchCount; i++)
//             {
//                 var visibleCountPerBatch = VisibleCountPerBatch[BatchOffset + i];
//                 if (visibleCountPerBatch == 0) // there is no any visible instances for this batch
//                     continue;
//                 visibleCountPerBatchGroup += visibleCountPerBatch;
//
//                 validSubBatchCount = math.select(validSubBatchCount, validSubBatchCount + 1,
//                     visibleCountPerBatch > 0);
//             }
//
//             ref var drawRangeDataRef = ref UnsafeUtility.ArrayElementAsRef<BatchGroupDrawRange>(DrawRangesData.GetUnsafePtr(), BatchGroupIndex);
//             if (validSubBatchCount == 0)
//             {
//                 for (var i = BatchGroupIndex + 1; i < BatchLODGroups.Length; i++)
//                 {
//                     ref var nextDrawRangeDataRef = ref UnsafeUtility.ArrayElementAsRef<BatchGroupDrawRange>(DrawRangesData.GetUnsafePtr(), i);
//
//                     Interlocked.Decrement(ref nextDrawRangeDataRef.IndexOffset);
//                     Interlocked.Add(ref nextDrawRangeDataRef.BatchIndex, subBatchCount);
//                 }
//
//                 return;
//             }
//
//             ref var visibleCountRef = ref UnsafeUtility.ArrayElementAsRef<int>(DrawCounters.GetUnsafePtr(), 0);
//             ref var drawRangesCountRef = ref UnsafeUtility.ArrayElementAsRef<int>(DrawCounters.GetUnsafePtr(), 1);
//             ref var drawCommandCountRef = ref UnsafeUtility.ArrayElementAsRef<int>(DrawCounters.GetUnsafePtr(), 2);
//
//             Interlocked.Increment(ref drawRangesCountRef);
//             Interlocked.Add(ref drawCommandCountRef, validSubBatchCount);
//             Interlocked.Add(ref visibleCountRef, visibleCountPerBatchGroup);
//
//             drawRangeDataRef.Count = validSubBatchCount;
//
//             for (var i = BatchGroupIndex + 1; i < BatchLODGroups.Length; i++) // prefix sum
//             {
//                 ref var nextDrawRangeDataRef = ref UnsafeUtility.ArrayElementAsRef<BatchGroupDrawRange>(DrawRangesData.GetUnsafePtr(), i);
//
//                 Interlocked.Add(ref nextDrawRangeDataRef.Begin, validSubBatchCount);
//                 Interlocked.Add(ref nextDrawRangeDataRef.VisibleIndexOffset, visibleCountPerBatchGroup);
//                 Interlocked.Add(ref nextDrawRangeDataRef.BatchIndex, subBatchCount);
//             }
//         }
//     }
// }