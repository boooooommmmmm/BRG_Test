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
        private readonly NativeParallelHashMap<BatchLODGroupID, BatchLODGroup> m_LODGroups;

        private Camera m_MainCamera;
        private float3 m_WorldOffset = float3.zero;
        internal int m_TotalBatchCount;


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
        public unsafe LODGroupBatchHandle AddLODGroup(ref BatchDescription batchDescription, in RendererDescription rendererDescription, ref  BatchWorldObjectData worldObjectData)
        {
            GraphicsBuffer graphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, batchDescription.TotalBufferSize);

            GetNewBatchLODGroupID(out BatchLODGroupID batchLODGroupID);
            BatchLODGroup batchLODGroup = CreateBatchLODGroup(in batchDescription, in rendererDescription, in batchLODGroupID, ref worldObjectData, Allocator.Persistent);
            
            m_TotalBatchCount += batchLODGroup.Register(m_BatchRendererGroup, graphicsBuffer.bufferHandle);
            
            m_GraphicsBuffers.Add(batchLODGroupID, graphicsBuffer);
            m_LODGroups.Add(batchLODGroupID, batchLODGroup);

            return new LODGroupBatchHandle(m_ContainerId, batchLODGroupID, batchLODGroup.LODCount, batchLODGroup.GetNativeBuffer(), batchLODGroup.m_InstanceCount, ref batchDescription);
        }


        public void RemoveBatch(in LODGroupBatchHandle lodGroupBatchHandle)
        {
            DestroyBatch(m_ContainerId, lodGroupBatchHandle.m_BatchLODGroupID);
        }

        // public void GetBatchData(in BatchHandle batchHandle, out BatchDescription batchDescription, out BatchRendererData batchRendererData)
        // {
        //     if (!m_Groups.TryGetValue(batchHandle.m_BatchId, out var batchGroup))
        //         throw new InvalidOperationException("Batch handle is not alive.");
        //
        //     batchDescription = batchGroup.m_BatchDescription;
        //     batchRendererData = batchGroup.BatchRendererData;
        // }

        public BatchLODGroup GetBatchLODGroup(BatchLODGroupID id)
        {
            return m_LODGroups[id];
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
                // batchHandle = ResizeBatchBuffers(ref batchID, targetCount);
                return true;
            }
        }

        // private BatchHandle ResizeBatchBuffers(ref BatchID batchID, int targetCount)
        // {
        //     targetCount = math.ceilpow2(targetCount);
        //
        //     BatchGroup batchGroup = GetBatchGroup(batchID);
        //     BatchDescription newBatchDescription = BatchDescription.CopyWithResize(ref batchGroup.m_BatchDescription, targetCount);
        //     BatchGroup newBatchGroup = new BatchGroup(ref batchGroup, ref newBatchDescription);
        //
        //     // no need copy graphics buffer data, BatchHandles.Upload() will flush all data to graphics buffer
        //     GraphicsBuffer newGraphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, newBatchDescription.TotalBufferSize);
        //
        //     DestroyBatch(m_ContainerId, batchID, true);
        //     return AddBatch(ref newBatchGroup, ref newBatchDescription, newGraphicsBuffer);
        // }

        // public unsafe void UpdateBatchHandle(ref BatchHandle batchHandle)
        // {
        //     BatchDescription batchDescription = batchHandle.Description;
        //     BatchID batchID = batchHandle.m_BatchId;
        //     BatchGroup batchGroup = GetBatchLODGroup(batchID);
        //     batchHandle = new BatchHandle(m_ContainerId, batchID, batchGroup.GetNativeBuffer(), batchGroup.m_InstanceCount, ref batchDescription);
        // }

        public void Dispose()
        {
            foreach (var group in m_LODGroups)
            {
                group.Value.Unregister(m_BatchRendererGroup);
                group.Value.Dispose();
            }

            m_LODGroups.Dispose();
            m_BatchRendererGroup.Dispose();

            foreach (var graphicsBuffer in m_GraphicsBuffers.Values)
                graphicsBuffer.Dispose();
            m_GraphicsBuffers.Clear();
            
            m_MainCamera = null;
            m_WorldOffset = float3.zero;
            
            m_Containers.TryRemove(m_ContainerId, out _);
        }

        #region Static APIs
        
        internal static void UploadCallback(ContainerID containerID, BatchLODGroupID batchLODGroupID, NativeArray<float4> data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return;

            container.m_GraphicsBuffers[batchLODGroupID].SetData(data, nativeBufferStartIndex, graphicsBufferStartIndex, count);
        }

        internal static void DestroyBatch(ContainerID containerID, BatchLODGroupID batchLODGroupID)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return;

            if (container.m_LODGroups.TryGetValue(batchLODGroupID, out var batchGroup))
            {
                container.m_LODGroups.Remove(batchLODGroupID);
                int removeBatchCount = batchGroup.Unregister(container.m_BatchRendererGroup);
                container.m_TotalBatchCount -= removeBatchCount;
                batchGroup.Dispose();
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
            if (GetBatchLODGroup(containerID, batchLODGroupID,out BatchLODGroup batchLODGroup))
            {
                batchLODGroup.SetInactive(index);
            }
        }

        internal static void SetActive(ContainerID containerID, BatchLODGroupID batchLODGroupID, int index, uint lod, bool active)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID,out BatchLODGroup batchLODGroup))
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
            // else
            // {
            //     throw new Exception("Not support now");
            // }
            else if (info == EGetNextActiveIndexInfo.NeedExtentInstanceCount)
            {
                bool batchHandleChanged = container.ExtendInstanceCount(ref lodGroupBatchHandle, 1);
                lodGroupBatchHandle.IncreaseInstanceCount();
            }
            // else if (info == EGetNextActiveIndexInfo.NeedResize)
            // {
            //     bool batchHandleChanged = container.ExtendInstanceCount(ref batchHandle, 1);
            //     batchLODGroup = container.GetBatchLODGroup(batchHandle.m_BatchLODGroupID);
            //     batchHandle.IncreaseInstanceCount();
            //     (index, info) = batchLODGroup.GetNextActiveIndex();
            // }
            
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
        #endregion
    }
}