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
    [DebuggerDisplay("InstanceCount = {InstanceCount}")]
    public struct BatchLODGroup : INativeDisposable
    {
        private static readonly int s_LODOffset = 0;
        private static readonly int s_ActiveOffset = 3; // 1 << 3 = 8
        private static readonly uint s_bLODMask = 0x7; // 0000 0000 0000 0111
        private static readonly uint s_bActiveMask = 0x8; // 1 << 3
        private static readonly uint s_LODCount = 4u;

        private readonly int m_BufferLength;
        private readonly uint m_LODCount;
        private /*readonly*/ Allocator m_Allocator;
        internal int m_VisibleInstanceIndexMaxCount;
        internal readonly BatchDescription m_BatchDescription;
        // public readonly RendererDescription RendererDescription;
        public BatchLODGroupID LODGroupID;

        [NativeDisableUnsafePtrRestriction] internal unsafe int* m_InstanceCount;
        [NativeDisableUnsafePtrRestriction] private unsafe float4* m_DataBuffer; // o2w, w2o, meta data arrays
        [NativeDisableUnsafePtrRestriction] private unsafe uint* m_State; // 0-3 bit: lod; 4 bit: active state
        [NativeDisableUnsafePtrRestriction] private unsafe HISMAABB* m_AABBs;
        [NativeDisableUnsafePtrRestriction] internal unsafe BatchLOD* m_BatchLODs;
        [NativeDisableUnsafePtrRestriction] internal unsafe int* m_ActiveCount; //

        public readonly unsafe int ActiveCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *m_ActiveCount;
        }

        public readonly uint LODCount => m_LODCount;

        // public readonly unsafe bool IsCreated => (IntPtr)m_DataBuffer != IntPtr.Zero &&
        //                                          (IntPtr)m_Batches != IntPtr.Zero &&
        //                                          (IntPtr)m_InstanceCount != IntPtr.Zero;

        public readonly unsafe uint* StatePtr => m_State;

        public readonly unsafe float4* DataBuffer => m_DataBuffer;

        public readonly BatchDescription BatchDescription => m_BatchDescription;

        // public readonly unsafe BatchLOD this[uint index] => m_BatchLODs[(int)index];
        public readonly unsafe BatchLOD this[uint index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (index >= m_LODCount)
                    throw new Exception($"SetLODRenderDataAndRegister LOD index cannot be larger than [{m_LODCount}], current get: [{index}]");
#endif
                return m_BatchLODs[(int)index];
            }
        }

        public readonly unsafe int InstanceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *m_InstanceCount;
        }

        public unsafe BatchLOD* GetByRef(int index) => m_BatchLODs + index;


        public unsafe BatchLODGroup(BRGContainer container, in BatchDescription batchDescription, in RendererDescription rendererDescription, in BatchLODGroupID batchLODGroupID,
            in BatchWorldObjectData worldObjectData, Allocator allocator, bool isEmptyLODData)
        {
            m_Allocator = allocator;
            m_BatchDescription = batchDescription;
            // RendererDescription = rendererDescription;
            // m_LODCount = (uint)worldObjectData.LODCount;
            m_LODCount = BRGConstants.MaxLODCount; // use default lod count 
            LODGroupID = batchLODGroupID;

            m_BufferLength = m_BatchDescription.TotalBufferSize / 16;

            m_DataBuffer = (float4*)UnsafeUtility.Malloc(BRGConstants.SizeOfFloat4 * m_BufferLength, BRGConstants.AlignOfFloat4, m_Allocator);

            m_InstanceCount = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt, BRGConstants.AlignOfInt, m_Allocator);
            UnsafeUtility.MemClear(m_InstanceCount, BRGConstants.SizeOfInt);
            m_ActiveCount = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt, BRGConstants.AlignOfInt, m_Allocator);
            UnsafeUtility.MemClear(m_ActiveCount, BRGConstants.SizeOfInt);

            int maxCount = m_BatchDescription.MaxInstanceCount;
            m_State = (uint*)UnsafeUtility.Malloc(BRGConstants.SizeOfUint * maxCount, BRGConstants.AlignOfUint, m_Allocator);
            UnsafeUtility.MemClear(m_State, BRGConstants.SizeOfUint * maxCount);
            m_AABBs = (HISMAABB*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<HISMAABB>() * maxCount, UnsafeUtility.AlignOf<HISMAABB>(), m_Allocator);

            // uint lodCount = (uint)Mathf.Min((uint)worldObjectData.LODCount, s_LODCount);
            uint lodCount = (uint)Mathf.Min((uint)m_LODCount, s_LODCount);
            m_BatchLODs = (BatchLOD*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchLOD>() * lodCount, UnsafeUtility.AlignOf<BatchLOD>(), m_Allocator);
            m_VisibleInstanceIndexMaxCount = batchDescription.MaxInstanceCount < BRGConstants.DefaultVisibleInstanceIndexCount
                ? BRGConstants.DefaultVisibleInstanceIndexCount
                : math.ceilpow2(batchDescription.MaxInstanceCount);
            for (uint lodIndex = 0u; lodIndex < lodCount; lodIndex++)
            {
                int startOffset = container.m_VisibleInstanceIndexStartOffset;
                container.m_VisibleInstanceIndexTotalCount += m_VisibleInstanceIndexMaxCount;
                container.m_VisibleInstanceIndexStartOffset += m_VisibleInstanceIndexMaxCount; // offset for next BatchLOD

                if (isEmptyLODData)
                {
                    m_BatchLODs[lodIndex] = new BatchLOD(in batchDescription, lodIndex, startOffset, m_InstanceCount, m_Allocator); // create empty batchLOD.
                }
                else
                {
                    BatchWorldObjectLODData batchWorldObjectLODData = worldObjectData[lodIndex];
                    m_BatchLODs[lodIndex] = new BatchLOD(in batchDescription, in rendererDescription, in batchWorldObjectLODData, lodIndex, startOffset, m_InstanceCount, m_Allocator);
                }
            }
        }

        // copy ctor for resizing BatchLODGroup
        public unsafe BatchLODGroup(BRGContainer container, ref BatchLODGroup oldBatchLODGroup, in BatchDescription newBatchDescription, in BatchLODGroupID batchLODGroupID)
        {
            m_Allocator = oldBatchLODGroup.m_Allocator;
            m_BatchDescription = newBatchDescription;
            // RendererDescription = oldBatchLODGroup.RendererDescription;
            // m_LODCount = (uint)worldObjectData.LODCount;
            m_LODCount = BRGConstants.MaxLODCount; // use default lod count 
            LODGroupID = batchLODGroupID;
            m_VisibleInstanceIndexMaxCount = oldBatchLODGroup.m_VisibleInstanceIndexMaxCount;

            if (newBatchDescription.MaxInstanceCount > oldBatchLODGroup.m_VisibleInstanceIndexMaxCount)
            {
                container.NeedForceUpdateVisibleInstanceIndexData(); // notify container to resize visible index data buffer
                m_VisibleInstanceIndexMaxCount = math.ceilpow2(newBatchDescription.MaxInstanceCount);
            }

            m_BufferLength = m_BatchDescription.TotalBufferSize / 16;

            // resize buffers
            int lastMaxCount = oldBatchLODGroup.m_BatchDescription.MaxInstanceCount;
            int lastBufferLength = oldBatchLODGroup.m_BufferLength;
            int currentMaxCount = m_BatchDescription.MaxInstanceCount;
            int currentBufferLength = m_BufferLength;

            m_InstanceCount = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt, UnsafeUtility.AlignOf<int>(), m_Allocator);
            UnsafeUtility.MemCpy(m_InstanceCount, oldBatchLODGroup.m_InstanceCount, UnsafeUtility.SizeOf<int>());
            m_ActiveCount = (int*)UnsafeUtility.Malloc(BRGConstants.SizeOfInt, BRGConstants.AlignOfInt, m_Allocator);
            UnsafeUtility.MemCpy(m_ActiveCount, oldBatchLODGroup.m_ActiveCount, UnsafeUtility.SizeOf<int>());

            m_State = (uint*)UnsafeUtility.Malloc(BRGConstants.SizeOfUint * currentMaxCount, BRGConstants.AlignOfUint, m_Allocator);
            UnsafeUtility.MemClear(m_State, BRGConstants.SizeOfUint * currentMaxCount);
            UnsafeUtility.MemCpy(m_State, oldBatchLODGroup.m_State, BRGConstants.SizeOfUint * lastMaxCount);

            m_AABBs = (HISMAABB*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<HISMAABB>() * currentMaxCount, UnsafeUtility.AlignOf<HISMAABB>(), m_Allocator);
            UnsafeUtility.MemCpy(m_AABBs, oldBatchLODGroup.m_AABBs, UnsafeUtility.SizeOf<HISMAABB>() * lastMaxCount);

            m_DataBuffer = (float4*)UnsafeUtility.Malloc(BRGConstants.SizeOfFloat4 * m_BufferLength, BRGConstants.AlignOfFloat4, m_Allocator);
            UnsafeUtility.MemClear(m_DataBuffer, BRGConstants.SizeOfFloat4 * currentBufferLength);
            BatchDescription oldBatchDescription = oldBatchLODGroup.m_BatchDescription;
            for (int i = 0, lastDataOffset = 0, newDataOffset = 0; i < oldBatchDescription.MetadataLength; i++)
            {
                MetadataValue oldValue = oldBatchDescription[i];
                MetadataInfo oldInfo = oldBatchDescription.GetMetadataInfo(oldValue.NameID);
                int size = oldInfo.Size;

                UnsafeUtility.MemCpy(m_DataBuffer + newDataOffset, oldBatchLODGroup.m_DataBuffer + lastDataOffset, size * lastMaxCount);

                newDataOffset += size * currentMaxCount / BRGConstants.SizeOfFloat4;
                lastDataOffset += size * lastMaxCount / BRGConstants.SizeOfFloat4;
            }

            uint lodCount = oldBatchLODGroup.LODCount;
            m_BatchLODs = (BatchLOD*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<BatchLOD>() * lodCount, UnsafeUtility.AlignOf<BatchLOD>(), m_Allocator);
            for (uint lodIndex = 0u; lodIndex < lodCount; lodIndex++)
            {
                m_BatchLODs[lodIndex] = new BatchLOD(in oldBatchLODGroup.m_BatchLODs[lodIndex], in newBatchDescription, m_InstanceCount);
            }
        }

        public readonly unsafe NativeArray<float4> GetNativeBuffer()
        {
            NativeArray<float4> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float4>(m_DataBuffer, m_BufferLength, m_Allocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Allocator == Allocator.Temp ? AtomicSafetyHandle.GetTempMemoryHandle() : AtomicSafetyHandle.Create());
#endif
            return array;
        }

        [BurstDiscard]
        public unsafe int Register([NotNull] BatchRendererGroup batchRendererGroup, in GraphicsBufferHandle bufferHandle)
        {
            int totalRegisterBatchCount = 0;
            var metadataValues = m_BatchDescription.AsNativeArray();
            for (uint lodIndex = 0; lodIndex < m_LODCount; lodIndex++)
            {
                if (m_BatchLODs[lodIndex].IsInitialied)
                    totalRegisterBatchCount += m_BatchLODs[lodIndex].Register(batchRendererGroup, in bufferHandle, metadataValues);
            }

            return totalRegisterBatchCount;
        }


        [BurstDiscard]
        public unsafe int Unregister(BatchRendererGroup batchRendererGroup, bool needUnregisterMeshAndMat = true)
        {
            int removeBatchCount = 0;
            for (uint lodIndex = 0; lodIndex < m_LODCount; lodIndex++)
            {
                if (m_BatchLODs[lodIndex].IsInitialied)
                    removeBatchCount += m_BatchLODs[lodIndex].Unregister(batchRendererGroup, needUnregisterMeshAndMat);
            }

            return removeBatchCount;
        }

        public unsafe void SetInstanceCount(int instanceCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (instanceCount < 0 || instanceCount > m_BatchDescription.MaxInstanceCount)
                throw new ArgumentOutOfRangeException($"Instance count {instanceCount} out of range from 0 to {m_BatchDescription.MaxInstanceCount} (include).");
#endif

            Interlocked.Exchange(ref *m_InstanceCount, instanceCount);
        }

        public unsafe void SetActiveCount(int activeCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (activeCount < 0 || activeCount > InstanceCount)
                throw new ArgumentOutOfRangeException($"Active count {activeCount} out of range from 0 to {InstanceCount} (include).");
#endif

            Interlocked.Exchange(ref *m_ActiveCount, activeCount);
        }

        // public unsafe NativeArray<PackedMatrix> GetObjectToWorldArray(Allocator allocator)
        // {
        //     var nativeArray = new NativeArray<PackedMatrix>(InstanceCount, allocator);
        //     var windowCount = this.GetWindowCount();
        //
        //     for (var i = 0; i < windowCount; i++)
        //     {
        //         var instanceCountPerWindow = this.GetInstanceCountPerWindow(i);
        //         var sourceOffset = i * m_BatchDescription.AlignedWindowSize;
        //         var destinationOffset = i * m_BatchDescription.MaxInstancePerWindow * UnsafeUtility.SizeOf<PackedMatrix>();
        //         var size = instanceCountPerWindow * UnsafeUtility.SizeOf<PackedMatrix>();
        //
        //         var sourcePtr = (void*)((IntPtr)m_DataBuffer + sourceOffset);
        //         var destinationPtr = (void*)((IntPtr)nativeArray.GetUnsafePtr() + destinationOffset);
        //
        //         UnsafeUtility.MemCpy(destinationPtr, sourcePtr, size);
        //     }
        //
        //     return nativeArray;
        // }

        public int GetWindowCount()
        {
            return BatchDescription.WindowCount;
        }

        public unsafe void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Allocator == Allocator.Invalid)
                throw new InvalidOperationException($"The {nameof(BatchLODGroup)} can not be Disposed because it was not allocated with a valid allocator.");
            if ((IntPtr)m_InstanceCount == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchLODGroup)} is already disposed");
            if ((IntPtr)m_DataBuffer == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchLODGroup)} is already disposed");
            if ((IntPtr)m_State == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchLODGroup)} is already disposed");
            if ((IntPtr)m_AABBs == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchLODGroup)} is already disposed");
            if ((IntPtr)m_BatchLODs == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchLODGroup)} is already disposed");
            if ((IntPtr)m_ActiveCount == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchLODGroup)} is already disposed");
