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
    [BRGClassThreadSafe]
    public sealed partial class BRGContainer : IDisposable
    {
        //global static data   
        [BRGValueThreadSafe] private static readonly ConcurrentDictionary<ContainerID, BRGContainer> m_Containers; //each view holds a single BRG Container
        private static long m_ContainerGlobalID;

        //per group data
        private readonly BatchRendererGroup m_BatchRendererGroup;
        private readonly ContainerID m_ContainerId;
        private readonly Dictionary<BatchID, GraphicsBuffer> m_GraphicsBuffers;
        private readonly NativeParallelHashMap<BatchID, BatchGroup> m_Groups;

        private Camera m_Camera;


        //static
        static BRGContainer()
        {
            m_Containers = new ConcurrentDictionary<ContainerID, BRGContainer>();
        }

        private BRGContainer()
        {
            m_ContainerId = new ContainerID(Interlocked.Increment(ref m_ContainerGlobalID));
            m_BatchRendererGroup = new BatchRendererGroup(CullingCallback, IntPtr.Zero);
            m_GraphicsBuffers = new Dictionary<BatchID, GraphicsBuffer>();
            m_Groups = new NativeParallelHashMap<BatchID, BatchGroup>(1, Allocator.Persistent);

            m_Containers.TryAdd(m_ContainerId, this);
        }

        public BRGContainer(Bounds bounds) : this()
        {
            m_BatchRendererGroup.SetGlobalBounds(bounds);
        }

        public void SetCamera(Camera camera)
        {
            m_Camera = camera;
        }


        public void SetGlobalBounds(Bounds bounds)
        {
            // m_BatchRendererGroup.SetGlobalBounds(bounds);
        }

        public unsafe BatchHandle AddBatch(ref BatchDescription batchDescription, [NotNull] Mesh mesh, ushort subMeshIndex, [NotNull] Material material,
            in RendererDescription rendererDescription)
        {
            GraphicsBuffer graphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, batchDescription.TotalBufferSize);
            BatchRendererData rendererData = CreateRendererData(rendererDescription, mesh, subMeshIndex, material);
            BatchGroup batchGroup = CreateBatchGroup(ref batchDescription, ref rendererData, graphicsBuffer.bufferHandle, batchDescription.m_Allocator);

            BatchID batchId = batchGroup[0];
            m_GraphicsBuffers.Add(batchId, graphicsBuffer);
            m_Groups.Add(batchId, batchGroup);

            return new BatchHandle(m_ContainerId, batchId, batchGroup.GetNativeBuffer(), batchGroup.m_InstanceCount, ref batchDescription);
        }

        public unsafe BatchHandle AddBatch(ref BatchGroup batchGroup, ref BatchDescription batchDescription, GraphicsBuffer graphicsBuffer)
        {
            // GraphicsBuffer graphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, batchDescription.TotalBufferSize);
            batchGroup.Register(m_BatchRendererGroup, graphicsBuffer.bufferHandle);

            BatchID batchId = batchGroup[0];
            m_GraphicsBuffers.Add(batchId, graphicsBuffer);
            m_Groups.Add(batchId, batchGroup);

            return new BatchHandle(m_ContainerId, batchId, batchGroup.GetNativeBuffer(), batchGroup.m_InstanceCount, ref batchDescription);
        }


        public void RemoveBatch(in BatchHandle batchHandle)
        {
            DestroyBatch(m_ContainerId, batchHandle.m_BatchId);
        }

        public void GetBatchData(in BatchHandle batchHandle, out BatchDescription batchDescription, out BatchRendererData batchRendererData)
        {
            if (!m_Groups.TryGetValue(batchHandle.m_BatchId, out var batchGroup))
                throw new InvalidOperationException("Batch handle is not alive.");

            batchDescription = batchGroup.m_BatchDescription;
            batchRendererData = batchGroup.BatchRendererData;
        }

        public BatchGroup GetBatchGroup(BatchID id)
        {
            return m_Groups[id];
        }

        // return if batchHandle changed
        public bool ExtendInstanceCount(ref BatchHandle batchHandle, int addCount)
        {
            BatchID batchID = batchHandle.m_BatchId;
            int currentCount = m_Groups[batchID].InstanceCount;
            int targetCount = currentCount + addCount;
            int maxCount = m_Groups[batchID].m_BatchDescription.MaxInstanceCount;
            if (maxCount >= targetCount)
            {
                // need reset instance count in dataBuffer via logic in BatchHanle.
                return false;
            }
            else
            {
                batchHandle = ResizeBatchBuffers(ref batchID, targetCount);
                return true;
            }
        }

        private BatchHandle ResizeBatchBuffers(ref BatchID batchID, int targetCount)
        {
            targetCount = math.ceilpow2(targetCount);

            BatchGroup batchGroup = GetBatchGroup(batchID);
            BatchDescription newBatchDescription = BatchDescription.CopyWithResize(ref batchGroup.m_BatchDescription, targetCount);
            BatchGroup newBatchGroup = new BatchGroup(ref batchGroup, ref newBatchDescription);

            // no need copy graphics buffer data, BatchHandles.Upload() will flush all data to graphics buffer
            GraphicsBuffer newGraphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, newBatchDescription.TotalBufferSize);

            DestroyBatch(m_ContainerId, batchID, true);
            return AddBatch(ref newBatchGroup, ref newBatchDescription, newGraphicsBuffer);
        }

        public unsafe void UpdateBatchHandle(ref BatchHandle batchHandle)
        {
            BatchDescription batchDescription = batchHandle.Description;
            BatchID batchID = batchHandle.m_BatchId;
            BatchGroup batchGroup = GetBatchGroup(batchID);
            batchHandle = new BatchHandle(m_ContainerId, batchID, batchGroup.GetNativeBuffer(), batchGroup.m_InstanceCount, ref batchDescription);
        }

        public void Dispose()
        {
            foreach (var group in m_Groups)
            {
                group.Value.Unregister(m_BatchRendererGroup, false);
                group.Value.Dispose();
            }

            m_Groups.Dispose();
            m_BatchRendererGroup.Dispose();

            foreach (var graphicsBuffer in m_GraphicsBuffers.Values)
                graphicsBuffer.Dispose();

            m_GraphicsBuffers.Clear();

            m_Containers.TryRemove(m_ContainerId, out _);
        }

        internal static void UploadCallback(ContainerID containerID, BatchID batchID, NativeArray<float4> data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return;

            container.m_GraphicsBuffers[batchID].SetData(data, nativeBufferStartIndex, graphicsBufferStartIndex, count);
        }

        internal static void DestroyBatch(ContainerID containerID, BatchID batchID, bool onlyRemoveBatch = false)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return;

            if (container.m_Groups.TryGetValue(batchID, out var batchGroup))
            {
                container.m_Groups.Remove(batchID);
                batchGroup.Unregister(container.m_BatchRendererGroup, onlyRemoveBatch);
                batchGroup.Dispose();
            }

            if (container.m_GraphicsBuffers.Remove(batchID, out var graphicsBuffer))
                graphicsBuffer.Dispose();
        }

        internal static bool IsAlive(ContainerID containerID, BatchID batchId)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return false;

            return container.m_Groups.ContainsKey(batchId);
        }
        
        internal static bool IsAlive(ContainerID containerID, BatchID batchId, int index)
        {
            if (GetBatchGroup(containerID, batchId, out BatchGroup group))
            {
                return group.IsAlive(index);
            }

            return false;
        }

        internal static void SetAlive(ContainerID containerID, BatchID batchId, int index, bool alive)
        {
            if (GetBatchGroup(containerID, batchId, out BatchGroup group))
            {
                group.SetAlive(index, alive);
            }
        }

        internal static void SetPosition(ContainerID containerID, BatchID batchId, int index, float3 position)
        {
            if (GetBatchGroup(containerID, batchId, out BatchGroup group))
            {
                group.SetPosition(index, position);
            }
        }

        internal static int AddAliveInstance(ContainerID containerID, BatchID batchId, ref BatchHandle batchHandle)
        {
            if (!m_Containers.TryGetValue(containerID, out BRGContainer container))
            {
                return -1;
            }

            if (!container.m_Groups.TryGetValue(batchId, out BatchGroup group))
                return -1;

            (int index, EGetNextAliveIndexInfo info) = group.GetNextAliveIndex();

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
                group = container.GetBatchGroup(batchHandle.m_BatchId);
                batchHandle.IncreaseInstanceCount();
                (index, info) = group.GetNextAliveIndex();
            }
            
            return index;
        }

        private static bool GetBatchGroup(ContainerID containerID, BatchID batchId, out BatchGroup group)
        {
            if (!m_Containers.TryGetValue(containerID, out BRGContainer container))
            {
                group = default;
                return false;
            }

            if (!container.m_Groups.TryGetValue(batchId, out group))
                return false;

            return true;
        }
    }
}