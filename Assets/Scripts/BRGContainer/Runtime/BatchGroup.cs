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
    public struct BatchGroup : INativeDisposable//, IEnumerable<BatchID>
    {
        // internal BatchDescription m_BatchDescription;
        public BatchRendererData BatchRendererData;

        [NativeDisableUnsafePtrRestriction] private unsafe BatchID* m_Batches; // for support multiple windows

        public readonly int WindowCount;
        public readonly uint WindowSize;
        public readonly int AlignedWindowSize;
        private readonly int m_BufferLength;
        private Allocator m_Allocator;
        private readonly uint m_SubmeshIndex;
        
        public readonly unsafe BatchID this[int index] => m_Batches[index];
        
        public unsafe BatchGroup(in BatchDescription batchDescription, in RendererDescription rendererDescription, in BatchRendererData rendererData, Allocator allocator)
        {
            BatchRendererData = rendererData;
            m_Allocator = allocator;
            m_SubmeshIndex = rendererData.SubMeshIndex;

            m_BufferLength = batchDescription.TotalBufferSize / 16;
            WindowCount = batchDescription.WindowCount;
            WindowSize = batchDescription.WindowSize;
            AlignedWindowSize = batchDescription.AlignedWindowSize;

            m_Batches = (BatchID*)UnsafeUtility.Malloc(BRGConstants.SizeOfBatchID * m_BufferLength,
                UnsafeUtility.AlignOf<BatchID>(), m_Allocator);
        }
        
        // copy ctor for resizing batchGroup
        public unsafe BatchGroup(in BatchGroup oldBatchGroup, in BatchDescription newBatchDescription)
        {
            BatchRendererData = oldBatchGroup.BatchRendererData;
            m_Allocator = oldBatchGroup.m_Allocator;
            m_SubmeshIndex = oldBatchGroup.BatchRendererData.SubMeshIndex;

            m_BufferLength = newBatchDescription.TotalBufferSize / 16;
            WindowCount = newBatchDescription.WindowCount;
            WindowSize = newBatchDescription.WindowSize;
            AlignedWindowSize = newBatchDescription.AlignedWindowSize;

            m_Batches = (BatchID*)UnsafeUtility.Malloc(BRGConstants.SizeOfBatchID * m_BufferLength, UnsafeUtility.AlignOf<BatchID>(), m_Allocator);
        }

        [BurstDiscard]
        public unsafe int Register([NotNull] BatchRendererGroup batchRendererGroup, GraphicsBufferHandle bufferHandle, NativeArray<MetadataValue> metadataValues)
        {
            int registBatchCount = 0;
            for (var i = 0; i < WindowCount; i++)
            {
                var offset = (uint)(i * AlignedWindowSize);
                var batchId = batchRendererGroup.AddBatch(metadataValues, bufferHandle, offset, WindowSize);
                m_Batches[i] = batchId;
                registBatchCount++;
            }

            return registBatchCount;
        }
        
        [BurstDiscard]
        public unsafe int Unregister(BatchRendererGroup batchRendererGroup, bool needUnregisterMeshAndMat) // default true
        {
            int removeBatchCount = 0;
            for (var i = 0; i < WindowCount; i++)
            {
                removeBatchCount++;
                batchRendererGroup.RemoveBatch(m_Batches[i]);

                if (needUnregisterMeshAndMat)
                {
                    // notify unity brg to unregister mesh and mat 
                    if (BatchRendererData.MeshID != BatchMeshID.Null)
                        batchRendererGroup.UnregisterMesh(BatchRendererData.MeshID);
                    if (BatchRendererData.MaterialID != BatchMaterialID.Null)
                        batchRendererGroup.UnregisterMaterial(BatchRendererData.MaterialID);
                }
            }

            return removeBatchCount;
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
            return new JobHandle();
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//             if (m_Allocator == Allocator.Invalid)
//                 throw new InvalidOperationException($"The {nameof(BatchGroup)} can not be Disposed because it was not allocated with a valid allocator.");
//             if ((IntPtr)m_Batches == IntPtr.Zero)
//                 throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
// #endif
//
//             if (m_Allocator > Allocator.None)
//             {
//                 var disposeData = new BatchGroupDisposeData
//                 {
//                     Batches = m_Batches,
//                     AllocatorLabel = m_Allocator,
//                 };
//
//                 var jobHandle = new BatchGroupDisposeJob(ref disposeData).Schedule(inputDeps);
//                 
//                 m_Batches = null;
//
//                 m_Allocator = Allocator.Invalid;
//                 return JobHandle.CombineDependencies(jobHandle, m_BatchDescription.Dispose(inputDeps), BatchRendererData.Dispose(inputDeps));
//             }
//             
//             m_Batches = null;
//
//             return inputDeps;
        }

        #region Get/Set State functions

        #endregion

        // #region Enumerator
        // public readonly Enumerator GetEnumerator()
        // {
        //     return new Enumerator(this);
        // }
        //
        // IEnumerator<BatchID> IEnumerable<BatchID>.GetEnumerator()
        // {
        //     return GetEnumerator();
        // }
        //
        // IEnumerator IEnumerable.GetEnumerator()
        // {
        //     return GetEnumerator();
        // }
        //
        // public struct Enumerator : IEnumerator<BatchID>
        // {
        //     private readonly BatchGroup m_BatchGroup;
        //     private int m_Index;
        //
        //     public BatchID Current => m_BatchGroup[m_Index];
        //
        //     object IEnumerator.Current => Current;
        //
        //     public Enumerator(BatchGroup batchGroup)
        //     {
        //         m_BatchGroup = batchGroup;
        //         m_Index = -1;
        //     }
        //
        //     public bool MoveNext()
        //     {
        //         ++m_Index;
        //         return m_Index < m_BatchGroup.WindowCount;
        //     }
        //
        //     public void Reset()
        //     {
        //         m_Index = -1;
        //     }
        //
        //     public void Dispose()
        //     {
        //     }
        // }
        // #endregion
    }
}