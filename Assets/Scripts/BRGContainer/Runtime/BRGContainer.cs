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
        
        public unsafe BatchHandle AddBatch(ref BatchDescription batchDescription, [NotNull] Mesh mesh, ushort subMeshIndex, [NotNull] Material material, in RendererDescription rendererDescription)
        {
            GraphicsBuffer graphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, batchDescription.TotalBufferSize);
            BatchRendererData rendererData = CreateRendererData(rendererDescription, mesh, subMeshIndex, material);

            CreateBatchLODGroupID(out BatchLODGroupID batchLODGroupID);
            BatchLODGroup batchLODGroup = CreateBatchLODGroup(ref batchDescription, ref rendererData, in batchLODGroupID, Allocator.Persistent);
            
            batchLODGroup.Register(m_BatchRendererGroup, graphicsBuffer.bufferHandle);
            
            m_GraphicsBuffers.Add(batchLODGroupID, graphicsBuffer);
            m_LODGroups.Add(batchLODGroupID, batchLODGroup);

            return new BatchHandle(m_ContainerId, batchLODGroupID, batchLODGroup.GetNativeBuffer(), batchLODGroup.m_InstanceCount, ref batchDescription);
        }


        public void RemoveBatch(in BatchHandle batchHandle)
        {
            DestroyBatch(m_ContainerId, batchHandle.m_BatchLODGroupID);
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
        // public bool ExtendInstanceCount(ref BatchHandle batchHandle, int addCount)
        // {
        //     BatchID batchID = batchHandle.m_BatchId;
        //     int currentCount = m_Groups[batchID].InstanceCount;
        //     int targetCount = currentCount + addCount;
        //     int maxCount = m_Groups[batchID].m_BatchDescription.MaxInstanceCount;
        //     if (maxCount >= targetCount)
        //     {
        //         // need reset instance count in dataBuffer via logic in BatchHanle.
        //         return false;
        //     }
        //     else
        //     {
        //         // batchHandle = ResizeBatchBuffers(ref batchID, targetCount);
        //         return true;
        //     }
        // }

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
                batchGroup.Unregister(container.m_BatchRendererGroup);
                batchGroup.Dispose();
            }

            if (container.m_GraphicsBuffers.Remove(batchLODGroupID, out var graphicsBuffer))
                graphicsBuffer.Dispose();
        }

        internal static bool IsAlive(ContainerID containerID, BatchLODGroupID batchLODGroupID)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return false;

            return container.m_LODGroups.ContainsKey(batchLODGroupID);
        }
        
        internal static bool IsAlive(ContainerID containerID, BatchLODGroupID batchLODGroupID, int index)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID, out BatchLODGroup batchLODGroup))
            {
                return batchLODGroup.IsAlive(index);
            }

            return false;
        }
        
        internal static void SetAlive(ContainerID containerID, BatchLODGroupID batchLODGroupID, int index, bool alive)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID,out BatchLODGroup batchLODGroup))
            {
                batchLODGroup.SetAlive(index, alive);
            }
        }

        internal static void SetAlive(ContainerID containerID, BatchLODGroupID batchLODGroupID, int index, uint lod, bool alive)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID,out BatchLODGroup batchLODGroup))
            {
                batchLODGroup.SetAlive(index, lod, alive);
            }
        }

        internal static void SetPosition(ContainerID containerID, BatchLODGroupID batchLODGroupID, int index, float3 position)
        {
            if (GetBatchLODGroup(containerID, batchLODGroupID, out BatchLODGroup batchLODGroup))
            {
                batchLODGroup.SetPosition(index, position);
            }
        }

        internal static int AddAliveInstance(ContainerID containerID, BatchLODGroupID batchLODGroupID, ref BatchHandle batchHandle)
        {
            if (!m_Containers.TryGetValue(containerID, out BRGContainer container))
            {
                return -1;
            }

            if (!container.m_LODGroups.TryGetValue(batchLODGroupID, out BatchLODGroup batchLODGroup))
                return -1;

            (int index, EGetNextAliveIndexInfo info) = batchLODGroup.GetNextAliveIndex();

            if (info == EGetNextAliveIndexInfo.None)
            {
            }
            else if (info == EGetNextAliveIndexInfo.NeedExtentInstanceCount)
            {
                bool batchHandleChanged = container.ExtendInstanceCount(ref batchHandle, 1);
                batchHandle.IncreaseInstanceCount();
            }
            else if (info == EGetNextAliveIndexInfo.NeedResize)
            {
                bool batchHandleChanged = container.ExtendInstanceCount(ref batchHandle, 1);
                batchLODGroup = container.GetBatchLODGroup(batchHandle.m_BatchLODGroupID);
                batchHandle.IncreaseInstanceCount();
                (index, info) = batchLODGroup.GetNextAliveIndex();
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
        #endregion
    }
}