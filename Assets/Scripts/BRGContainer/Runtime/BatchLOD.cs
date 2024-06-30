using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;

namespace BRGContainer.Runtime
{
    public struct BatchLOD
    {
        private readonly uint m_LODIndex;
        internal readonly int m_SubmeshCount;
        private readonly int m_MaxInstanceCount;
        private readonly unsafe BatchGroup* m_BatchGroups;
        private readonly unsafe int* visibleArray;
        private readonly unsafe int* m_InstanceCount;

        private Allocator m_Allocator;

        public readonly unsafe int* VisibleArrayPtr() => visibleArray;

        public readonly unsafe BatchGroup this[int index] => m_BatchGroups[index];

        public unsafe BatchLOD(in BatchDescription description, in RendererDescription rendererDescription, in BatchWorldObjectLODData worldObjectLODData, uint lodIndex, int maxInstanceCount, int* instanceCount,  Allocator allocator)
        {
            m_Allocator = allocator;
            
            m_LODIndex = lodIndex;
            m_SubmeshCount = worldObjectLODData.SubmeshCount;
            m_MaxInstanceCount = maxInstanceCount;
            m_InstanceCount = instanceCount;
            
            m_BatchGroups = (BatchGroup*)UnsafeUtility.Malloc(BRGConstants.SizeOfBatchGroup * m_SubmeshCount, BRGConstants.AlignOfBatchGroup, m_Allocator);
            visibleArray = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt * m_MaxInstanceCount, BRGConstants.AlignOfInt, m_Allocator);

            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                BatchWorldObjectSubMeshData woldObjectSubMeshData = worldObjectLODData[submeshIndex];
                m_BatchGroups[submeshIndex] = new BatchGroup(in description, in rendererDescription, in woldObjectSubMeshData.m_RendererData,  m_Allocator);
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
                UnsafeUtility.Free(visibleArray, m_Allocator);
                m_Allocator = Allocator.Invalid;
            }
        }

        [BurstDiscard]
        internal unsafe void Register(BatchRendererGroup batchRendererGroup, in GraphicsBufferHandle bufferHandle, NativeArray<MetadataValue> metadataValues)
        {
            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                m_BatchGroups[submeshIndex].Register(batchRendererGroup, bufferHandle, metadataValues);
            }
        }
        
        [BurstDiscard]
        internal unsafe void Unregister(BatchRendererGroup batchRendererGroup)
        {
            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                m_BatchGroups[submeshIndex].Unregister(batchRendererGroup);
            }
        }
        
        
    }
}