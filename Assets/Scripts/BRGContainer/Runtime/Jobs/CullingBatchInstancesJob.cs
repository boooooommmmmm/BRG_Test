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

        // [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<PackedMatrix> ObjectToWorld;
        [ReadOnly] public int BatchGroupIndex;

        [NativeDisableUnsafePtrRestriction, NativeDisableParallelForRestriction]
        public unsafe PackedMatrix* ObjectToWorldPtr; //sven test

        [WriteOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        public NativeArray<int> VisibleInstanceCount;

        [WriteOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction] public NativeArray<int> VisibleIndices;

        //sven test
        // [WriteOnly, NativeDisableContainerSafetyRestriction, NativeDisableParallelForRestriction]
        // public NativeArray<BatchInstanceData> InstanceDataPerBatch;

        public int DataOffset;

        //@TODO: need AABB
        // public static AABB aabb = new AABB( ) {Center = float3.zero, Extents = new float3(1,1,1)};

        public static int sizeOfPackedMatrix = UnsafeUtility.SizeOf<PackedMatrix>();

        public unsafe void Execute(int index)
        {
            // var matrix = ObjectToWorld[index + DataOffset];
            //Sven test
            // return;
            var matrix = ObjectToWorldPtr[index];


            //@TODO: temp code
            AABB aabb = new AABB
            {
                Center = float3.zero,
                Extents = Vector3.one
            };
            aabb = AABB.Transform(matrix.fullMatrix, aabb);

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