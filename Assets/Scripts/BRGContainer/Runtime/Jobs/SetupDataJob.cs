namespace BRGContainer.Runtime
{
    using System;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine.Rendering;
    using System.Threading;

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
    internal struct SetupDataJob : IJobParallelFor
    {
        // [ReadOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        // public NativeArray<BatchInstanceData> InstanceDataPerBatch;

        [ReadOnly] public int BatchGroupIndex;
        
        [WriteOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        public NativeArray<int> VisibleInstanceCount;

        public unsafe void Execute(int index)
        {
            // ref BatchInstanceData instanceIndices = ref UnsafeUtility.ArrayElementAsRef<BatchInstanceData>(InstanceDataPerBatch.GetUnsafePtr(), BatchGroupIndex);
            // instanceIndices.VisibleInstanceCount = 0;

            VisibleInstanceCount[BatchGroupIndex] = 0;
        }
    }
}