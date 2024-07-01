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
            // return CullingMainThread(rendererGroup, cullingContext, cullingOutput, userContext);
            return CullingParallel(rendererGroup, cullingContext, cullingOutput, userContext);
        }

        private const bool _forceJobFence = true; //default : false
        private const bool _useMainCameraCulling = true; //default : true

        private static float3 commonExtents = new float3(2, 2, 2);
        
        private unsafe JobHandle CullingParallel(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            // return new JobHandle();
            
            cullingOutput.drawCommands[0] = new BatchCullingOutputDrawCommands();
            NativeArray<BatchLODGroup> batchLODGroups = m_LODGroups.GetValueArray(Allocator.TempJob);
            
            var batchCount = 0;
            for (var i = 0; i < batchLODGroups.Length; i++)
                batchCount += batchLODGroups[i].GetWindowCount(); // sub batch count (for UBO)
            
            if (batchCount == 0)
                return batchLODGroups.Dispose(default);
            
            if (m_MainCamera)
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
                
                JobHandle batchHandle = default;
                
                uint* statePtr = batchLODGroup.StatePtr;
                
                // setup data
                var setupDataJob = new SetupDataJob()
                {
                    BatchGroupIndex = batchLODGroupIndex,
                    BatchLODGroup = batchLODGroup,
                };
                var setupDataJobHandle = setupDataJob.ScheduleByRef(maxInstanceCountPerBatch, 64, batchHandle);
                if (_forceJobFence) setupDataJobHandle.Complete();
                
                // [culling] active + frustum culling, add visible count for active lod levle.
                var cullingBatchInstancesJob = new CullingBatchInstancesJob
                {
                    CullingPlanes = cullingPlanes,
                    BatchLODGroup = batchLODGroup,
                    Extents = commonExtents, //@TODO: temp code, need HISMAABB
                    StatePtr = statePtr,
                    // DataOffset = maxInstancePerWindow * batchIndex,
                    DataOffset = 0,
                    BatchLODGroupIndex = batchLODGroupIndex,
                };
                batchHandle = cullingBatchInstancesJob.ScheduleByRef(maxInstanceCountPerBatch, 64, setupDataJobHandle);
                if (_forceJobFence) batchHandle.Complete();
                
                // offset += windowCount;
                // batchJobHandles[batchGroupIndex] = batchHandle;

                // for (uint lodIndex = 0; lodIndex < batchLODGroup.LODCount; lodIndex++)
                // {
                //     BatchLOD batchLOD = batchLODGroup[lodIndex];
                //     for (uint subMeshIndex = 0; subMeshIndex < batchLOD.SubMeshCount; subMeshIndex++)
                //     {
                //         BatchGroup batchGroup = batchLOD[subMeshIndex];
                //     }
                // }

            }
            
            var cullingHandle = JobHandleUnsafeUtility.CombineDependencies(batchJobHandles, batchLODGroups.Length);
            if (_forceJobFence) cullingHandle.Complete();
            
            var drawCounters = new NativeArray<int>(3, Allocator.TempJob);
            var drawRangeData = new NativeArray<BatchGroupDrawRange>(batchLODGroups.Length, Allocator.TempJob);
            
            offset = 0;
            for (var i = 0; i < batchLODGroups.Length; i++)
            {
                var batchLODGroup = batchLODGroups[i];
                var windowCount = batchLODGroup.GetWindowCount();
                windowCount = 1;
            
                var computeDrawCountersJob = new ComputeDrawCountersLODGroupJob()
                {
                    DrawCounters = drawCounters,
                    DrawRangesData = drawRangeData,
                    BatchLODGroups = batchLODGroups,
                    BatchGroupIndex = i,
                    BatchOffset = offset,
                };
            
                offset += windowCount;
                batchJobHandles[i] = computeDrawCountersJob.Schedule(cullingHandle);
                if (_forceJobFence) batchJobHandles[i].Complete();
            }
            
            var countersHandle = JobHandleUnsafeUtility.CombineDependencies(batchJobHandles, batchLODGroups.Length);
            if (_forceJobFence) countersHandle.Complete();
            
            
            var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            var allocateOutputDrawCommandsJob = new AllocateOutputDrawCommandsJob
            {
                OutputDrawCommands = drawCommands,
                Counters = drawCounters
            };
            var allocateOutputDrawCommandsHandle = allocateOutputDrawCommandsJob.Schedule(countersHandle);
            allocateOutputDrawCommandsHandle = drawCounters.Dispose(allocateOutputDrawCommandsHandle);
            if (_forceJobFence) allocateOutputDrawCommandsHandle.Complete();
            
            var createDrawRangesJob = new CreateDrawRangesJob
            {
                BatchLODGroups = batchLODGroups,
                DrawRangeData = drawRangeData,
                OutputDrawCommands = drawCommands
            };
            var createDrawRangesHandle = createDrawRangesJob.ScheduleParallel(batchLODGroups.Length, 64, allocateOutputDrawCommandsHandle);
            if (_forceJobFence) createDrawRangesHandle.Complete();
            
            var createDrawCommandsJob = new CreateDrawCommandsJob
            {
                BatchLODGroups = batchLODGroups,
                DrawRangeData = drawRangeData,
                OutputDrawCommands = drawCommands
            };
            var createDrawCommandsHandle = createDrawCommandsJob.ScheduleParallel(batchLODGroups.Length, 64, createDrawRangesHandle);
            if (_forceJobFence) createDrawCommandsHandle.Complete();
            
            var copyVisibilityIndicesToArrayJob = new CopyVisibilityIndicesToArrayJob
            {
                BatchLODGroups = batchLODGroups,
                DrawRangesData = drawRangeData,
                OutputDrawCommands = drawCommands
            };
            
            var resultHandle = copyVisibilityIndicesToArrayJob.ScheduleParallel(batchLODGroups.Length, 32, createDrawCommandsHandle);
            if (_forceJobFence) resultHandle.Complete();
            
            resultHandle = JobHandle.CombineDependencies(drawRangeData.Dispose(resultHandle), batchLODGroups.Dispose(resultHandle));
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