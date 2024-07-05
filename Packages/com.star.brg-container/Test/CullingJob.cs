using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public struct CullingJob :  IJobParallelFor, IJobFor
{
    [ReadOnly] public NativeArray<Plane> cameraFrustumPlanes;
    // [ReadOnly] public Bounds bounds;

    public NativeArray<float3> targetMovePoints;
    public NativeArray<Matrix4x4> curMatrix;

    [WriteOnly, NativeDisableParallelForRestriction]
    public NativeArray<int> culledObjsCount;

    [WriteOnly, NativeDisableParallelForRestriction]
    // public NativeList<int>.ParallelWriter VisibleIndicesWriter;
    public NativeArray<int> VisibleIndicesWriter;

    public unsafe void Execute(int index)
    {
        float3 targetPos = targetMovePoints[index];
        Vector3 pos = curMatrix[index].GetPosition();
        
        Vector3 _pos = new Vector3(pos.x, pos.y, pos.z);
        Bounds bounds = new Bounds() { center = _pos, extents = Vector3.one };

        if (AABBTest(cameraFrustumPlanes, bounds))
        {
            int count = Interlocked.Increment(ref UnsafeUtility.ArrayElementAsRef<int>(culledObjsCount.GetUnsafePtr(), 0));
            // VisibleIndicesWriter.AddNoResize(index);
            VisibleIndicesWriter[count - 1] = index;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AABBTest(NativeArray<Plane> planes, Bounds _bouns)
    {
        for (var i = 0; i < planes.Length; i++)
        {
            var plane = planes[i];
            var normal = plane.normal;
            var distance = math.dot(normal, _bouns.center) + plane.distance;
            var radius = math.dot(_bouns.extents, math.abs(normal));

            if (distance + radius <= 0)
                return false;
        }

        return true;
    }
}