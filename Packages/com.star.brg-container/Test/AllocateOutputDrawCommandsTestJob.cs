using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

[BurstCompile]
public struct AllocateOutputDrawCommandsTestJob : IJobParallelFor, IJobFor
{
    [ReadOnly] public NativeArray<int> culledObjsCount;
    [ReadOnly] public NativeArray<int> culledObjsInstance;
    [ReadOnly] public NativeArray<BatchID> batchID;
    [ReadOnly] public NativeArray<BatchMaterialID> batchMatID;
    [ReadOnly] public NativeArray<BatchMeshID> batchMeshID;
    [NativeDisableUnsafePtrRestriction] public unsafe BatchCullingOutputDrawCommands* OutputDrawCommands;

    public unsafe void Execute(int index)
    {
        if (index != 0)
            return;
        
        

        int alignment = UnsafeUtility.AlignOf<long>();

        var visibleCount = culledObjsCount[0];
        // var visibleCount = 3; 
        var drawRangesCount = 1;
        var drawCommandCount = 1;

        OutputDrawCommands->visibleInstanceCount = visibleCount;
        OutputDrawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>() * visibleCount,
            alignment, Allocator.TempJob);

        OutputDrawCommands->drawRangeCount = drawRangesCount;
        OutputDrawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>() * drawRangesCount, alignment, Allocator.TempJob);

        OutputDrawCommands->drawCommandCount = drawCommandCount;
        OutputDrawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>() * drawCommandCount, alignment, Allocator.TempJob);

        OutputDrawCommands->drawCommandPickingInstanceIDs = null;

        OutputDrawCommands->visibleInstanceCount = visibleCount;

        OutputDrawCommands->instanceSortingPositions = null;
        OutputDrawCommands->instanceSortingPositionFloatCount = 0;

        // fill commands
        OutputDrawCommands->drawCommands[0].visibleOffset = 0;
        OutputDrawCommands->drawCommands[0].visibleCount = (uint)visibleCount;
        OutputDrawCommands->drawCommands[0].batchID = batchID[0];
        OutputDrawCommands->drawCommands[0].materialID = batchMatID[0];
        OutputDrawCommands->drawCommands[0].meshID = batchMeshID[0];
        OutputDrawCommands->drawCommands[0].submeshIndex = 0;
        OutputDrawCommands->drawCommands[0].splitVisibilityMask = 0xff;
        OutputDrawCommands->drawCommands[0].flags = 0;
        OutputDrawCommands->drawCommands[0].sortingPosition = 0;

        //fill ranges
        OutputDrawCommands->drawRanges[0].drawCommandsBegin = (uint)0;
        OutputDrawCommands->drawRanges[0].drawCommandsCount = 1;
        OutputDrawCommands->drawRanges[0].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, };

        // for (int i = 0; i < OutputDrawCommands->visibleInstanceCount; ++i)
        //     OutputDrawCommands->visibleInstances[i] = i;
        for (int i = 0; i < OutputDrawCommands->visibleInstanceCount; ++i)
            OutputDrawCommands->visibleInstances[i] = culledObjsInstance[i];

        return;
    }
}