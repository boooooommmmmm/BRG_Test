using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace BRGContainer.Runtime
{
    public partial class BRGContainer
    {
        [BurstCompile]
        private unsafe JobHandle CullingCallback(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            // return CullingMainThread(rendererGroup, cullingContext, cullingOutput, userContext);
            return CullingParallel(rendererGroup, cullingContext, cullingOutput, userContext);
        }

        private const bool _forceJobFence = true; //default : false
        private const bool _useMainCameraCulling = true; //default : true

        //Sven test
        [NativeDisableContainerSafetyRestriction]
        private NativeArray<BatchInstanceData> instanceDataPerBatch = new NativeArray<BatchInstanceData>(500, Allocator.Persistent);
        private NativeArray<int> visibleCountPerBatch = new NativeArray<int>(500, Allocator.Persistent);
        private int _initedInstanceDataCount = 0;
        private unsafe JobHandle CullingParallel(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            cullingOutput.drawCommands[0] = new BatchCullingOutputDrawCommands();
            var batchGroups = m_Groups.GetValueArray(Allocator.TempJob);

            var batchCount = 0;
            for (var i = 0; i < batchGroups.Length; i++)
                batchCount += batchGroups[i].GetWindowCount(); // sub batch count (for UBO)

            if (batchCount == 0)
                return batchGroups.Dispose(default);

            NativeArray<Plane> cullingPlanes = new NativeArray<Plane>(GeometryUtility.CalculateFrustumPlanes(m_Camera), Allocator.TempJob);
            if (!_useMainCameraCulling)
            {
                cullingPlanes.Dispose();
                cullingPlanes = cullingContext.cullingPlanes;
            }

            // var instanceDataPerBatch = new NativeArray<BatchInstanceData>(batchCount, Allocator.TempJob);
            // var visibleCountPerBatch = new NativeArray<int>(batchCount, Allocator.TempJob);
            var visibleInstanceCount = new NativeArray<int>(batchGroups.Length, Allocator.TempJob); //assume each batch only has one window 
            var visibleIndices = new NativeArray<int>(batchGroups.Length * 20 * 1, Allocator.TempJob); //assume each batch only has one window
            
            //Sven test
            while (_initedInstanceDataCount < batchCount)
            {
                BatchInstanceData instanceIndices = default;
                instanceIndices.Indices = (int*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<int>() * 20,
                    UnsafeUtility.AlignOf<int>(), Allocator.Persistent, 0);
                instanceDataPerBatch[_initedInstanceDataCount] = instanceIndices;
                _initedInstanceDataCount++;
            }

            var offset = 0;
            var batchJobHandles = stackalloc JobHandle[batchGroups.Length];
            for (var batchGroupIndex = 0; batchGroupIndex < batchGroups.Length; batchGroupIndex++)
            {
                var batchGroup = batchGroups[batchGroupIndex];

                var maxInstancePerWindow = batchGroup.m_BatchDescription.MaxInstancePerWindow;
                var windowCount = batchGroup.GetWindowCount();
                // var objectToWorld = batchGroup.GetObjectToWorldArray(Allocator.TempJob);
                //Sven test
                var objectToWorld = batchGroup.GetO2WArrayPtr();

                JobHandle batchHandle = default;
                for (var batchIndex = 0; batchIndex < windowCount; batchIndex++)
                {
                    var maxInstanceCountPerBatch = batchGroup.GetInstanceCountPerWindow(batchIndex);

                    // var visibleIndices = new NativeArray<int>(maxInstanceCountPerBatch * 1, Allocator.TempJob);

                    // culling
                    var cullingBatchInstancesJob = new CullingBatchInstancesJob
                    {
                        CullingPlanes = cullingPlanes,
                        // ObjectToWorld = objectToWorld,
                        ObjectToWorldPtr = objectToWorld,
                        VisibleInstanceCount = visibleInstanceCount,
                        VisibleIndices = visibleIndices,
                        // InstanceDataPerBatch = instanceDataPerBatch,
                        DataOffset = maxInstancePerWindow * batchIndex,
                        BatchGroupIndex = batchGroupIndex,
                    };
                    var cullingBatchInstancesJobHandle = cullingBatchInstancesJob.ScheduleByRef(maxInstanceCountPerBatch, 64, batchHandle);
                    if (_forceJobFence) cullingBatchInstancesJobHandle.Complete();

                    // return cullingBatchInstancesJobHandle;

                    //fill
                    var copyVisibleIndicesToMapJob = new CopyVisibleIndicesToMapJob
                    {
                        InstanceDataPerBatch = instanceDataPerBatch,
                        VisibleIndices = visibleIndices,
                        VisibleInstanceCount = visibleInstanceCount,
                        VisibleCountPerBatch = visibleCountPerBatch,
                        BatchIndex = offset + batchIndex,
                        BatchGroupIndex = batchGroupIndex,
                    };
                    batchHandle = copyVisibleIndicesToMapJob.ScheduleByRef(cullingBatchInstancesJobHandle);
                    // batchHandle = visibleInstanceCount.Dispose(batchHandle);
                    // batchHandle = visibleIndices.Dispose(batchHandle);
                    if (_forceJobFence) batchHandle.Complete();
                    
                    // NativeArray<int> temp = new NativeArray<int>(instanceCount[0], Allocator.Temp);
                    // for (int ii = 0 ; ii < instanceCount[0]; ii++)
                    //     temp[ii] = instanceDataPerBatch[0].Indices[ii];
                }

                offset += windowCount;
                // batchJobHandles[batchGroupIndex] = objectToWorld.Dispose(batchHandle);
                batchJobHandles[batchGroupIndex] = batchHandle;
            }

            var cullingHandle = JobHandleUnsafeUtility.CombineDependencies(batchJobHandles, batchGroups.Length);
            // cullingHandle = visibleInstanceCount.Dispose(cullingHandle);
            if (_forceJobFence) cullingHandle.Complete();

            var drawCounters = new NativeArray<int>(3, Allocator.TempJob);
            var drawRangeData = new NativeArray<BatchGroupDrawRange>(batchGroups.Length, Allocator.TempJob);

            offset = 0;
            for (var i = 0; i < batchGroups.Length; i++)
            {
                var batchGroup = batchGroups[i];
                var windowCount = batchGroup.GetWindowCount();

                var computeDrawCountersJob = new ComputeDrawCountersJob
                {
                    DrawCounters = drawCounters,
                    VisibleCountPerBatch = visibleCountPerBatch,
                    DrawRangesData = drawRangeData,
                    BatchGroups = batchGroups,
                    BatchGroupIndex = i,
                    BatchOffset = offset,
                    InstanceDataPerBatch = instanceDataPerBatch
                };

                offset += windowCount;
                batchJobHandles[i] = computeDrawCountersJob.ScheduleByRef(cullingHandle);
                if (_forceJobFence) batchJobHandles[i].Complete();
            }

            var countersHandle = JobHandleUnsafeUtility.CombineDependencies(batchJobHandles, batchGroups.Length);
            if (_forceJobFence) countersHandle.Complete();
            

            var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            var allocateOutputDrawCommandsJob = new AllocateOutputDrawCommandsJob
            {
                OutputDrawCommands = drawCommands,
                Counters = drawCounters
            };
            var allocateOutputDrawCommandsHandle = allocateOutputDrawCommandsJob.ScheduleByRef(countersHandle);
            allocateOutputDrawCommandsHandle = drawCounters.Dispose(allocateOutputDrawCommandsHandle);
            if (_forceJobFence) allocateOutputDrawCommandsHandle.Complete();

            var createDrawRangesJob = new CreateDrawRangesJob
            {
                BatchGroups = batchGroups,
                DrawRangeData = drawRangeData,
                OutputDrawCommands = drawCommands
            };
            var createDrawRangesHandle = createDrawRangesJob.ScheduleParallelByRef(batchGroups.Length, 64, allocateOutputDrawCommandsHandle);
            if (_forceJobFence) createDrawRangesHandle.Complete();

            var createDrawCommandsJob = new CreateDrawCommandsJob
            {
                BatchGroups = batchGroups,
                DrawRangeData = drawRangeData,
                VisibleCountPerBatch = visibleCountPerBatch,
                InstanceDataPerBatch = instanceDataPerBatch,
                OutputDrawCommands = drawCommands
            };
            var createDrawCommandsHandle = createDrawCommandsJob.ScheduleParallelByRef(batchGroups.Length, 64, createDrawRangesHandle);
            if (_forceJobFence) createDrawCommandsHandle.Complete();

            var copyVisibilityIndicesToArrayJob = new CopyVisibilityIndicesToArrayJob
            {
                BatchGroups = batchGroups,
                VisibleCountPerBatch = visibleCountPerBatch,
                InstanceDataPerBatch = instanceDataPerBatch,
                DrawRangesData = drawRangeData,
                OutputDrawCommands = drawCommands
            };

            var resultHandle = copyVisibilityIndicesToArrayJob.ScheduleParallelByRef(batchGroups.Length, 32, createDrawCommandsHandle);
            if (_forceJobFence) resultHandle.Complete();
            // resultHandle = JobHandle.CombineDependencies(instanceDataPerBatch.Dispose(resultHandle),
                // visibleCountPerBatch.Dispose(resultHandle));
            resultHandle = JobHandle.CombineDependencies(drawRangeData.Dispose(resultHandle), batchGroups.Dispose(resultHandle));
            resultHandle = cullingPlanes.Dispose(resultHandle);
            //sven test
            resultHandle = visibleInstanceCount.Dispose(resultHandle);
            resultHandle = visibleIndices.Dispose(resultHandle);
            if (_forceJobFence) resultHandle.Complete();


            return resultHandle;
        }
        
        private unsafe JobHandle CullingMainThread(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            return new JobHandle();
        }
    }
}