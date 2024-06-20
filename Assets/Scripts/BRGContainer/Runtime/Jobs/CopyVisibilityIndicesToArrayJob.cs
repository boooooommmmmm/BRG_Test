﻿using System.Collections.Generic;

namespace BRGContainer.Runtime
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
        public NativeArray<BatchGroup> BatchGroups;
        [ReadOnly]
        public NativeArray<int> VisibleCountPerBatch;
        // [ReadOnly] public NativeArray<int> VisibleIndices;
        
        [ReadOnly] public NativeArray<BatchGroupDrawRange> DrawRangesData;

        [NativeDisableUnsafePtrRestriction]
        public unsafe BatchCullingOutputDrawCommands* OutputDrawCommands;
        
        public unsafe void Execute(int index)
        {
            var drawRangeData = DrawRangesData[index];
            if(drawRangeData.Count == 0)
                return; // there is no any visible batches
            
            var batchGroup = BatchGroups[index];
            var windowCount = batchGroup.GetWindowCount();
            var visibleOffset = drawRangeData.VisibleIndexOffset;
            var visibleIndices = batchGroup.VisiblesPtr; 

            var batchStartIndex = drawRangeData.BatchIndex;
            for (var i = 0; i < windowCount; i++)
            {
                var batchIndex = batchStartIndex + i;
                var visibleCountPerBatch = VisibleCountPerBatch[batchIndex];
                if (visibleCountPerBatch == 0) // there is no any visible instances for this batch
                    continue;
                
                //sven test
                // int size = 20;
                // int offset = batchIndex * size;
                // UnsafeUtility.MemCpy((void*)((IntPtr) OutputDrawCommands->visibleInstances + visibleOffset * UnsafeUtility.SizeOf<int>()), (int*)VisibleIndices.GetUnsafeReadOnlyPtr() + offset, visibleCountPerBatch * UnsafeUtility.SizeOf<int>());
                UnsafeUtility.MemCpy((void*)((IntPtr) OutputDrawCommands->visibleInstances + visibleOffset * UnsafeUtility.SizeOf<int>()), visibleIndices, visibleCountPerBatch * UnsafeUtility.SizeOf<int>());

                visibleOffset += visibleCountPerBatch;
            }
        }
    }
}