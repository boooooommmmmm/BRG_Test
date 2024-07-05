using System;
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
        internal /*readonly*/ int m_SubMeshCount;
        private readonly int m_MaxInstanceCount;
        internal /*readonly*/ unsafe BatchGroup* m_BatchGroups;

        internal RendererDescription m_RendererDescription;
        // private readonly unsafe int* visibleArray;
        internal unsafe int* visibleCount;
        private readonly unsafe int* m_InstanceCount;
        internal int m_DrawBatchIndex;
        internal int m_VisibleInstanceIndexStartIndex;
        private bool m_IsInitialized;

        private Allocator m_Allocator;

        // public readonly unsafe int* VisibleArrayPtr() => visibleArray;
        public readonly bool IsInitialied => m_IsInitialized;
        public readonly uint SubMeshCount => (uint)m_SubMeshCount;
        public readonly unsafe BatchGroup this[int index] => m_BatchGroups[index];
        public readonly unsafe BatchGroup this[uint index] => m_BatchGroups[(uint)index];
        public readonly unsafe BatchGroup* GetByRef(int index) => m_BatchGroups + index;
        public readonly unsafe RendererDescription RendererDescription => m_RendererDescription;
        
        public readonly unsafe int VisibleCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *visibleCount;
        }

        public unsafe BatchLOD(in BatchDescription description, in RendererDescription rendererDescription, in BatchWorldObjectLODData worldObjectLODData, uint lodIndex, int visibleInstanceIndexStartIndex, int* instanceCount,  Allocator allocator)
        {
            m_IsInitialized = true;
            m_Allocator = allocator;
            
            m_LODIndex = lodIndex;
            m_SubMeshCount = worldObjectLODData.SubmeshCount;
            m_MaxInstanceCount = description.MaxInstanceCount;
            m_InstanceCount = instanceCount;
            m_VisibleInstanceIndexStartIndex = visibleInstanceIndexStartIndex;
            m_DrawBatchIndex = 0;
            m_RendererDescription = rendererDescription;
            
            m_BatchGroups = (BatchGroup*)UnsafeUtility.Malloc(BRGConstants.SizeOfBatchGroup * m_SubMeshCount, BRGConstants.AlignOfBatchGroup, m_Allocator);
            // visibleArray = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt * m_MaxInstanceCount, BRGConstants.AlignOfInt, m_Allocator);
            
            visibleCount = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt, BRGConstants.AlignOfInt, m_Allocator);
            UnsafeUtility.MemClear(visibleCount, BRGConstants.SizeOfInt);

            for (int submeshIndex = 0; submeshIndex < m_SubMeshCount; submeshIndex++)
            {
                BatchWorldObjectSubMeshData woldObjectSubMeshData = worldObjectLODData[submeshIndex];
                m_BatchGroups[submeshIndex] = new BatchGroup(in description, in woldObjectSubMeshData.m_RendererData,  m_Allocator);
            }
        }
        
        // create an empty BatchLOD Group, without register mesh and material
        public unsafe BatchLOD(in BatchDescription description, uint lodIndex, int visibleInstanceIndexStartIndex, int* instanceCount,  Allocator allocator)
        {
            m_IsInitialized = false;
            m_Allocator = allocator;
            
            m_LODIndex = lodIndex;
            m_SubMeshCount = BRGConstants.DefaultSubMeshCount;
            m_MaxInstanceCount = description.MaxInstanceCount;
            m_InstanceCount = instanceCount;
            m_VisibleInstanceIndexStartIndex = visibleInstanceIndexStartIndex;
            m_DrawBatchIndex = 0;
            m_RendererDescription = default;
            
            m_BatchGroups = (BatchGroup*)0; // nullptr, allocate memory and create batch group latter.
            
            visibleCount = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt, BRGConstants.AlignOfInt, m_Allocator);
            UnsafeUtility.MemClear(visibleCount, BRGConstants.SizeOfInt);
        }
        
        
        // copy ctor for resizing BatchLOD
        public unsafe BatchLOD(in BatchLOD oldBatchLOD, in BatchDescription newBatchDescription, int* instanceCount)
        {
            m_IsInitialized = oldBatchLOD.m_IsInitialized;
            m_Allocator = oldBatchLOD.m_Allocator;
            
            m_LODIndex = oldBatchLOD.m_LODIndex;
            m_SubMeshCount = oldBatchLOD.m_SubMeshCount;
            m_MaxInstanceCount = newBatchDescription.MaxInstanceCount;
            m_InstanceCount = instanceCount;
            m_VisibleInstanceIndexStartIndex = oldBatchLOD.m_VisibleInstanceIndexStartIndex;
            m_DrawBatchIndex = oldBatchLOD.m_DrawBatchIndex;
            m_RendererDescription = oldBatchLOD.m_RendererDescription;
            
            // visibleArray = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt * m_MaxInstanceCount, BRGConstants.AlignOfInt, m_Allocator);
            
            visibleCount = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt, BRGConstants.AlignOfInt, m_Allocator);
            UnsafeUtility.MemCpy(visibleCount, oldBatchLOD.visibleCount, BRGConstants.SizeOfInt);

            if (m_IsInitialized)
            {
                m_BatchGroups = (BatchGroup*)UnsafeUtility.Malloc(BRGConstants.SizeOfBatchGroup * m_SubMeshCount, BRGConstants.AlignOfBatchGroup, m_Allocator);
                for (int submeshIndex = 0; submeshIndex < m_SubMeshCount; submeshIndex++)
                {
                    m_BatchGroups[submeshIndex] = new BatchGroup(in oldBatchLOD.m_BatchGroups[submeshIndex], in newBatchDescription); // create a new BatchGroup
                }
            }
            else
            {
                m_BatchGroups = (BatchGroup*)0; // keep nullptr
            }

        }

        public unsafe void Dispose()
        {
            if (m_IsInitialized)
            {
                for (int submeshIndex = 0; submeshIndex < m_SubMeshCount; submeshIndex++)
                {
                    m_BatchGroups[submeshIndex].Dispose();
                }
            }

            if (m_Allocator > Allocator.None)
            {
                UnsafeUtility.Free(m_BatchGroups, m_Allocator);
                // UnsafeUtility.Free(visibleArray, m_Allocator);
                UnsafeUtility.Free(visibleCount, m_Allocator);
                // m_InstanceCount will be released via BatchLODGroup.
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
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!m_IsInitialized)
                throw new Exception("Batch LOD is not initialized before registering to BRG!");
#endif
            int registerCount = 0;
            for (int submeshIndex = 0; submeshIndex < m_SubMeshCount; submeshIndex++)
            {
                registerCount += m_BatchGroups[submeshIndex].Register(batchRendererGroup, bufferHandle, metadataValues);
            }

            return registerCount;
        }
        
        [BurstDiscard]
        internal unsafe int Unregister(BatchRendererGroup batchRendererGroup, bool needUnregisterMeshAndMat)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!m_IsInitialized)
                throw new Exception("Batch LOD is not initialized before unregistering to BRG!");
