using System;

namespace BRGContainer.Runtime
{
    using System.Runtime.InteropServices;
    using System.Threading;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
    internal struct CullingBatchInstancesJob : IJobParallelFor //IJobFilter
    {
        [ReadOnly, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction]
        public NativeArray<Plane> CullingPlanes;

        [ReadOnly] public int BatchGroupIndex;

        [NativeDisableUnsafePtrRestriction, NativeDisableParallelForRestriction]
        public unsafe PackedMatrix* ObjectToWorldPtr; //sven test

        [WriteOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        public NativeArray<int> VisibleInstanceCount;

        [WriteOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction] public NativeArray<int> VisibleIndices;

        public int DataOffset;

        //@TODO: need AABB
        
        public static float3 size = new float3(1, 1, 1);

        public static int sizeOfPackedMatrix = UnsafeUtility.SizeOf<PackedMatrix>();

        public unsafe void Execute(int index)
        {
            var matrix = ObjectToWorldPtr[index];
            var pos = matrix.GetPosition();

            //@TODO: temp code
            HISMAABB aabb = new HISMAABB()
            {
                Min = pos - size,
                Max = pos + size,
            };

            for (var i = 0; i < CullingPlanes.Length; i++)
            {
                var plane = CullingPlanes[i];
                var normal = plane.normal;
                var distance = math.dot(normal, aabb.Center) + plane.distance;
                var radius = math.dot(aabb.Extents, math.abs(normal));

                if (distance + radius <= 0)
                    return;
            }

            // sven test
            int count = Interlocked.Increment(ref UnsafeUtility.ArrayElementAsRef<int>(VisibleInstanceCount.GetUnsafePtr(), BatchGroupIndex));
            int offset = BatchGroupIndex * 20; //sven test
            VisibleIndices[offset + count - 1] = index;

            //sven test
            // ref BatchInstanceData instanceIndices = ref UnsafeUtility.ArrayElementAsRef<BatchInstanceData>(InstanceDataPerBatch.GetUnsafePtr(), BatchGroupIndex);
            // int count = Interlocked.Increment(ref instanceIndices.VisibleInstanceCount);
            // // UnsafeUtility.WriteArrayElement(instanceIndices.Indices, count - 1, index);
            // instanceIndices.Indices[count - 1] = index;
        }
    }
}