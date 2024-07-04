using System;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;


namespace BRGContainer.Runtime
{
    [BRGClassThreadUnsafe]
    public sealed partial class BRGContainer : IDisposable
    {
        //global static data   
        [BRGValueThreadSafe] private static readonly ConcurrentDictionary<ContainerID, BRGContainer> m_Containers; //each view holds a single BRG Container
        private static long m_ContainerGlobalID;
        private static long m_BatchLODGroupGlobalID;

        //per group data
        private readonly BatchRendererGroup m_BatchRendererGroup; // unity brg
        private readonly ContainerID m_ContainerId;
        private readonly Dictionary<BatchLODGroupID, GraphicsBuffer> m_GraphicsBuffers;
        private /*readonly*/ NativeParallelHashMap<BatchLODGroupID, BatchLODGroup> m_LODGroups;

        private Camera m_MainCamera;
        private float3 m_WorldOffset = float3.zero;
        internal int m_TotalBatchCount;

        internal int m_VisibleInstanceIndexStartOffset = 0;
        internal int m_VisibleInstanceIndexTotalCount = 0;
        private NativeArray<int> m_VisibleInstanceIndexData;
        private int visibleInstanceIndexRealTotalCount = 1;
        private bool needForceUpdateVisibleInstanceIndexData = false;


        //static
        static BRGContainer()
        {
            m_Containers = new ConcurrentDictionary<ContainerID, BRGContainer>();
        }

        private BRGContainer()
        {
            m_ContainerId = new ContainerID(Interlocked.Increment(ref m_ContainerGlobalID));
            m_BatchRendererGroup = new BatchRendererGroup(CullingCallback, IntPtr.Zero);
            m_GraphicsBuffers = new Dictionary<BatchLODGroupID, GraphicsBuffer>();
            m_LODGroups = new NativeParallelHashMap<BatchLODGroupID, BatchLODGroup>(1, Allocator.Persistent);
            m_VisibleInstanceIndexData = new NativeArray<int>(visibleInstanceIndexRealTotalCount, Allocator.Persistent);

            m_Containers.TryAdd(m_ContainerId, this);
            m_TotalBatchCount = 0;
        }

        public BRGContainer(Bounds bounds) : this()
        {
            m_BatchRendererGroup.SetGlobalBounds(bounds);
        }

        public void SetMainCamera(Camera camera)
        {
            m_MainCamera = camera;
        }

        public void SetWorldOffset(float3 offset)
        {
            m_WorldOffset = offset;
        }


        public void SetGlobalBounds(Bounds bounds)
        {
            // m_BatchRendererGroup.SetGlobalBounds(bounds);
        }


        // for resize
        // public unsafe BatchHandle AddBatch(ref BatchGroup batchGroup, ref BatchDescription batchDescription, GraphicsBuffer graphicsBuffer)
        // {
        //     batchGroup.Register(m_BatchRendererGroup, graphicsBuffer.bufferHandle);
        //
        //     BatchID batchId = batchGroup[0];
        //     m_GraphicsBuffers.Add(batchId, graphicsBuffer);
        //     m_Groups.Add(batchId, batchGroup);
        //
        //     return new BatchHandle(m_ContainerId, batchId, batchGroup.GetNativeBuffer(), batchGroup.m_InstanceCount, ref batchDescription);
        // }

        [BRGMethodThreadUnsafe]
        public unsafe LODGroupBatchHandle AddLODGroupWithData(ref BatchDescription batchDescription, in RendererDescription rendererDescription,
            ref BatchWorldObjectData worldObjectData)
        {
            GraphicsBuffer graphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, batchDescription.TotalBufferSize);

            BatchLODGroup batchLODGroup = CreateBatchLODGroup(in batchDescription, in rendererDescription, out BatchLODGroupID batchLODGroupID, ref worldObjectData,
                Allocator.Persistent);

            m_TotalBatchCount += batchLODGroup.Register(m_BatchRendererGroup, graphicsBuffer.bufferHandle);

            m_GraphicsBuffers.Add(batchLODGroupID, graphicsBuffer);
            m_LODGroups.Add(batchLODGroupID, batchLODGroup);

            // check need update index data buffer
            UpdateVisibleIndexDataOffset();

            return new LODGroupBatchHandle(m_ContainerId, batchLODGroupID, batchLODGroup.LODCount, batchLODGroup.GetNativeBuffer(), batchLODGroup.m_InstanceCount,
                ref batchDescription);
        }

