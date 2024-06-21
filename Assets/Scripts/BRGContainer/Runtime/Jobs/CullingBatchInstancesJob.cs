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
        [ReadOnly, NativeDisableUnsafePtrRestriction] public unsafe float3* Positions;
        [ReadOnly] public float3 Extents;

        [WriteOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        public NativeArray<int> VisibleInstanceCount;

        // [WriteOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        // public NativeArray<int> VisibleIndices;
        [WriteOnly, NativeDisableUnsafePtrRestriction]
        public unsafe int* VisibleIndices;

        public int DataOffset;

        private const int PLANE_COUNT = 6;

        public unsafe void Execute(int index)
        {
            // var matrix = ObjectToWorldPtr[index];
            float3 pos = Positions[index];

            for (var i = 0; i < PLANE_COUNT; i++)
            {
                var plane = CullingPlanes[i];
                var normal = plane.normal;
                // var distance = math.dot(normal, aabb.Center) + plane.distance;
                // var radius = math.dot(aabb.Extents, math.abs(normal));
                var distance = math.dot(normal, pos) + plane.distance;
                var radius = math.dot(Extents, math.abs(normal));

                if (distance + radius <= 0)
                    return;
            }

            int count = Interlocked.Increment(ref UnsafeUtility.ArrayElementAsRef<int>(VisibleInstanceCount.GetUnsafePtr(), BatchGroupIndex));
            VisibleIndices[count - 1] = index;
        }
    }
}