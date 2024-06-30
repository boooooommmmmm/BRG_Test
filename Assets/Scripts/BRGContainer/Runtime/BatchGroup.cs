// data for jobs

namespace BRGContainer.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Rendering;

    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay("Count = {WindowCount}, InstanceCount = {InstanceCount}")]
    public struct BatchGroup : INativeDisposable, IEnumerable<BatchID>
    {
        private static int s_SizeOfBatchID = UnsafeUtility.SizeOf<BatchID>();

        internal BatchDescription m_BatchDescription;
        public BatchRendererData BatchRendererData;

        [NativeDisableUnsafePtrRestriction] private unsafe BatchID* m_Batches; // for support multiple windows

        public readonly int WindowCount;
        private readonly int m_BufferLength;
        private Allocator m_Allocator;
        private readonly uint m_SubmeshIndex;
        
        public readonly unsafe BatchID this[int index] => m_Batches[index];
        
        public unsafe BatchGroup(in BatchDescription batchDescription, in RendererDescription rendererDescription, in BatchRendererData rendererData, Allocator allocator)
        {
            m_BatchDescription = batchDescription;
            BatchRendererData = rendererData;
            m_Allocator = allocator;
            m_SubmeshIndex = rendererData.SubMeshIndex;

            m_BufferLength = m_BatchDescription.TotalBufferSize / 16;
            WindowCount = m_BatchDescription.WindowCount;

            m_Batches = (BatchID*)UnsafeUtility.Malloc(s_SizeOfBatchID * m_BufferLength,
                UnsafeUtility.AlignOf<BatchID>(), m_Allocator);
        }

        // copy ctor for resizing batchGroup
        // public unsafe BatchGroup(ref BatchGroup _batchGroup, ref BatchDescription description)
        // {
        //     m_BatchDescription = description;
        //     BatchRendererData = _batchGroup.BatchRendererData;
        //     m_BufferLength = m_BatchDescription.TotalBufferSize / 16;
        //     WindowCount = m_BatchDescription.WindowCount;
        //
        //     m_Allocator = _batchGroup.m_Allocator;
        //
        //     m_InstanceCount = (int*)UnsafeUtility.Malloc(s_SizeOfInt, UnsafeUtility.AlignOf<int>(), m_Allocator);
        //     UnsafeUtility.MemCpy(m_InstanceCount, _batchGroup.m_InstanceCount, UnsafeUtility.SizeOf<int>());
        //     m_AliveCount = (int*)UnsafeUtility.Malloc(s_SizeOfInt, UnsafeUtility.AlignOf<int>(), m_Allocator);
        //     UnsafeUtility.MemCpy(m_AliveCount, _batchGroup.m_AliveCount, UnsafeUtility.SizeOf<int>());
        //
        //     // resize buffers
        //     int lastMaxCount = _batchGroup.m_BatchDescription.MaxInstanceCount;
        //     int lastBufferLength = _batchGroup.m_BufferLength;
        //     int currentMaxCount = m_BatchDescription.MaxInstanceCount;
        //     int currentBufferLength = m_BufferLength;
        //     m_DataBuffer = (float4*)UnsafeUtility.Malloc(s_SizeOfFloat4 * currentBufferLength, UnsafeUtility.AlignOf<float4>(), m_Allocator);
        //     UnsafeUtility.MemClear(m_DataBuffer, s_SizeOfFloat4 * currentBufferLength);
        //     m_Batches = (BatchID*)UnsafeUtility.Malloc(s_SizeOfBatchID * currentBufferLength, UnsafeUtility.AlignOf<BatchID>(), m_Allocator);
        //     m_Visibles = (int*)UnsafeUtility.Malloc(s_SizeOfInt * currentMaxCount, UnsafeUtility.AlignOf<int>(), m_Allocator);
        //     m_State = (uint*)UnsafeUtility.Malloc(s_SizeOfUint * currentMaxCount,UnsafeUtility.AlignOf<uint>(), m_Allocator);
        //     m_Alives = (bool*)UnsafeUtility.Malloc(s_SizeOfBool * currentMaxCount,UnsafeUtility.AlignOf<bool>(), m_Allocator);
        //     UnsafeUtility.MemClear(m_Alives, s_SizeOfBool * currentMaxCount);
        //
        //     BatchDescription oldBatchDescription = _batchGroup.m_BatchDescription;
        //     for (int i = 0, lastDataOffset = 0, newDataOffset = 0; i < oldBatchDescription.MetadataLength; i++)
        //     {
        //         MetadataValue oldValue = oldBatchDescription[i];
        //         MetadataInfo oldInfo = oldBatchDescription.GetMetadataInfo(oldValue.NameID);
        //         int size = oldInfo.Size;
        //         
        //         UnsafeUtility.MemCpy(m_DataBuffer + newDataOffset, _batchGroup.m_DataBuffer + lastDataOffset, size * lastMaxCount);
        //
        //         newDataOffset += size * currentMaxCount / s_SizeOfFloat4;
        //         lastDataOffset += size * lastMaxCount / s_SizeOfFloat4;
        //     }
        //
        //     UnsafeUtility.MemCpy(m_Batches, _batchGroup.m_Batches, s_SizeOfBatchID * lastBufferLength);
        //     UnsafeUtility.MemCpy(m_Visibles, _batchGroup.m_Visibles, s_SizeOfInt * lastMaxCount);
        //     UnsafeUtility.MemCpy(m_State, _batchGroup.m_State, s_SizeOfUint * lastMaxCount);
        //     UnsafeUtility.MemCpy(m_Alives, _batchGroup.m_Alives, s_SizeOfBool * lastMaxCount);
        //
        //     // _batchGroup.Dispose();
        // }
        

        [BurstDiscard]
        public unsafe void Register([NotNull] BatchRendererGroup batchRendererGroup, GraphicsBufferHandle bufferHandle, NativeArray<MetadataValue> metadataValues)
        {
            for (var i = 0; i < m_BatchDescription.WindowCount; i++)
            {
                var offset = (uint)(i * m_BatchDescription.AlignedWindowSize);
                var batchId = batchRendererGroup.AddBatch(metadataValues, bufferHandle, offset, m_BatchDescription.WindowSize);
                m_Batches[i] = batchId;
            }
        }
        
        [BurstDiscard]
        public unsafe void Unregister(BatchRendererGroup batchRendererGroup)
        {
            for (var i = 0; i < WindowCount; i++)
            {
                batchRendererGroup.RemoveBatch(m_Batches[i]);
                
                // notify unity brg to unregister mesh and mat 
                if (BatchRendererData.MeshID != BatchMeshID.Null)
                    batchRendererGroup.UnregisterMesh(BatchRendererData.MeshID);
                if (BatchRendererData.MaterialID != BatchMaterialID.Null)
                    batchRendererGroup.UnregisterMaterial(BatchRendererData.MaterialID);
            }
        }
        
        public unsafe void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Allocator == Allocator.Invalid)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} can not be Disposed because it was not allocated with a valid allocator.");
            if ((IntPtr)m_Batches == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
#endif

            if (m_Allocator > Allocator.None)
            {
                UnsafeUtility.Free(m_Batches, m_Allocator);

                // m_BatchDescription.Dispose(); // released by BatchLODGroup
                BatchRendererData.Dispose();

                m_Allocator = Allocator.Invalid;
            }

            m_Batches = null;
        }

        // @TODO:
        public unsafe JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Allocator == Allocator.Invalid)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} can not be Disposed because it was not allocated with a valid allocator.");
            if ((IntPtr)m_Batches == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
#endif

            if (m_Allocator > Allocator.None)
            {
                var disposeData = new BatchGroupDisposeData
                {
                    Batches = m_Batches,
                    AllocatorLabel = m_Allocator,
                };

                var jobHandle = new BatchGroupDisposeJob(ref disposeData).Schedule(inputDeps);
                
                m_Batches = null;

                m_Allocator = Allocator.Invalid;
                return JobHandle.CombineDependencies(jobHandle, m_BatchDescription.Dispose(inputDeps), BatchRendererData.Dispose(inputDeps));
            }
            
            m_Batches = null;

            return inputDeps;
        }

        #region Get/Set State functions

        #endregion

        #region Enumerator
        public readonly Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<BatchID> IEnumerable<BatchID>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public struct Enumerator : IEnumerator<BatchID>
        {
            private readonly BatchGroup m_BatchGroup;
            private int m_Index;

            public BatchID Current => m_BatchGroup[m_Index];

            object IEnumerator.Current => Current;

            public Enumerator(BatchGroup batchGroup)
            {
                m_BatchGroup = batchGroup;
                m_Index = -1;
            }

            public bool MoveNext()
            {
                ++m_Index;
                return m_Index < m_BatchGroup.WindowCount;
            }

            public void Reset()
            {
                m_Index = -1;
            }

            public void Dispose()
            {
            }
        }
        #endregion
    }
}