        [BRGMethodThreadUnsafe]
        public unsafe LODGroupBatchHandle AddEmptyLODGroup(ref BatchDescription batchDescription)
        {
            GraphicsBuffer graphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, batchDescription.TotalBufferSize);
            BatchLODGroup batchLODGroup = CreateEmptyBatchLODGroup(in batchDescription, out BatchLODGroupID batchLODGroupID, Allocator.Persistent);

            // should manually register
            // m_TotalBatchCount += batchLODGroup.Register(m_BatchRendererGroup, graphicsBuffer.bufferHandle);

            m_GraphicsBuffers.Add(batchLODGroupID, graphicsBuffer);
            m_LODGroups.Add(batchLODGroupID, batchLODGroup);

            // check need update index data buffer
            UpdateVisibleIndexDataOffset();

            return new LODGroupBatchHandle(m_ContainerId, batchLODGroupID, batchLODGroup.LODCount, batchLODGroup.GetNativeBuffer(), batchLODGroup.m_InstanceCount,
                ref batchDescription);
        }

        public void AddLODData(in BatchLODGroupID batchLODGroupID, int lodIndex, Mesh mesh, Material[] materials, in RendererDescription rendererDescription)
        {
            GetBatchLODGroup(in batchLODGroupID, out BatchLODGroup batchLODGroup);
            GraphicsBufferHandle bufferHandle = GetGraphicBuffer(in batchLODGroupID).bufferHandle;
            int registerBatchCount = batchLODGroup.SetLODRenderDataAndRegister(m_BatchRendererGroup, lodIndex, bufferHandle, in rendererDescription, mesh, materials, materials.Length);
            m_TotalBatchCount += registerBatchCount;
            
            // sven todo: check batchLODGroup copy ctor, should be all right...
        }


        public void RemoveBatchLODGroup(in LODGroupBatchHandle lodGroupBatchHandle)
        {
            DestroyBatchLODGroup(m_ContainerId, lodGroupBatchHandle.m_BatchLODGroupID);
        }

        public BatchLODGroup GetBatchLODGroup(BatchLODGroupID id)
        {
            return m_LODGroups[id];
        }

        public void GetBatchLODGroup(BatchLODGroupID id, out BatchLODGroup batchLODGroup)
        {
            batchLODGroup = m_LODGroups[id];
        }

        public void GetBatchLODGroup(in BatchLODGroupID id, out BatchLODGroup batchLODGroup)
        {
            batchLODGroup = m_LODGroups[id];
        }

        public GraphicsBuffer GetGraphicBuffer(in BatchLODGroupID id)
        {
            return m_GraphicsBuffers[id];
        }

        // return if batchHandle changed
        public bool ExtendInstanceCount(ref LODGroupBatchHandle lodGroupBatchHandle, int addCount)
        {
            var lodGroupID = lodGroupBatchHandle.m_BatchLODGroupID;
            int currentCount = m_LODGroups[lodGroupID].InstanceCount;
            int targetCount = currentCount + addCount;
            int maxCount = m_LODGroups[lodGroupID].m_BatchDescription.MaxInstanceCount;
            if (maxCount >= targetCount)
            {
                // need reset instance count in dataBuffer via logic in BatchHanle.
                return false;
            }
            else
            {
                lodGroupBatchHandle = ResizeBatchBuffers(ref lodGroupID, targetCount);
                return true;
            }
        }

        private unsafe LODGroupBatchHandle ResizeBatchBuffers(ref BatchLODGroupID batchLODGroupID, int targetCount)
        {
            targetCount = math.ceilpow2(targetCount);

            GetBatchLODGroup(batchLODGroupID, out BatchLODGroup oldBatchLODGroup);
            BatchDescription newBatchDescription = BatchDescription.CopyWithResize(in oldBatchLODGroup.m_BatchDescription, targetCount);

            // unregister. Need unregister before copy ctor, because 'm_IsRegistered' flag also will be copied.
            int removeBatchCount = oldBatchLODGroup.Unregister(m_BatchRendererGroup, false);
            // m_TotalBatchCount -= removeBatchCount;// m_TotalBatchCount should not be changed.

            // no need copy graphics buffer data, LODGroupBatchHandle.Upload() will flush all data to graphics buffer
            GraphicsBuffer newGraphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, newBatchDescription.TotalBufferSize);
            BatchLODGroup newBatchLODGroup = new BatchLODGroup(this, ref oldBatchLODGroup, in newBatchDescription, in batchLODGroupID);

            //dispose data
            oldBatchLODGroup.Dispose();
            m_GraphicsBuffers.Remove(batchLODGroupID);
            m_LODGroups.Remove(batchLODGroupID);
            if (m_GraphicsBuffers.Remove(batchLODGroupID, out var graphicsBuffer))
                graphicsBuffer.Dispose();

            // register new batches
            int addBatchCount = newBatchLODGroup.Register(m_BatchRendererGroup, newGraphicsBuffer.bufferHandle);
            // m_TotalBatchCount -= removeBatchCount;// m_TotalBatchCount should not be changed.

            m_GraphicsBuffers.Add(batchLODGroupID, newGraphicsBuffer);
            m_LODGroups.Add(batchLODGroupID, newBatchLODGroup);

            //check need resize visible index buffer
            UpdateVisibleIndexDataOffset();


            return new LODGroupBatchHandle(m_ContainerId, newBatchLODGroup.LODGroupID, newBatchLODGroup.LODCount, newBatchLODGroup.GetNativeBuffer(),
                newBatchLODGroup.m_InstanceCount,
                ref newBatchDescription);
        }

        public void Dispose()
        {
            foreach (var group in m_LODGroups)
            {
                group.Value.Unregister(m_BatchRendererGroup);
                group.Value.Dispose();
            }

            m_LODGroups.Dispose();
            m_BatchRendererGroup.Dispose();
            m_VisibleInstanceIndexData.Dispose();

            foreach (var graphicsBuffer in m_GraphicsBuffers.Values)
                graphicsBuffer.Dispose();
            m_GraphicsBuffers.Clear();

            m_MainCamera = null;
            m_WorldOffset = float3.zero;

            m_Containers.TryRemove(m_ContainerId, out _);
        }

        private unsafe void UpdateVisibleIndexDataOffset()
        {
            if (!needForceUpdateVisibleInstanceIndexData && (visibleInstanceIndexRealTotalCount >= m_VisibleInstanceIndexTotalCount))
            {
                return;
            }

            int startOffset = 0;
            int totalLength = 0;
            var keyArray = m_LODGroups.GetKeyArray(Allocator.Temp);
            for (int index = 0; index < keyArray.Length; index++)
            {
                var batchLODGroupID = keyArray[index];
                m_LODGroups.TryGetValue(batchLODGroupID, out var lodGroup);
                int maxCount = lodGroup.m_VisibleInstanceIndexMaxCount;
                for (int lodIndex = 0; lodIndex < lodGroup.LODCount; lodIndex++)
                {
                    ref var batchLOD = ref UnsafeUtility.ArrayElementAsRef<BatchLOD>(lodGroup.m_BatchLODs, lodIndex);
                    batchLOD.m_VisibleInstanceIndexStartIndex = startOffset;
                    totalLength += maxCount;
                    startOffset += maxCount;
                }

                m_LODGroups[batchLODGroupID] = lodGroup;
            }

            needForceUpdateVisibleInstanceIndexData = false;
            int allocateSize = math.ceilpow2(totalLength);
            m_VisibleInstanceIndexTotalCount = totalLength;
            m_VisibleInstanceIndexStartOffset = startOffset;

            if (visibleInstanceIndexRealTotalCount >= allocateSize)
            {
                // Debug.LogError($"UpdateVisibleIndexDataOffset: tidy without resize");
            }
            else
            {
                visibleInstanceIndexRealTotalCount = allocateSize;
                m_VisibleInstanceIndexData.Dispose();
                m_VisibleInstanceIndexData = new NativeArray<int>(allocateSize, Allocator.Persistent);
                // Debug.LogError($"UpdateVisibleIndexDataOffset: resize visible instance index buffer size to: [{allocateSize}]");
            }
        }

        public void NeedForceUpdateVisibleInstanceIndexData()
        {
            needForceUpdateVisibleInstanceIndexData = true;
        }

        #region Static APIs

        internal static void UploadCallback(ContainerID containerID, BatchLODGroupID batchLODGroupID, NativeArray<float4> data, int nativeBufferStartIndex,
            int graphicsBufferStartIndex, int count)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return;

            container.m_GraphicsBuffers[batchLODGroupID].SetData(data, nativeBufferStartIndex, graphicsBufferStartIndex, count);
        }

        internal static void DestroyBatchLODGroup(ContainerID containerID, BatchLODGroupID batchLODGroupID)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return;

            if (container.m_LODGroups.TryGetValue(batchLODGroupID, out var batchLODGroup))
            {
                container.m_LODGroups.Remove(batchLODGroupID);
                int removeBatchCount = batchLODGroup.Unregister(container.m_BatchRendererGroup);
                container.m_TotalBatchCount -= removeBatchCount;
                batchLODGroup.Dispose();
            }

            if (container.m_GraphicsBuffers.Remove(batchLODGroupID, out var graphicsBuffer))
                graphicsBuffer.Dispose();
        }

        internal static bool IsActive(ContainerID containerID, BatchLODGroupID batchLODGroupID)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return false;

            return container.m_LODGroups.ContainsKey(batchLODGroupID);
        }

        internal static bool IsActive(ContainerID containerID, BatchLODGroupID batchLODGroupID, int index)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID, out BatchLODGroup batchLODGroup))
            {
                return batchLODGroup.IsActive(index);
            }

            return false;
        }

        internal static bool IsActive(ContainerID containerID, BatchLODGroupID batchLODGroupID, int index, uint lod)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID, out BatchLODGroup batchLODGroup))
            {
                return batchLODGroup.IsActive(index, lod);
            }

            return false;
        }

        internal static void SetInactive(ContainerID containerID, BatchLODGroupID batchLODGroupID, int index)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID, out BatchLODGroup batchLODGroup))
            {
                batchLODGroup.SetInactive(index);
            }
        }

        internal static void SetActive(ContainerID containerID, BatchLODGroupID batchLODGroupID, int index, uint lod, bool active)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID, out BatchLODGroup batchLODGroup))
            {
                batchLODGroup.SetActive(index, lod, active);
            }
        }

        internal static int AddActiveInstance(ContainerID containerID, BatchLODGroupID batchLODGroupID, ref LODGroupBatchHandle lodGroupBatchHandle)
        {
            if (!m_Containers.TryGetValue(containerID, out BRGContainer container))
            {
                return -1;
            }

            if (!container.m_LODGroups.TryGetValue(batchLODGroupID, out BatchLODGroup batchLODGroup))
                return -1;

            (int index, EGetNextActiveIndexInfo info) = batchLODGroup.GetNextActiveIndex();

            if (info == EGetNextActiveIndexInfo.None)
            {
            }
            else if (info == EGetNextActiveIndexInfo.NeedExtentInstanceCount)
            {
                bool batchHandleChanged = container.ExtendInstanceCount(ref lodGroupBatchHandle, 1);
                lodGroupBatchHandle.IncreaseInstanceCount();
            }
            else if (info == EGetNextActiveIndexInfo.NeedResize)
            {
                bool batchHandleChanged = container.ExtendInstanceCount(ref lodGroupBatchHandle, 1);
                batchLODGroup = container.GetBatchLODGroup(lodGroupBatchHandle.m_BatchLODGroupID);
                lodGroupBatchHandle.IncreaseInstanceCount();
                (index, info) = batchLODGroup.GetNextActiveIndex();
            }

            return index;
        }

        private static bool GetBatchLODGroup(ContainerID containerID, BatchLODGroupID batchLODGroupID, out BatchLODGroup batchLODGroup)
        {
            if (!m_Containers.TryGetValue(containerID, out BRGContainer container))
            {
                batchLODGroup = default;
                return false;
            }

            if (!container.m_LODGroups.TryGetValue(batchLODGroupID, out batchLODGroup))
                return false;

            return true;
        }

        internal static bool IsLODDataInitialized(ContainerID containerID, BatchLODGroupID batchLODGroupID, uint lodIndex)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID, out BatchLODGroup batchLODGroup))
            {
                return batchLODGroup[lodIndex].IsInitialied;
            }

            return false;
        }

        internal static bool RegisterLODData(ContainerID containerID, BatchLODGroupID batchLODGroupID, in RendererDescription rendererDescription, uint lodIndex, Mesh mesh, Material[] materials)
        {
            if (!m_Containers.TryGetValue(containerID, out BRGContainer container))
                return false;

            if (!container.m_LODGroups.TryGetValue(batchLODGroupID, out var batchLODGroup))
                return false;

            container.AddLODData(in batchLODGroupID, (int)lodIndex, mesh, materials, in rendererDescription);
            return true;
        }

        #endregion
    }
}