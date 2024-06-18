//#define TEMP_TEST_MODE

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
#if ENABLE_IL2CPP
using Il2Cpp;
#endif



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


        //static -> delegate
        private static readonly FunctionPointer<UploadDelegate> m_UploadFunctionPointer;
        private static readonly FunctionPointer<DestroyBatchDelegate> m_DestroyBatchFunctionPointer;
        private static readonly FunctionPointer<IsBatchAliveDelegate> m_IsBatchAliveFunctionPointer;


        //static
        static BRGContainer()
        {
            m_Containers = new ConcurrentDictionary<ContainerID, BRGContainer>();

            m_UploadFunctionPointer = new FunctionPointer<UploadDelegate>(Marshal.GetFunctionPointerForDelegate(new UploadDelegate(UploadCallback)));
            m_DestroyBatchFunctionPointer = new FunctionPointer<DestroyBatchDelegate>(Marshal.GetFunctionPointerForDelegate(new DestroyBatchDelegate(DestroyBatchCallback)));
            m_IsBatchAliveFunctionPointer = new FunctionPointer<IsBatchAliveDelegate>(Marshal.GetFunctionPointerForDelegate(new IsBatchAliveDelegate(IsAliveCallback)));
        }

        private BRGContainer()
        {
            m_ContainerId = new ContainerID(Interlocked.Increment(ref m_ContainerGlobalID));

#if TEMP_TEST_MODE
            m_BatchRendererGroup = new BatchRendererGroup(TempCullingCallback);
#else
            m_BatchRendererGroup = new BatchRendererGroup(CullingCallback, IntPtr.Zero);
#endif

            m_GraphicsBuffers = new Dictionary<BatchID, GraphicsBuffer>();
            m_Groups = new NativeParallelHashMap<BatchID, BatchGroup>(1, Allocator.Persistent);

            m_Containers.TryAdd(m_ContainerId, this);
        }

        public BRGContainer(Bounds bounds) : this()
        {
            #if TEMP_TEST_MODE
            #else
            m_BatchRendererGroup.SetGlobalBounds(bounds);
            #endif
        }

        public void SetCamera(Camera camera)
        {
            m_Camera = camera;
        }


        public void SetGlobalBounds(Bounds bounds)
        {
            // m_BatchRendererGroup.SetGlobalBounds(bounds);
        }

        public unsafe BatchHandle AddBatch(ref BatchDescription batchDescription, [NotNull] Mesh mesh, ushort subMeshIndex, [NotNull] Material material, in RendererDescription rendererDescription)
        {
            GraphicsBuffer graphicsBuffer = CreateGraphicsBuffer(BatchDescription.IsUBO, batchDescription.TotalBufferSize);
            BatchRendererData rendererData = CreateRendererData(rendererDescription, mesh, subMeshIndex, material);
            #if TEMP_TEST_MODE
            BatchGroup batchGroup = CreateBatchGroup(ref batchDescription, ref rendererData, new GraphicsBufferHandle(), batchDescription.m_Allocator);
            #else
            BatchGroup batchGroup = CreateBatchGroup(ref batchDescription, ref rendererData, graphicsBuffer.bufferHandle, batchDescription.m_Allocator);
            #endif

            var batchId = batchGroup[0];
            m_GraphicsBuffers.Add(batchId, graphicsBuffer);
            m_Groups.Add(batchId, batchGroup);

            return new BatchHandle(m_ContainerId, batchId, batchGroup.GetNativeBuffer(), batchGroup.m_InstanceCount,
                ref batchDescription, m_UploadFunctionPointer, m_DestroyBatchFunctionPointer, m_IsBatchAliveFunctionPointer);
        }


        public void RemoveBatch(in BatchHandle batchHandle)
        {
            DestroyBatchCallback(m_ContainerId, batchHandle.m_BatchId);
        }

        public void GetBatchData(in BatchHandle batchHandle, out BatchDescription batchDescription, out BatchRendererData batchRendererData)
        {
            if (!m_Groups.TryGetValue(batchHandle.m_BatchId, out var batchGroup))
                throw new InvalidOperationException("Batch handle is not alive.");

            batchDescription = batchGroup.m_BatchDescription;
            batchRendererData = batchGroup.BatchRendererData;
        }
        
        //sven test
        public BatchGroup GetBatchGroup(BatchID id)
        {
            return m_Groups[id];
        }

        public void Dispose()
        {
            foreach (var group in m_Groups)
            {
                group.Value.Unregister(m_BatchRendererGroup);
                group.Value.Dispose();
            }

            m_Groups.Dispose();
            m_BatchRendererGroup.Dispose();

            foreach (var graphicsBuffer in m_GraphicsBuffers.Values)
                graphicsBuffer.Dispose();

            m_GraphicsBuffers.Clear();

            m_Containers.TryRemove(m_ContainerId, out _);
        }

        [AOT.MonoPInvokeCallback(typeof(UploadDelegate))]
        private static void UploadCallback(ContainerID containerID, BatchID batchID, NativeArray<float4> data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return;

            container.m_GraphicsBuffers[batchID].SetData(data, nativeBufferStartIndex, graphicsBufferStartIndex, count);
        }

        [AOT.MonoPInvokeCallback(typeof(DestroyBatchDelegate))]
        private static void DestroyBatchCallback(ContainerID containerID, BatchID batchID)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return;

            if (container.m_Groups.TryGetValue(batchID, out var batchGroup))
            {
                container.m_Groups.Remove(batchID);
                batchGroup.Unregister(container.m_BatchRendererGroup);
                batchGroup.Dispose();
            }

            if (container.m_GraphicsBuffers.Remove(batchID, out var graphicsBuffer))
                graphicsBuffer.Dispose();
        }

        [AOT.MonoPInvokeCallback(typeof(IsBatchAliveDelegate))]
        private static bool IsAliveCallback(ContainerID containerID, BatchID batchId)
        {
            if (!m_Containers.TryGetValue(containerID, out var container))
                return false;

            return container.m_Groups.ContainsKey(batchId);
        }
    }
}