#endif
            int removeBatchCount = 0;
            for (int submeshIndex = 0; submeshIndex < m_SubMeshCount; submeshIndex++)
            {
                removeBatchCount += m_BatchGroups[submeshIndex].Unregister(batchRendererGroup, needUnregisterMeshAndMat);
            }

            return removeBatchCount;
        }

        internal unsafe int SetRenderDataAndRegister(BatchRendererGroup batchRendererGroup, in BatchDescription batchDescription, GraphicsBufferHandle bufferHandle, NativeArray<MetadataValue> metadataValues, in RendererDescription rendererDescription, Mesh mesh, Material[] materials, int subMeshCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_IsInitialized)
                throw new Exception("Batch LOD is initialized before SetRenderDataAndRegister!");
#endif
            
            m_IsInitialized = true;
            m_SubMeshCount = subMeshCount;
            m_RendererDescription = rendererDescription;
            m_BatchGroups = (BatchGroup*)UnsafeUtility.Malloc(BRGConstants.SizeOfBatchGroup * m_SubMeshCount, BRGConstants.AlignOfBatchGroup, m_Allocator);

            int registedBatchCount = 0;
            for (uint subMeshIndex = 0; subMeshIndex < m_SubMeshCount; subMeshIndex++)
            {
                BatchGroup batchGroup = new BatchGroup(in batchDescription, m_Allocator);
                registedBatchCount += batchGroup.SetRenderDataAndRegister(batchRendererGroup, bufferHandle, metadataValues, rendererDescription, mesh, materials[subMeshIndex], subMeshIndex);
                m_BatchGroups[subMeshIndex] = batchGroup;
            }

            return registedBatchCount;
        }
    }
}