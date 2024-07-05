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
        [ReadOnly] public int BatchGroupIndex;

        [WriteOnly, NativeDisableContainerSafetyRestriction]
        public BatchLODGroup BatchLODGroup;

        public unsafe void Execute(int index)
        {
            for (int lodIndex = 0; lodIndex < (int)BatchLODGroup.LODCount; lodIndex++)
            {
                BatchLOD* batchLOD = BatchLODGroup.GetByRef(lodIndex);
                Interlocked.Exchange(ref UnsafeUtility.AsRef<int>((*batchLOD).visibleCount) , 0);
                // no need clear visible array, will be overwrote in culling phase.
            }
        }
    }
}