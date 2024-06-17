using BRGContainer.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
partial struct RandomMoveJob : IJobParallelFor
{
    [ReadOnly]
    public Unity.Mathematics.Random random;
    [ReadOnly]
    public float4 randomPostionRange;
    [ReadOnly]
    public float m_DeltaTime;
 
    public NativeArray<Matrix4x4> matrices;
    public NativeArray<float3> targetMovePoints;
    public NativeArray<PackedMatrix> obj2WorldArr;
    public NativeArray<PackedMatrix> world2ObjArr;
    [BurstCompile]
    public void Execute(int index)
    {
        float3 curPos = matrices[index].GetPosition();
        float3 dir = targetMovePoints[index] - curPos;
        if (Unity.Mathematics.math.lengthsq(dir) < 0.4f)
        {
            var newTargetPos = targetMovePoints[index];
            newTargetPos.x = random.NextFloat(randomPostionRange.x, randomPostionRange.y);
            newTargetPos.z = random.NextFloat(randomPostionRange.z, randomPostionRange.w);
            targetMovePoints[index] = newTargetPos;
        }
 
        dir = math.normalizesafe(targetMovePoints[index] - curPos, Vector3.forward);
        curPos += dir * m_DeltaTime;// math.lerp(curPos, targetMovePoints[index], m_DeltaTime);
 
        var mat = matrices[index];
        mat.SetTRS(curPos, Quaternion.LookRotation(dir), Vector3.one);
        matrices[index] = mat;
        var item = obj2WorldArr[index];
        item.SetData(mat);
        obj2WorldArr[index] = item;
 
        item = world2ObjArr[index];
        item.SetData(mat.inverse);
        world2ObjArr[index] = item;
    }
}