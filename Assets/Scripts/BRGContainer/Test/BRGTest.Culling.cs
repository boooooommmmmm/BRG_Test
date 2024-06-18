//#define TEMP_TEST_MODE

#if TEMP_TEST_MODE
#else

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace BRGContainer.Test
{

    public partial class BRGTest
    {
        public unsafe JobHandle OnPerformCullingDefault(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr serContext)
        {
            int alignment = UnsafeUtility.AlignOf<long>();

            // Acquire a pointer to the BatchCullingOutputDrawCommands struct so you can easily modify it directly.
            var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            // Allocate memory for the output arrays. In a more complicated implementation, you would calculate
            // the amount of memory to allocate dynamically based on what is visible.
            // This example assumes that all of the instances are visible and thus allocates
            // memory for each of them. The necessary allocations are as follows:
            // - a single draw command (which draws kNumInstances instances)
            // - a single draw range (which covers our single draw command)
            // - kNumInstances visible instance indices.
            // You must always allocate the arrays using Allocator.TempJob.
            drawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>() * m_BatchCount, alignment, Allocator.TempJob);
            drawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>() * m_BatchCount, alignment, Allocator.TempJob);
            drawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(kNumInstances * sizeof(int) * m_BatchCount, alignment, Allocator.TempJob);
            drawCommands->drawCommandPickingInstanceIDs = null;

            drawCommands->drawCommandCount = m_BatchCount;
            drawCommands->drawRangeCount = m_BatchCount;
            drawCommands->visibleInstanceCount = kNumInstances * m_BatchCount; // * m_BatchCount;

            // This example doens't use depth sorting, so it leaves instanceSortingPositions as null.
            drawCommands->instanceSortingPositions = null;
            drawCommands->instanceSortingPositionFloatCount = 0;

            for (int i = 0; i < drawCommands->drawCommandCount; ++i)
                SetupDrawCommand(drawCommands, i);
            for (int i = 0; i < drawCommands->drawRangeCount; ++i)
                SetupDrawRange(drawCommands, i);

            // Finally, write the actual visible instance indices to the array. In a more complicated
            // implementation, this output would depend on what is visible, but this example
            // assumes that everything is visible.
            for (int i = 0; i < drawCommands->visibleInstanceCount; ++i)
                drawCommands->visibleInstances[i] = i;

            // This simple example doesn't use jobs, so it returns an empty JobHandle.
            // Performance-sensitive applications are encouraged to use Burst jobs to implement
            // culling and draw command output. In this case, this function returns a
            // handle here that completes when the Burst jobs finish.
            return new JobHandle();

            // return cullingJob.Schedule(kNumInstances, 64);
        }


        unsafe void SetupDrawCommand(BatchCullingOutputDrawCommands* drawCommands, int index)
        {
            // Configure the single draw command to draw kNumInstances instances
            // starting from offset 0 in the array, using the batch, material and mesh
            // IDs registered in the Start() method. It doesn't set any special flags.
            drawCommands->drawCommands[index].visibleOffset = 0;
            drawCommands->drawCommands[index].visibleCount = (uint)kNumInstances;
            drawCommands->drawCommands[index].batchID = m_BatchID[index];
            drawCommands->drawCommands[index].materialID = m_MaterialID[index];
            drawCommands->drawCommands[index].meshID = m_MeshID[index];
            drawCommands->drawCommands[index].submeshIndex = 0;
            drawCommands->drawCommands[index].splitVisibilityMask = 0xff;
            drawCommands->drawCommands[index].flags = 0;
            drawCommands->drawCommands[index].sortingPosition = 0;
        }

        unsafe void SetupDrawRange(BatchCullingOutputDrawCommands* drawCommands, int index)
        {
            // Configure the single draw range to cover the single draw command which is at offset 0.
            drawCommands->drawRanges[index].drawCommandsBegin = (uint)index;
            drawCommands->drawRanges[index].drawCommandsCount = 1;

            // This example doesn't care about shadows or motion vectors, so it leaves everything
            // at the default zero values, except the renderingLayerMask which it sets to all ones
            // so Unity renders the instances regardless of mask settings.
            drawCommands->drawRanges[index].filterSettings = new BatchFilterSettings { renderingLayerMask = 0xffffffff, };
        }


        private JobHandle cullingHandle;

        public unsafe JobHandle OnPerformCullingParallel(
            BatchRendererGroup rendererGroup,
            BatchCullingContext cullingContext,
            BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            JobHandle batchHandle = default;

            // var batchJobHandles = stackalloc JobHandle[1];

            if (cullingHandle.IsCompleted)
            {
                cullingHandle.Complete();
            }

            Plane[] cameraFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            NativeArray<Plane> planesArray = new NativeArray<Plane>(cameraFrustumPlanes, Allocator.TempJob);
            NativeArray<float3> _targetPoints = new NativeArray<float3>(m_TargetPoints[0], Allocator.TempJob);
            int[] _culledCount = { 0 };
            NativeArray<int> culledCount = new NativeArray<int>(_culledCount, Allocator.TempJob);
            // NativeArray<int> culledCount = new NativeArray<int>(kNumInstances, Allocator.TempJob);
            NativeArray<int> visibleIndices = new NativeArray<int>(kNumInstances, Allocator.TempJob);
            var cullingJob = new CullingJob
            {
                cameraFrustumPlanes = planesArray,
                targetMovePoints = _targetPoints,
                curMatrix = tempMatrices,
                culledObjsCount = culledCount,
                VisibleIndicesWriter = visibleIndices,
            };
            cullingHandle = cullingJob.Schedule(kNumInstances, 64, batchHandle);
            // cullingJobHandle.Complete();


            // _culledCount = new[] { kNumInstances };
            // culledCount = new NativeArray<int>(_culledCount, Allocator.TempJob);
            var drawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            var batchIDArray = new NativeArray<BatchID>(m_BatchID, Allocator.TempJob);
            var matIDArray = new NativeArray<BatchMaterialID>(m_MaterialID, Allocator.TempJob);
            var meshIDArray = new NativeArray<BatchMeshID>(m_MeshID, Allocator.TempJob);
            var allocateCommandJob = new AllocateOutputDrawCommandsTestJob()
            {
                OutputDrawCommands = drawCommands,
                culledObjsCount = culledCount,
                culledObjsInstance = visibleIndices,
                batchID = batchIDArray,
                batchMatID = matIDArray,
                batchMeshID = meshIDArray,
            };

            var allocateCommandJobHandle = allocateCommandJob.Schedule(1, 1, cullingHandle);
            allocateCommandJobHandle = visibleIndices.Dispose(allocateCommandJobHandle);
            // allocateCommandJobHandle.Complete();

            return allocateCommandJobHandle;

            // return resultHandle;
        }
    }
}

#endif