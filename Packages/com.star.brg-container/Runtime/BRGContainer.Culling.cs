using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace BRGContainer.Runtime
{
    public partial class BRGContainer
    {
        // [BurstCompile]
        private unsafe JobHandle CullingCallback(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput, IntPtr userContext)
        {
	        // return new JobHandle();
            // return CullingMainThread(rendererGroup, cullingContext, cullingOutput, userContext);
            return CullingParallel(rendererGroup, cullingContext, cullingOutput, userContext);
        }

        private const bool _forceJobFence = false; //default : false
        private const bool _useMainCameraCulling = true; //default : true

        private static float3 commonExtents = new float3(2, 2, 2);
        
        private unsafe JobHandle CullingParallel(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            // return new JobHandle();
            
            cullingOutput.drawCommands[0] = new BatchCullingOutputDrawCommands();
            NativeArray<BatchLODGroup> batchLODGroups = m_LODGroups.GetValueArray(Allocator.TempJob);
            
            if (batchLODGroups.Length == 0)
                return batchLODGroups.Dispose(default);

            if (m_MainCamera == null)
             return batchLODGroups.Dispose(default);
            // NativeArray<Plane> cullingPlanes = new NativeArray<Plane>(GeometryUtility.CalculateFrustumPlanes(m_Camera), Allocator.TempJob);
            Matrix4x4 matrix4X4 = m_MainCamera.cameraToWorldMatrix;
            matrix4X4.m03 -= m_WorldOffset.x;
            matrix4X4.m13 -= m_WorldOffset.y;
            matrix4X4.m23 -= m_WorldOffset.z;
            matrix4X4 = m_MainCamera.projectionMatrix * matrix4X4.inverse;
            NativeArray<Plane> cullingPlanes = new NativeArray<Plane>(GeometryUtility.CalculateFrustumPlanes(matrix4X4), Allocator.TempJob);
            if (!_useMainCameraCulling)
            {
                cullingPlanes.Dispose();
                cullingPlanes = cullingContext.cullingPlanes;
            }
            
            JobHandle defaultHandle = default;
            
            // setup data
            JobHandle setupDataHandle = default;
            var setupDataJob = new SetupDataJob()
            {
                BatchLODGroups = batchLODGroups,
            };
            var setupDataJobHandle = setupDataJob.ScheduleByRef(batchLODGroups.Length, 64, defaultHandle);
            if (_forceJobFence) setupDataJobHandle.Complete();
            

            var drawInstanceIndexData = m_VisibleInstanceIndexData;
            
            var offset = 0;
            var batchJobHandles = stackalloc JobHandle[batchLODGroups.Length];
            for (var batchLODGroupIndex = 0; batchLODGroupIndex < batchLODGroups.Length; batchLODGroupIndex++)
            {
                BatchLODGroup batchLODGroup = batchLODGroups[batchLODGroupIndex]; //can use ptr instead of copy struct
                
                // assume window count is always 1
                // var maxInstancePerWindow = batchLODGroup.m_BatchDescription.MaxInstancePerWindow;
                // var windowCount = batchLODGroup.GetWindowCount();
                // windowCount = 1; // assume window count is always 1.
                var maxInstanceCountPerBatch = batchLODGroup.GetInstanceCountPerWindow(0);
                
                uint* statePtr = batchLODGroup.StatePtr;
                
                // [culling] active + frustum culling, add visible count for active lod level.
                var cullingBatchInstancesJob = new CullingBatchInstancesJob
                {
                    CullingPlanes = cullingPlanes,
                    BatchLODGroup = batchLODGroup,
                    Extents = commonExtents, //@TODO: temp code, need HISMAABB
                    StatePtr = statePtr,
                    // DataOffset = maxInstancePerWindow * batchIndex,
                    DrawInstanceIndexData = drawInstanceIndexData,
                    DataOffset = 0,
                    BatchLODGroupIndex = batchLODGroupIndex,
                };
                JobHandle batchHandle = cullingBatchInstancesJob.ScheduleByRef(maxInstanceCountPerBatch, 64, setupDataJobHandle);
                if (_forceJobFence) batchHandle.Complete();
                
                // offset += windowCount;
                batchJobHandles[batchLODGroupIndex] = batchHandle;
            }
            
            var cullingHandle = JobHandleUnsafeUtility.CombineDependencies(batchJobHandles, batchLODGroups.Length);
            if (_forceJobFence) cullingHandle.Complete();

            // return cullingHandle;
            
            var drawCounters = new NativeArray<int>(3, Allocator.TempJob);
            // var drawRangeData = new NativeArray<BatchGroupDrawRange>(batchLODGroups.Length, Allocator.TempJob);

            var computeDrawCountersJob = new ComputeDrawCountersLODGroupJob()
            {
                DrawCounters = drawCounters,
                BatchLODGroups = batchLODGroups,
            };

            var countersHandle = computeDrawCountersJob.ScheduleByRef(batchLODGroups.Length, 64, cullingHandle);
            if (_forceJobFence) countersHandle.Complete();
            
            var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            var allocateOutputDrawCommandsJob = new AllocateOutputDrawCommandsJob
            {
                OutputDrawCommands = drawCommands,
                Counters = drawCounters,
                TotalBatchCount = m_TotalBatchCount,
                MaxVisibleCount = m_VisibleInstanceIndexTotalCount,
            };
            var allocateOutputDrawCommandsHandle = allocateOutputDrawCommandsJob.Schedule(countersHandle);
            allocateOutputDrawCommandsHandle = drawCounters.Dispose(allocateOutputDrawCommandsHandle);
            if (_forceJobFence) allocateOutputDrawCommandsHandle.Complete();
            
            // return countersHandle;
            
            var createDrawRangesJob = new CreateDrawRangesJob
            {
                BatchLODGroups = batchLODGroups,
                OutputDrawCommands = drawCommands
            };
            var createDrawRangesHandle = createDrawRangesJob.ScheduleParallel(batchLODGroups.Length, 64, allocateOutputDrawCommandsHandle);
            if (_forceJobFence) createDrawRangesHandle.Complete();
            
            var createDrawCommandsJob = new CreateDrawCommandsJob
            {
                BatchLODGroups = batchLODGroups,
                OutputDrawCommands = drawCommands
            };
            var createDrawCommandsHandle = createDrawCommandsJob.ScheduleParallel(batchLODGroups.Length, 64, createDrawRangesHandle);
            if (_forceJobFence) createDrawCommandsHandle.Complete();
            
            var copyVisibilityIndicesToArrayJob = new CopyVisibilityIndicesToArrayJob
            {
                BatchLODGroups = batchLODGroups,
                DrawInstanceIndexData = (int*)drawInstanceIndexData.GetUnsafePtr(),
                OutputDrawCommands = drawCommands
            };
            
            var resultHandle = copyVisibilityIndicesToArrayJob.ScheduleParallel(batchLODGroups.Length, 32, createDrawCommandsHandle);
            if (_forceJobFence) resultHandle.Complete();
            
            // resultHandle = JobHandle.CombineDependencies(/*drawRangeData.Dispose(resultHandle),*/ batchLODGroups.Dispose(resultHandle), drawInstanceIndexData.Dispose(resultHandle));
            resultHandle = batchLODGroups.Dispose(resultHandle);
            resultHandle = cullingPlanes.Dispose(resultHandle);
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