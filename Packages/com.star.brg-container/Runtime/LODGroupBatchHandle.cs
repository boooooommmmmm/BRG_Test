using UnityEngine;

namespace BRGContainer.Runtime
{
    using System;
    using System.Threading;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEngine.Rendering;

    /// <summary>
    /// The handle of a batch.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct LODGroupBatchHandle
    {
        private readonly ContainerID m_ContainerId;
        internal readonly BatchLODGroupID m_BatchLODGroupID;

        [NativeDisableContainerSafetyRestriction]
        private readonly NativeArray<float4> m_Buffer;

        [NativeDisableUnsafePtrRestriction] private readonly unsafe int* m_InstanceCount;

        [NativeDisableContainerSafetyRestriction]
        private readonly BatchDescription m_Description;

        private readonly uint m_LODCount;

        private readonly bool isCreated;

        public bool IsCreated => isCreated;
        public bool IsAlive => IsCreated && CheckIfIsAlive(m_ContainerId, m_BatchLODGroupID);
        public unsafe int InstanceCount => (IntPtr)m_InstanceCount == IntPtr.Zero ? 0 : *m_InstanceCount;
        public BatchDescription Description => m_Description;
        public uint LODCount => m_LODCount;

        // [ExcludeFromBurstCompatTesting("BatchHandle creating is unburstable")]
        internal unsafe LODGroupBatchHandle(ContainerID containerId, BatchLODGroupID batchLODGroupID, uint lodCount, NativeArray<float4> buffer, int* instanceCount,
            ref BatchDescription description)
        {
            m_ContainerId = containerId;
            m_BatchLODGroupID = batchLODGroupID;
            m_LODCount = lodCount;

            m_Buffer = buffer;
            m_InstanceCount = instanceCount;
            m_Description = description; //copy ctor

            isCreated = true;
        }

        /// <summary>
        /// Returns <see cref="BatchInstanceDataBuffer"/> instance that provides API for write instance data.
        /// </summary>
        /// <returns>Returns <see cref="BatchInstanceDataBuffer"/> instance.</returns>
        public unsafe BatchInstanceDataBuffer AsInstanceDataBuffer()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsAlive)
                throw new InvalidOperationException("This batch has been destroyed.");
#endif

