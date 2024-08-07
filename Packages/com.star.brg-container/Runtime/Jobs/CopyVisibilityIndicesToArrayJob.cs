﻿namespace BRGContainer.Runtime
{
    using System;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine.Rendering;

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Fast, CompileSynchronously = true, FloatPrecision = FloatPrecision.Low, DisableSafetyChecks = true)]
    internal struct CopyVisibilityIndicesToArrayJob : IJobFor
    {
        [ReadOnly, NativeDisableContainerSafetyRestriction]
        public NativeArray<BatchLODGroup> BatchLODGroups;

        // [ReadOnly] public NativeArray<BatchGroupDrawRange> DrawRangesData;

        [ReadOnly, NativeDisableUnsafePtrRestriction]
        public unsafe int* DrawInstanceIndexData;

        [NativeDisableUnsafePtrRestriction] public unsafe BatchCullingOutputDrawCommands* OutputDrawCommands;

        public unsafe void Execute(int index)
        {
            ref var batchLODGroup = ref UnsafeUtility.ArrayElementAsRef<BatchLODGroup>(BatchLODGroups.GetUnsafePtr(), index);

            for (uint lodIndex = 0; lodIndex < batchLODGroup.LODCount; lodIndex++)
            {
                ref var batchLOD = ref UnsafeUtility.ArrayElementAsRef<BatchLOD>(batchLODGroup.m_BatchLODs, (int)lodIndex);
                int visibleCount = batchLOD.VisibleCount;

                if (!batchLOD.IsInitialied || batchLOD.VisibleCount == 0)
                    continue;

                for (uint subMeshIndex = 0; subMeshIndex < batchLOD.SubMeshCount; subMeshIndex++)
                {
                    int visibleIndexOffset = batchLOD.m_VisibleInstanceIndexStartIndex;
                    int targetVisibleIndexOffset = batchLOD.m_VisibleInstanceIndexStartIndex ;
                    
                    // IntPtr is different with int*, need calculate pointer size
                    UnsafeUtility.MemCpy((void*)((IntPtr)OutputDrawCommands->visibleInstances + (targetVisibleIndexOffset) * UnsafeUtility.SizeOf<int>()), (void*)((IntPtr)DrawInstanceIndexData + visibleIndexOffset * UnsafeUtility.SizeOf<int>()), visibleCount * UnsafeUtility.SizeOf<int>());
                    
                    //only need copy once for each LOD
                    break;
                }
            }
        }
    }
}