#endif
            for (int lodIndex = 0; lodIndex < LODCount; lodIndex++)
            {
                m_BatchLODs[lodIndex].Dispose();
            }

            if (m_Allocator > Allocator.None)
            {
                UnsafeUtility.Free(m_InstanceCount, m_Allocator);
                UnsafeUtility.Free(m_DataBuffer, m_Allocator);
                UnsafeUtility.Free(m_State, m_Allocator);
                UnsafeUtility.Free(m_AABBs, m_Allocator);
                UnsafeUtility.Free(m_BatchLODs, m_Allocator);
                UnsafeUtility.Free(m_ActiveCount, m_Allocator);

                m_BatchDescription.Dispose(); // only release here, cannot be release via BatchLOD/BatchGroup
                m_Allocator = Allocator.Invalid;
            }

            m_InstanceCount = null;
            m_DataBuffer = null;
            m_State = null;
            m_AABBs = null;
            m_BatchLODs = null;
            m_ActiveCount = null;
        }

        // @TODO: use jobs to dispose
        public unsafe JobHandle Dispose(JobHandle inputDeps)
        {
            return inputDeps;
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//             if (m_Allocator == Allocator.Invalid)
//                 throw new InvalidOperationException($"The {nameof(BatchGroup)} can not be Disposed because it was not allocated with a valid allocator.");
//             if ((IntPtr)m_DataBuffer == IntPtr.Zero)
//                 throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
//             if ((IntPtr)m_Batches == IntPtr.Zero)
//                 throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
//             if ((IntPtr)m_InstanceCount == IntPtr.Zero)
//                 throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
// #endif
//
//             if (m_Allocator > Allocator.None)
//             {
//                 var disposeData = new BatchGroupDisposeData
//                 {
//                     Buffer = m_DataBuffer,
//                     Batches = m_Batches,
//                     InstanceCount = m_InstanceCount,
//                     AllocatorLabel = m_Allocator,
//                 };
//
//                 var jobHandle = new BatchGroupDisposeJob(ref disposeData).Schedule(inputDeps);
//
//                 m_DataBuffer = null;
//                 m_Batches = null;
//                 m_InstanceCount = null;
//
//                 m_Allocator = Allocator.Invalid;
//                 return JobHandle.CombineDependencies(jobHandle, m_BatchDescription.Dispose(inputDeps), BatchRendererData.Dispose(inputDeps));
//             }
//
//             m_DataBuffer = null;
//             m_Batches = null;
//             m_InstanceCount = null;
//
//             return inputDeps;
        }


        internal unsafe int SetLODRenderDataAndRegister(BatchRendererGroup batchRendererGroup, int lodIndex, GraphicsBufferHandle bufferHandle, in RendererDescription rendererDescription, Mesh mesh, Material[] materials, int subMeshCount)
        {
            NativeArray<MetadataValue> metadataValues = m_BatchDescription.AsNativeArray();
            return m_BatchLODs[lodIndex].SetRenderDataAndRegister(batchRendererGroup, in m_BatchDescription, bufferHandle, metadataValues, in rendererDescription, mesh, materials,
                subMeshCount);
        }

        #region Get/Set State functions

        public unsafe void SetAABB(int index, HISMAABB aabb)
        {
            m_AABBs[index] = aabb;
        }

        public unsafe HISMAABB GetAABB(int index)
        {
            return m_AABBs[index];
        }

        public unsafe HISMAABB* GetAABBPtr(int index)
        {
            return m_AABBs + index;
        }

        public unsafe uint GetCurrentLOD(int index)
        {
            uint savedState = m_State[index];
            uint savedLOD = savedState & s_bLODMask;
            uint lod = (savedLOD >> s_LODOffset);
            return lod;
        }

        public unsafe void SetLOD(int index, uint lod)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index > InstanceCount)
                throw new ArgumentOutOfRangeException($"SetActive index {index} out of range from 0 to {InstanceCount} (include).");
