#if !STAR_BRG_CONTAINER
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace BRGContainer.Test
{
    public partial class BRGTest
    {
        public unsafe JobHandle OnPerformCullingMainThread(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput,
            IntPtr userContext)
        {
            JobHandle batchHandle = default;

            //culling
            Plane[] cameraFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            int[] _visibleIndices = new int[kNumInstances];
            int totalCount = matrices[0].Length;
            int visibleCount = 0;
            for (int i = 0; i < totalCount; i++)
            {
                Vector3 pos = matrices[0][i].GetPosition();
                Bounds bounds = new Bounds() { center = pos, extents = Vector3.one };

                if (AABBTest(cameraFrustumPlanes, bounds))
                {
                    _visibleIndices[visibleCount] = i;
                    visibleCount++;
                }
            }

            int[] _culledCount = { visibleCount };
            NativeArray<int> culledCount = new NativeArray<int>(_culledCount, Allocator.TempJob);


            // start submit
            var OutputDrawCommands = (BatchCullingOutputDrawCommands*)cullingOutput.drawCommands.GetUnsafePtr();
            int alignment = UnsafeUtility.AlignOf<long>();

            var drawRangesCount = 1;
            var drawCommandCount = 1;

            int visibleInstanceCount = visibleCount;
            OutputDrawCommands->visibleInstanceCount = visibleInstanceCount;
            OutputDrawCommands->visibleInstances = (int*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<int>() * visibleInstanceCount, alignment, Allocator.TempJob);

            OutputDrawCommands->drawRangeCount = drawRangesCount;
            OutputDrawCommands->drawRanges = (BatchDrawRange*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawRange>() * drawRangesCount, alignment, Allocator.TempJob);

            OutputDrawCommands->drawCommandCount = drawCommandCount;
            OutputDrawCommands->drawCommands = (BatchDrawCommand*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchDrawCommand>() * drawCommandCount, alignment, Allocator.TempJob);

            OutputDrawCommands->drawCommandPickingInstanceIDs = null;

            OutputDrawCommands->instanceSortingPositions = null;
            OutputDrawCommands->instanceSortingPositionFloatCount = 0;

            // fill commands
            OutputDrawCommands->drawCommands[0].visibleOffset = 0;
            OutputDrawCommands->drawCommands[0].visibleCount = (uint)visibleCount;
            OutputDrawCommands->drawCommands[0].batchID = m_BatchID[0];
            OutputDrawCommands->drawCommands[0].materialID = m_MaterialID[0];
            OutputDrawCommands->drawCommands[0].meshID = m_MeshID[0];
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
            for (int i = 0; i < visibleCount; ++i)
            {
                OutputDrawCommands->visibleInstances[i] = _visibleIndices[i];
            }

            return new JobHandle();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AABBTest(NativeArray<Plane> planes, Bounds _bouns)
        {
            // return true;
            // Vector3 cameraPos = new Vector3(0f, 0f, 0f);
            // return Vector3.Distance(cameraPos, _bouns.center) <30.0f;
            // return true;
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

        public bool AABBTest(Plane[] _planes, Bounds _bouns)
        {
            NativeArray<Plane> planes = new NativeArray<Plane>(_planes, Allocator.Temp);
            return AABBTest(planes, _bouns);
        }
    }
}
#endif