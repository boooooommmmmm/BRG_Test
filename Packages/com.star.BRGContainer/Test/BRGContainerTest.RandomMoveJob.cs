using BRGContainer.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
partial struct BRGContainerTestRandomMoveJob : IJobParallelFor
{
    [NativeDisableParallelForRestriction]
    public BatchInstanceDataBuffer InstanceDataBuffer;
    
    public int ObjectToWorldPropertyId;
    
    [ReadOnly]
    public Unity.Mathematics.Random random;
    [ReadOnly]
    public float4 randomPostionRange;
    [ReadOnly]
    public float m_DeltaTime;

    [ReadOnly, NativeDisableParallelForRestriction, NativeDisableUnsafePtrRestriction] public BatchGroup BatchGroup;
 
    public NativeArray<float3> targetMovePoints;
    
    [BurstCompile]
    public void Execute(int index)
    {
        var objectToWorld = InstanceDataBuffer.ReadInstanceData<PackedMatrix>(index, ObjectToWorldPropertyId);
        var curPos = objectToWorld.GetPosition();

        float3 dir = targetMovePoints[index] - curPos;
        if (Unity.Mathematics.math.lengthsq(dir) < 0.4f)
        {
            var newTargetPos = targetMovePoints[index];
            newTargetPos.x = random.NextFloat(randomPostionRange.x, randomPostionRange.y);
            newTargetPos.z = random.NextFloat(randomPostionRange.z, randomPostionRange.w);
            targetMovePoints[index] = newTargetPos;
        }
 
        dir = math.normalizesafe(targetMovePoints[index] - curPos, Vector3.forward);
        curPos += dir * m_DeltaTime;
        
        InstanceDataBuffer.SetTRS(index, curPos, Quaternion.LookRotation(dir), Vector3.one);
    }
}