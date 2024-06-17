namespace BRGContainer.Runtime
{
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
    internal struct CopyVisibleIndicesToMapJob : IJob
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        public unsafe NativeArray<int> VisibleInstanceCount;

        [ReadOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        public NativeArray<int> VisibleIndices;

        [ReadOnly] public int BatchGroupIndex;

        [WriteOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        public NativeArray<BatchInstanceData> InstanceDataPerBatch;

        [WriteOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<int> VisibleCountPerBatch;

        public int BatchIndex;
        
        public unsafe void Execute()
        {
            // if (VisibleIndices.Length == 0)
            //     return;
            //
            // BatchInstanceData instanceIndices = default;
            // instanceIndices.Indices = (int*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<int>() * VisibleIndices.Length,
            //     UnsafeUtility.AlignOf<int>(), Allocator.TempJob, 0);
            // UnsafeUtility.MemCpy(instanceIndices.Indices, VisibleIndices.GetUnsafeReadOnlyPtr(), UnsafeUtility.SizeOf<int>() * VisibleIndices.Length);
            //
            // instanceIndices.InstanceCount = VisibleInstanceCount[0];
            //
            // InstanceDataPerBatch[BatchIndex] = instanceIndices;
            // VisibleCountPerBatch[BatchIndex] = VisibleIndices.Length;
            // // VisibleCountPerBatch[BatchIndex] = InstanceCount;


            //Sven test
            if (VisibleIndices.Length == 0)
                return;

            ref BatchInstanceData instanceIndices = ref UnsafeUtility.ArrayElementAsRef<BatchInstanceData>(InstanceDataPerBatch.GetUnsafePtr(), BatchGroupIndex);
            
            int offset = BatchIndex * 20;
            int size = 20;
            UnsafeUtility.MemCpy(instanceIndices.Indices, (int*)VisibleIndices.GetUnsafeReadOnlyPtr() + offset, UnsafeUtility.SizeOf<int>() * size);

            instanceIndices.InstanceCount = VisibleInstanceCount[0];
            instanceIndices.InstanceCount = 20;
            VisibleCountPerBatch[BatchIndex] = 20; //temp test
            
        }
    }
}