            return new BatchInstanceDataBuffer(m_Buffer, m_Description.m_MetadataInfoMap, m_Description.m_MetadataValues,
                m_InstanceCount, m_Description.MaxInstanceCount, m_Description.MaxInstancePerWindow, m_Description.AlignedWindowSize / 16);
        }

        /// <summary>
        /// Upload current data to the GPU side.
        /// </summary>
        /// <param name="instanceCount"></param>
        public unsafe void Upload(int instanceCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (instanceCount < 0 || instanceCount > m_Description.MaxInstanceCount)
                throw new ArgumentOutOfRangeException($"{nameof(instanceCount)} must be from 0 to {m_Description.MaxInstanceCount}.");

            if (!IsAlive)
                throw new InvalidOperationException("This batch already has been destroyed.");
#endif

            *m_InstanceCount = instanceCount;

            var completeWindows = instanceCount / m_Description.MaxInstancePerWindow;
            if (completeWindows > 0)
            {
                var size = completeWindows * m_Description.AlignedWindowSize / 16;
                Upload(m_ContainerId, m_BatchLODGroupID, m_Buffer, 0, 0, size);
            }

            var lastBatchId = completeWindows;
            var itemInLastBatch = instanceCount - m_Description.MaxInstancePerWindow * completeWindows;

            if (itemInLastBatch <= 0)
                return;

            var windowOffsetInFloat4 = lastBatchId * m_Description.AlignedWindowSize / 16;

            var offset = 0;
            for (var i = 0; i < m_Description.MetadataLength; i++)
            {
                var metadataValue = m_Description[i];
                var metadataInfo = m_Description.GetMetadataInfo(metadataValue.NameID);
                var startIndex = windowOffsetInFloat4 + m_Description.MaxInstancePerWindow * offset;
                var sizeInFloat4 = metadataInfo.Size / 16;
                offset += sizeInFloat4;

                Upload(m_ContainerId, m_BatchLODGroupID, m_Buffer, startIndex, startIndex,
                    itemInLastBatch * sizeInFloat4);
            }
        }

        public unsafe void SetInstanceCount(int instanceCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (instanceCount < 0 || instanceCount > m_Description.MaxInstanceCount)
                throw new ArgumentOutOfRangeException($"Instance count {instanceCount} out of range from 0 to {m_Description.MaxInstanceCount} (include).");
#endif

            Interlocked.Exchange(ref *m_InstanceCount, instanceCount);
        }

        public unsafe void IncreaseInstanceCount()
        {
            int newInstanceCount = *m_InstanceCount + 1;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (newInstanceCount < 0 || newInstanceCount > m_Description.MaxInstanceCount)
                throw new ArgumentOutOfRangeException($"IncreaseInstanceCount {newInstanceCount} out of range from 0 to {m_Description.MaxInstanceCount} (include).");
#endif

            Interlocked.Exchange(ref *m_InstanceCount, newInstanceCount);
        }

        /// <summary>
        /// Upload current data to the GPU side.
        /// </summary>
        public unsafe void Upload()
        {
            Upload(*m_InstanceCount);
        }

        /// <summary>
        /// Destroy the batch.
        /// </summary>
        // [ExcludeFromBurstCompatTesting("BatchHandle destroying is unburstable")]
        public void Destroy()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!IsAlive)
                throw new InvalidOperationException("This batch already has been destroyed.");
#endif

            Destroy(m_ContainerId, m_BatchLODGroupID);
        }

        #region Container packed APIs
        
        // update graphics buffers (GPU buffer)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Upload(ContainerID containerId, BatchLODGroupID batchLODGroupID, NativeArray<float4> data, int nativeBufferStartIndex, int graphicsBufferStartIndex, int count)
        {
            BRGContainer.UploadCallback(containerId, batchLODGroupID, data, nativeBufferStartIndex, graphicsBufferStartIndex, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Destroy(ContainerID containerId, BatchLODGroupID batchLODGroupID)
        {
            BRGContainer.DestroyBatchLODGroup(containerId, batchLODGroupID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckIfIsAlive(ContainerID containerId, BatchLODGroupID batchLODGroupID)
        {
            return BRGContainer.IsActive(containerId, batchLODGroupID);
        }

        public bool IsInstanceActive(int index)
        {
            return BRGContainer.IsActive(m_ContainerId, m_BatchLODGroupID, index);
        }

        public bool IsInstanceActive(int index, uint lod)
        {
            return BRGContainer.IsActive(m_ContainerId, m_BatchLODGroupID, index, lod);
        }

        public void SetInstanceInactive(int index) // all lod inactive
        {
            BRGContainer.SetInactive(m_ContainerId, m_BatchLODGroupID, index);
        }

        public void SetInstanceActive(int index, uint lod, bool active)
        {
            BRGContainer.SetActive(m_ContainerId, m_BatchLODGroupID, index, lod, active);
        }

        public Tuple<int, bool> AddAliveInstance(ref LODGroupBatchHandle hanlde)
        {
            return BRGContainer.AddActiveInstance(m_ContainerId, m_BatchLODGroupID, ref hanlde);
        }

        public bool IsLODDataInitialized(uint lod)
        {
            return BRGContainer.IsLODDataInitialized(m_ContainerId, m_BatchLODGroupID, lod);
        }

        public bool RegisterLODData(in RendererDescription rendererDescription, uint lod, Mesh mesh, Material[] materials)
        {
            return BRGContainer.RegisterLODData(m_ContainerId, m_BatchLODGroupID, in rendererDescription, lod, mesh, materials);
        }
        #endregion
    }
}