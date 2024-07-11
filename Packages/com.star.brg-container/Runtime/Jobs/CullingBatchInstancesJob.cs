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
        
        [WriteOnly, NativeDisableContainerSafetyRestriction]
        public BatchLODGroup BatchLODGroup;

        [ReadOnly] public int BatchLODGroupIndex;
        // [ReadOnly] public float3 Extents;
        
        [WriteOnly, NativeDisableUnsafePtrRestriction] public unsafe uint* StatePtr;
        
        [WriteOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<int> DrawInstanceIndexData;

        public int DataOffset;

        private const int PLANE_COUNT = 6;

        public unsafe void Execute(int index)
        {
            // uint state = StatePtr[index];
            // int aliveMaskPos = 4;
            // uint aliveMask = state & (1u << aliveMaskPos);
            // bool isAlive = aliveMask > 0u ? true : false;
            bool isAlive = BatchLODGroup.IsActive(index);

            if (!isAlive)
            {
                return;
            }
            
            PackedMatrix matrix = UnsafeUtility.ArrayElementAsRef<PackedMatrix>(BatchLODGroup.DataBuffer, index);
            float3 pos = matrix.GetPosition();

            HISMAABB* aabb = BatchLODGroup.GetAABBPtr(index);
            var extents = aabb->Extents;
            
            for (var i = 0; i < PLANE_COUNT; i++)
            {
                var plane = CullingPlanes[i];
                var normal = plane.normal;
                // var distance = math.dot(normal, aabb.Center) + plane.distance;
                // var radius = math.dot(aabb.Extents, math.abs(normal));
                var distance = math.dot(normal, pos) + plane.distance;
                var radius = math.dot(extents, math.abs(normal));

                if (distance + radius <= 0)
                    return;
            }
            
            //increase count for target lod level
            uint lodIndex = BatchLODGroup.GetCurrentLOD(index);
            BatchLOD* batchLOD = BatchLODGroup.GetByRef((int)lodIndex);
            int count = Interlocked.Increment(ref UnsafeUtility.AsRef<int>((*batchLOD).visibleCount));
            // (*batchLOD).VisibleArrayPtr()[count - 1] = index;

            int visibleIndexOffset = batchLOD->m_VisibleInstanceIndexStartIndex;
            DrawInstanceIndexData[visibleIndexOffset + count - 1] = index;
        }
    }
}