#endif
            uint savedState = m_State[index];
            
            //change active lod
            uint stateWithoutLOD = (savedState & ~s_bLODMask);
            savedState = (stateWithoutLOD | (uint)(lod << s_LODOffset));

            m_State[index] = savedState;
        }

        public unsafe void SetInactive(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index > InstanceCount)
                throw new ArgumentOutOfRangeException($"SetInactive index {index} out of range from 0 to {InstanceCount} (include).");
#endif
            uint savedState = m_State[index];
            uint savedActive = savedState & s_bActiveMask;
            bool bSavedActive = (savedActive == (1u << s_ActiveOffset));
            savedState &= ~(1u << s_ActiveOffset);
            m_State[index] = savedState;

            if (bSavedActive)
                (*m_ActiveCount) -= 1;
        }

        public unsafe void SetActive(int index, uint lod, bool isActive)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (index < 0 || index > InstanceCount)
                throw new ArgumentOutOfRangeException($"SetActive index {index} out of range from 0 to {InstanceCount} (include).");
#endif
            uint savedState = m_State[index];
            uint savedLOD = savedState & s_bLODMask;
            savedLOD = (savedLOD >> s_LODOffset);
            uint savedActive = savedState & s_bActiveMask;
            bool bSavedActive = (savedActive == (1u << s_ActiveOffset));
            // bool isActiveChanged = false;

            if (bSavedActive && isActive)
            {
                if (savedLOD == (uint)lod)
                {
                    // nothing
                }
                else
                {
                    //change active lod
                    uint stateWithoutLOD = (savedState & ~s_bLODMask);
                    savedState = (stateWithoutLOD | (uint)(lod << s_LODOffset));
                }
            }
            else if (!bSavedActive && isActive)
            {
                // Interlocked.Increment(ref *m_ActiveCount);
                (*m_ActiveCount) += 1;
                if (savedLOD == (uint)lod)
                {
                    // set active
                    savedState |= (1u << s_ActiveOffset);
                }
                else
                {
                    // change lod and set active
                    uint stateWithoutLOD = (savedState & ~s_bLODMask);
                    savedState = (stateWithoutLOD | (uint)(lod << s_LODOffset));
                    savedState |= (1u << s_ActiveOffset);
                }
            }
            else if (bSavedActive && !isActive)
            {
                (*m_ActiveCount) -= 1;
                if (savedLOD == (uint)lod)
                {
                    // set inactive
                    savedState &= ~(1u << s_ActiveOffset);
                }
                else
                {
                    // change lod and set inactive
                    uint stateWithoutLOD = (savedState & ~s_bLODMask);
                    savedState = (stateWithoutLOD | (uint)(lod << s_LODOffset));
                    savedState &= ~(1u << s_ActiveOffset);
                }
            }
            else //(!bSavedActive && !isActive)
            {
                if (savedLOD == (uint)lod)
                {
                    // nothing
                }
                else
                {
                    //change lod
                    uint stateWithoutLOD = (savedState & ~s_bLODMask);
                    savedState = (stateWithoutLOD | (uint)(lod << s_LODOffset));
                }
            }

            m_State[index] = savedState;

            // if (isActive)
            // {
            //     uint stateWithoutLOD = (savedState & ~s_bLODMask);
            //     savedState = (stateWithoutLOD | (uint)(lod << s_LODOffset));
            //     savedState |= (1u << s_ActiveOffset);
            // }
            // else if (savedLOD == (uint)lod) // inactive
            // {
            //     savedState &= ~(1u << s_ActiveOffset);
            // }
            // else if (bSavedActive) // set inactive with different lod level
            // {
            //     // nothing
            // }
            // else // set inactive with different lod level
            // {
            //     // nothing
            // }
            //
            // m_State[index] = savedState;
        }

        public unsafe bool IsActive(int index)
        {
            uint savedState = m_State[index];
            uint savedActive = savedState & s_bActiveMask;
            bool bSavedActive = (savedActive == (1u << s_ActiveOffset));
            return bSavedActive;
        }

        public unsafe bool IsActive(int index, uint lod)
        {
            uint savedState = m_State[index];
            uint savedLOD = savedState & s_bLODMask;
            savedLOD = (savedLOD >> s_LODOffset);
            bool savedActive = IsActive(index);

            if (savedLOD == (uint)lod)
            {
                return savedActive;
            }
            else if (savedActive)
            {
                return false;
            }
            else
            {
                return false;
            }
        }

        public unsafe (int, EGetNextActiveIndexInfo) GetNextActiveIndex()
        {
            int index = -1;
            if (ActiveCount >= m_BatchDescription.MaxInstanceCount)
                return (index, EGetNextActiveIndexInfo.NeedResize); // need resize
            else if (ActiveCount == InstanceCount)
                index = ActiveCount; // because index starts at 0 => ActiveCount = index + 1
            else // ActiveCount < InstanceCount
            {
                for (int i = 0; i < InstanceCount; i++)
                {
                    if (!IsActive(i)) return (i, EGetNextActiveIndexInfo.None);
                }
            }

            return (index, EGetNextActiveIndexInfo.NeedExtentInstanceCount);
        }

        #endregion
    }
}