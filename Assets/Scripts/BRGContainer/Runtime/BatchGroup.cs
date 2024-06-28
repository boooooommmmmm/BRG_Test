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
        private static int s_SizeOfBool = UnsafeUtility.SizeOf<bool>();
        private static int s_SizeOfInt = UnsafeUtility.SizeOf<int>();
        private static int s_SizeOfUint = UnsafeUtility.SizeOf<uint>();
        private static int s_SizeOfFloat3 = UnsafeUtility.SizeOf<float3>();
        private static int s_SizeOfFloat4 = UnsafeUtility.SizeOf<float4>();
        private static int s_SizeOfBatchID = UnsafeUtility.SizeOf<BatchID>();

        internal BatchDescription m_BatchDescription;

        [NativeDisableUnsafePtrRestriction] private unsafe float4* m_DataBuffer;
        [NativeDisableUnsafePtrRestriction] private unsafe BatchID* m_Batches; // for support multiple windows
        [NativeDisableUnsafePtrRestriction] internal unsafe int* m_InstanceCount;

        public readonly int WindowCount;
        private readonly int m_BufferLength;
        private Allocator m_Allocator;

        // Persistent buffer for acceleration
        [NativeDisableUnsafePtrRestriction] private unsafe float3* m_Positions;
        [NativeDisableUnsafePtrRestriction] private unsafe int* m_Visibles;
        [NativeDisableUnsafePtrRestriction] private unsafe uint* m_State; // 0: alive
        [NativeDisableUnsafePtrRestriction] private unsafe bool* m_Alives;
        [NativeDisableUnsafePtrRestriction] internal unsafe int* m_AliveCount; //

        public BatchRendererData BatchRendererData;

        // public readonly unsafe bool IsCreated => (IntPtr)m_DataBuffer != IntPtr.Zero &&
        //                                          (IntPtr)m_Batches != IntPtr.Zero &&
        //                                          (IntPtr)m_InstanceCount != IntPtr.Zero;

        public readonly unsafe BatchID this[int index] => m_Batches[index];

        public readonly unsafe float3* PositionsPtr => m_Positions;
        public readonly unsafe int* VisiblesPtr => m_Visibles;
        public readonly unsafe uint* StatePtr => m_State;

        public readonly unsafe int InstanceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *m_InstanceCount;
        }

        public readonly unsafe int AliveCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *m_AliveCount;
        }

        public unsafe BatchGroup(ref BatchDescription batchDescription, in BatchRendererData rendererData, Allocator allocator)
        {
            m_BatchDescription = batchDescription;
            BatchRendererData = rendererData;
            m_Allocator = allocator;

            m_BufferLength = m_BatchDescription.TotalBufferSize / 16;
            WindowCount = m_BatchDescription.WindowCount;

            m_DataBuffer = (float4*)UnsafeUtility.Malloc(s_SizeOfFloat4 * m_BufferLength,
                UnsafeUtility.AlignOf<float4>(), m_Allocator);
            m_Batches = (BatchID*)UnsafeUtility.Malloc(s_SizeOfBatchID * m_BufferLength,
                UnsafeUtility.AlignOf<BatchID>(), m_Allocator);

            m_InstanceCount = (int*)UnsafeUtility.Malloc(s_SizeOfInt, UnsafeUtility.AlignOf<int>(), m_Allocator);
            UnsafeUtility.MemClear(m_InstanceCount, UnsafeUtility.SizeOf<int>());
            m_AliveCount = (int*)UnsafeUtility.Malloc(s_SizeOfInt, UnsafeUtility.AlignOf<int>(), m_Allocator);
            UnsafeUtility.MemClear(m_AliveCount, UnsafeUtility.SizeOf<int>());

            // persistent buffers
            int maxCount = m_BatchDescription.MaxInstanceCount;
            m_Positions = (float3*)UnsafeUtility.Malloc(s_SizeOfFloat3 * maxCount, UnsafeUtility.AlignOf<float3>(), m_Allocator);
            m_Visibles = (int*)UnsafeUtility.Malloc(s_SizeOfInt * maxCount, UnsafeUtility.AlignOf<int>(), m_Allocator);
            m_State = (uint*)UnsafeUtility.Malloc(s_SizeOfUint * maxCount, UnsafeUtility.AlignOf<uint>(), m_Allocator);
            m_Alives = (bool*)UnsafeUtility.Malloc(s_SizeOfBool * maxCount, UnsafeUtility.AlignOf<bool>(), m_Allocator);
            UnsafeUtility.MemClear(m_Alives, s_SizeOfBool * maxCount);
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
        //     m_Positions = (float3*)UnsafeUtility.Malloc(s_SizeOfFloat3 * currentMaxCount, UnsafeUtility.AlignOf<float3>(), m_Allocator);
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
        //     UnsafeUtility.MemCpy(m_Positions, _batchGroup.m_Positions, s_SizeOfFloat3 * lastMaxCount);
        //     UnsafeUtility.MemCpy(m_Visibles, _batchGroup.m_Visibles, s_SizeOfInt * lastMaxCount);
        //     UnsafeUtility.MemCpy(m_State, _batchGroup.m_State, s_SizeOfUint * lastMaxCount);
        //     UnsafeUtility.MemCpy(m_Alives, _batchGroup.m_Alives, s_SizeOfBool * lastMaxCount);
        //
        //     // _batchGroup.Dispose();
        // }

        public readonly unsafe NativeArray<float4> GetNativeBuffer()
        {
            NativeArray<float4> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float4>(m_DataBuffer, m_BufferLength, m_Allocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Allocator == Allocator.Temp ? AtomicSafetyHandle.GetTempMemoryHandle() : AtomicSafetyHandle.Create());
#endif
            return array;
        }

        public BatchDescription GetBatchDescription()
        {
            return m_BatchDescription;
        }

        public void SetBachDescription(BatchDescription batchDescription)
        {
            m_BatchDescription = batchDescription;
        }

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
            }
        }

        public unsafe void SetInstanceCount(int instanceCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (instanceCount < 0 || instanceCount > m_BatchDescription.MaxInstanceCount)
                throw new ArgumentOutOfRangeException($"Instance count {instanceCount} out of range from 0 to {m_BatchDescription.MaxInstanceCount} (include).");
#endif

            Interlocked.Exchange(ref *m_InstanceCount, instanceCount);
        }
        
        public unsafe void SetAliveCount(int aliveCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (aliveCount < 0 || aliveCount > InstanceCount)
                throw new ArgumentOutOfRangeException($"Alive count {aliveCount} out of range from 0 to {InstanceCount} (include).");
#endif

            Interlocked.Exchange(ref *m_AliveCount, aliveCount);
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


        public unsafe void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Allocator == Allocator.Invalid)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} can not be Disposed because it was not allocated with a valid allocator.");
            if ((IntPtr)m_DataBuffer == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
            if ((IntPtr)m_Batches == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
            if ((IntPtr)m_InstanceCount == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
            if ((IntPtr)m_AliveCount == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
#endif

            if (m_Allocator > Allocator.None)
            {
                UnsafeUtility.Free(m_DataBuffer, m_Allocator);
                UnsafeUtility.Free(m_Batches, m_Allocator);
                UnsafeUtility.Free(m_InstanceCount, m_Allocator);
                UnsafeUtility.Free(m_AliveCount, m_Allocator);
                // persistent buffers
                UnsafeUtility.Free(m_Positions, m_Allocator);
                UnsafeUtility.Free(m_Visibles, m_Allocator);
                UnsafeUtility.Free(m_State, m_Allocator);
                UnsafeUtility.Free(m_Alives, m_Allocator);

                m_BatchDescription.Dispose();
                BatchRendererData.Dispose();

                m_Allocator = Allocator.Invalid;
            }

            m_DataBuffer = null;
            m_Batches = null;
            m_InstanceCount = null;
            m_AliveCount = null;
        }

        // @TODO:
        public unsafe JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (m_Allocator == Allocator.Invalid)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} can not be Disposed because it was not allocated with a valid allocator.");
            if ((IntPtr)m_DataBuffer == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
            if ((IntPtr)m_Batches == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
            if ((IntPtr)m_InstanceCount == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
#endif

            if (m_Allocator > Allocator.None)
            {
                var disposeData = new BatchGroupDisposeData
                {
                    Buffer = m_DataBuffer,
                    Batches = m_Batches,
                    InstanceCount = m_InstanceCount,
                    AllocatorLabel = m_Allocator,
                };

                var jobHandle = new BatchGroupDisposeJob(ref disposeData).Schedule(inputDeps);

                m_DataBuffer = null;
                m_Batches = null;
                m_InstanceCount = null;

                m_Allocator = Allocator.Invalid;
                return JobHandle.CombineDependencies(jobHandle, m_BatchDescription.Dispose(inputDeps), BatchRendererData.Dispose(inputDeps));
            }

            m_DataBuffer = null;
            m_Batches = null;
            m_InstanceCount = null;

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