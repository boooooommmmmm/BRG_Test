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

        private readonly Allocator m_Allocator;

        public readonly unsafe int* VisibleArrayPtr() => visibleArray;

        public readonly unsafe BatchGroup this[int index] => m_BatchGroups[index];

        public unsafe BatchLOD(uint lodIndex, int submeshCount, int maxInstanceCount, int* instanceCount, ref BatchDescription description, in BatchRendererData rendererData, Allocator allocator)
        {
            m_Allocator = allocator;
            
            m_LODIndex = lodIndex;
            m_SubmeshCount = submeshCount;
            m_MaxInstanceCount = maxInstanceCount;
            m_InstanceCount = instanceCount;
            
            m_BatchGroups = (BatchGroup*)UnsafeUtility.Malloc(BRGConstants.SizeOfBatchGroup * m_SubmeshCount, BRGConstants.AlignOfBatchGroup, m_Allocator);
            visibleArray = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt * m_MaxInstanceCount, BRGConstants.AlignOfInt, m_Allocator);

            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                m_BatchGroups[submeshIndex] = new BatchGroup(ref description, in rendererData, m_Allocator);
            }
        }

        public unsafe void Dispose()
        {
            for (int submeshIndex = 0; submeshIndex < m_SubmeshCount; submeshIndex++)
            {
                m_BatchGroups[submeshIndex].Dispose();
            }
            UnsafeUtility.Free(m_BatchGroups, m_Allocator);
            UnsafeUtility.Free(visibleArray, m_Allocator);
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