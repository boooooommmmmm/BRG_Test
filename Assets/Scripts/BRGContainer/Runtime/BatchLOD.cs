using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace BRGContainer.Runtime
{
    public struct BatchLOD : INativeDisposable
    {
        private readonly uint m_LODIndex;
        internal readonly int m_SubmeshCount;
        private readonly int m_MaxInstanceCount;
        internal readonly unsafe BatchGroup* m_BatchGroups;
        // private readonly unsafe int* visibleArray;
        internal unsafe int* visibleCount;
        private readonly unsafe int* m_InstanceCount;
        internal int m_DrawBatchIndex;
        internal int m_VisibleInstanceIndexStartIndex;

        private Allocator m_Allocator;

        // public readonly unsafe int* VisibleArrayPtr() => visibleArray;
        public readonly uint SubMeshCount => (uint)m_SubmeshCount;
        public readonly unsafe BatchGroup this[int index] => m_BatchGroups[index];
        public readonly unsafe BatchGroup this[uint index] => m_BatchGroups[(uint)index];
        public readonly unsafe BatchGroup* GetByRef(int index) => m_BatchGroups + index;
        
        public readonly unsafe int VisibleCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *visibleCount;
        }

        public unsafe BatchLOD(in BatchDescription description, in RendererDescription rendererDescription, in BatchWorldObjectLODData worldObjectLODData, uint lodIndex, int visibleInstanceIndexStartIndex, int* instanceCount,  Allocator allocator)
        {
            m_Allocator = allocator;
            
            m_LODIndex = lodIndex;
            m_SubmeshCount = worldObjectLODData.SubmeshCount;
            m_MaxInstanceCount = description.MaxInstanceCount;
            m_InstanceCount = instanceCount;
            m_VisibleInstanceIndexStartIndex = visibleInstanceIndexStartIndex;
            m_DrawBatchIndex = 0;
            
            m_BatchGroups = (BatchGroup*)UnsafeUtility.Malloc(BRGConstants.SizeOfBatchGroup * m_SubmeshCount, BRGConstants.AlignOfBatchGroup, m_Allocator);
            // visibleArray = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt * m_MaxInstanceCount, BRGConstants.AlignOfInt, m_Allocator);
            
            visibleCount = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt, BRGConstants.AlignOfInt, m_Allocator);
            UnsafeUtility.MemClear(visibleCount, BRGConstants.SizeOfInt);

            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                BatchWorldObjectSubMeshData woldObjectSubMeshData = worldObjectLODData[submeshIndex];
                m_BatchGroups[submeshIndex] = new BatchGroup(in description, in rendererDescription, in woldObjectSubMeshData.m_RendererData,  m_Allocator);
            }
        }
        
        // copy ctor for resizing BatchLOD
        public unsafe BatchLOD(in BatchLOD oldBatchLOD, in BatchDescription newBatchDescription, int* instanceCount)
        {
            m_Allocator = oldBatchLOD.m_Allocator;
            
            m_LODIndex = oldBatchLOD.m_LODIndex;
            m_SubmeshCount = oldBatchLOD.m_SubmeshCount;
            m_MaxInstanceCount = newBatchDescription.MaxInstanceCount;
            m_InstanceCount = instanceCount;
            m_VisibleInstanceIndexStartIndex = oldBatchLOD.m_VisibleInstanceIndexStartIndex;
            m_DrawBatchIndex = oldBatchLOD.m_DrawBatchIndex;
            
            m_BatchGroups = (BatchGroup*)UnsafeUtility.Malloc(BRGConstants.SizeOfBatchGroup * m_SubmeshCount, BRGConstants.AlignOfBatchGroup, m_Allocator);
            // visibleArray = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt * m_MaxInstanceCount, BRGConstants.AlignOfInt, m_Allocator);
            
            visibleCount = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt, BRGConstants.AlignOfInt, m_Allocator);
            UnsafeUtility.MemCpy(visibleCount, oldBatchLOD.visibleCount, BRGConstants.SizeOfInt);

            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                m_BatchGroups[submeshIndex] = new BatchGroup(in oldBatchLOD.m_BatchGroups[submeshIndex], in newBatchDescription); // create a new BatchGroup
            }
        }

        public unsafe void Dispose()
        {
            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                m_BatchGroups[submeshIndex].Dispose();
            }

            if (m_Allocator > Allocator.None)
            {
                UnsafeUtility.Free(m_BatchGroups, m_Allocator);
                // UnsafeUtility.Free(visibleArray, m_Allocator);
                UnsafeUtility.Free(visibleCount, m_Allocator);
                m_Allocator = Allocator.Invalid;
            }
        }

        // @TODO
        public JobHandle Dispose(JobHandle inputDeps)
        {
            return inputDeps;
        }

        [BurstDiscard]
        internal unsafe int Register(BatchRendererGroup batchRendererGroup, in GraphicsBufferHandle bufferHandle, NativeArray<MetadataValue> metadataValues)
        {
            int registerCount = 0;
            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                registerCount += m_BatchGroups[submeshIndex].Register(batchRendererGroup, bufferHandle, metadataValues);
            }

            return registerCount;
        }
        
        [BurstDiscard]
        internal unsafe int Unregister(BatchRendererGroup batchRendererGroup, bool needUnregisterMeshAndMat)
        {
            int removeBatchCount = 0;
            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                removeBatchCount += m_BatchGroups[submeshIndex].Unregister(batchRendererGroup, needUnregisterMeshAndMat);
            }

            return removeBatchCount;
        }
        
        
    }
}