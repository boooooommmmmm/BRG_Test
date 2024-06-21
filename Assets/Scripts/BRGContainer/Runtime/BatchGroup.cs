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
        private static int s_SizeOfInt = UnsafeUtility.SizeOf<int>();
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
        // [NativeDisableUnsafePtrRestriction] private unsafe int* m_State;

        public BatchRendererData BatchRendererData;

        // public readonly unsafe bool IsCreated => (IntPtr)m_DataBuffer != IntPtr.Zero &&
        //                                          (IntPtr)m_Batches != IntPtr.Zero &&
        //                                          (IntPtr)m_InstanceCount != IntPtr.Zero;

        public readonly unsafe BatchID this[int index] => m_Batches[index];

        public readonly unsafe float3* PositionsPtr => m_Positions;
        public readonly unsafe int* VisiblesPtr => m_Visibles;

        public readonly unsafe int InstanceCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *m_InstanceCount;
        }

        public unsafe BatchGroup(ref BatchDescription batchDescription, in BatchRendererData rendererData, Allocator allocator)
        {
            m_BatchDescription = batchDescription;
            BatchRendererData = rendererData;

            m_BufferLength = m_BatchDescription.TotalBufferSize / 16;
            WindowCount = m_BatchDescription.WindowCount;

            m_Allocator = allocator;

            m_DataBuffer = (float4*)UnsafeUtility.Malloc(s_SizeOfFloat4 * m_BufferLength,
                UnsafeUtility.AlignOf<float4>(), allocator);
            m_Batches = (BatchID*)UnsafeUtility.Malloc(s_SizeOfBatchID * m_BufferLength,
                UnsafeUtility.AlignOf<BatchID>(), allocator);

            m_InstanceCount = (int*)UnsafeUtility.Malloc(s_SizeOfInt, UnsafeUtility.AlignOf<int>(), allocator);
            UnsafeUtility.MemClear(m_InstanceCount, UnsafeUtility.SizeOf<int>());
            
            // persistent buffers
            m_Positions = (float3*)UnsafeUtility.Malloc(s_SizeOfFloat3 * m_BatchDescription.MaxInstanceCount,
                UnsafeUtility.AlignOf<float3>(), allocator);
            m_Visibles = (int*)UnsafeUtility.Malloc(s_SizeOfInt * m_BatchDescription.MaxInstanceCount,
                UnsafeUtility.AlignOf<int>(), allocator);
        }

        // copy ctor for resize batchGroup
        public unsafe BatchGroup(ref BatchGroup _batchGroup, ref BatchDescription description)
        {
            m_BatchDescription = description;
            // BatchRendererData = batchRendererData;
            BatchRendererData = _batchGroup.BatchRendererData;
            m_BufferLength = m_BatchDescription.TotalBufferSize / 16;
            WindowCount = m_BatchDescription.WindowCount;
            
            m_Allocator = _batchGroup.m_Allocator;
            
            m_InstanceCount = (int*)UnsafeUtility.Malloc(s_SizeOfInt, UnsafeUtility.AlignOf<int>(), m_Allocator);
            UnsafeUtility.MemClear(m_InstanceCount, UnsafeUtility.SizeOf<int>());
            
            // resize buffers
            int lastMaxCount = _batchGroup.m_BatchDescription.MaxInstanceCount;
            int lastBufferLength = _batchGroup.m_BufferLength;
            int currentMaxCount = m_BatchDescription.MaxInstanceCount;
            int currentBufferLength = m_BufferLength;
            m_DataBuffer = (float4*)UnsafeUtility.Malloc(s_SizeOfFloat4 * m_BufferLength,
                UnsafeUtility.AlignOf<float4>(), m_Allocator);
            UnsafeUtility.MemClear(m_DataBuffer, UnsafeUtility.SizeOf<float4>() * currentBufferLength);
            m_Batches = (BatchID*)UnsafeUtility.Malloc(s_SizeOfBatchID * m_BufferLength,
                UnsafeUtility.AlignOf<BatchID>(), m_Allocator);
            m_Positions = (float3*)UnsafeUtility.Malloc(s_SizeOfFloat3 * m_BatchDescription.MaxInstanceCount,
                UnsafeUtility.AlignOf<float3>(), m_Allocator);
            m_Visibles = (int*)UnsafeUtility.Malloc(s_SizeOfInt * m_BatchDescription.MaxInstanceCount,
                UnsafeUtility.AlignOf<int>(), m_Allocator);


            // UnsafeUtility.MemCpy(m_DataBuffer, _batchGroup.m_DataBuffer, s_SizeOfFloat4 * lastMaxCount * 7);
            // UnsafeUtility.MemCpy(m_DataBuffer + currentMaxCount * s_SizeOfFloat4 * 3, _batchGroup.m_DataBuffer + lastMaxCount * s_SizeOfFloat4 * 3, lastMaxCount * s_SizeOfFloat4 * 3);
            // UnsafeUtility.MemCpy(m_DataBuffer + currentMaxCount * 2 * s_SizeOfFloat4 * 3, _batchGroup.m_DataBuffer + lastMaxCount * 2 * s_SizeOfFloat4 * 3, lastMaxCount * s_SizeOfFloat4 * 1);
            UnsafeUtility.MemMove(m_DataBuffer, _batchGroup.m_DataBuffer, s_SizeOfFloat4 * lastBufferLength);
            UnsafeUtility.MemMove(m_Batches, _batchGroup.m_Batches, s_SizeOfBatchID * lastBufferLength);
            UnsafeUtility.MemMove(m_Positions, _batchGroup.m_Positions, s_SizeOfFloat3 * lastMaxCount);
            UnsafeUtility.MemMove(m_Visibles, _batchGroup.m_Visibles, s_SizeOfInt * lastMaxCount);

            // _batchGroup.Dispose();
        }

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


        // public unsafe void Resize(int targetInstanceCount)
        // {
        //     int lastMaxCount = m_BatchDescription.MaxInstanceCount;
        //     int lastBufferLength = m_BufferLength;
        //     m_BatchDescription = BatchDescription.CopyWithResize(ref m_BatchDescription, targetInstanceCount);
        //     m_BufferLength = m_BatchDescription.TotalBufferSize / 16;
        //     WindowCount = m_BatchDescription.WindowCount;
        //     
        //     // resize buffers
        //     var tempDataBuffer = (float4*)UnsafeUtility.Malloc(s_SizeOfFloat4 * m_BufferLength,
        //         UnsafeUtility.AlignOf<float4>(), m_Allocator);
        //     var tempBatches = (BatchID*)UnsafeUtility.Malloc(s_SizeOfBatchID * m_BufferLength,
        //         UnsafeUtility.AlignOf<BatchID>(), m_Allocator);
        //     var tempPositions = (float3*)UnsafeUtility.Malloc(s_SizeOfFloat3 * m_BatchDescription.MaxInstanceCount,
        //         UnsafeUtility.AlignOf<float3>(), m_Allocator);
        //     var tempVisibles = (int*)UnsafeUtility.Malloc(s_SizeOfInt * m_BatchDescription.MaxInstanceCount,
        //         UnsafeUtility.AlignOf<int>(), m_Allocator);
        //     
        //     UnsafeUtility.MemMove(tempDataBuffer, m_DataBuffer, s_SizeOfFloat4 * lastBufferLength);
        //     UnsafeUtility.MemMove(tempBatches, m_Batches, s_SizeOfBatchID * lastBufferLength);
        //     UnsafeUtility.MemMove(tempPositions, m_Positions, s_SizeOfFloat3 * lastMaxCount);
        //     UnsafeUtility.MemMove(tempVisibles, m_Visibles, s_SizeOfInt * lastMaxCount);
        //
        //     m_DataBuffer = tempDataBuffer;
        //     m_Batches = tempBatches;
        //     m_Positions = tempPositions;
        //     m_Visibles = tempVisibles;
        //
        //     UnsafeUtility.Free(m_DataBuffer, m_Allocator);
        //     UnsafeUtility.Free(m_Batches, m_Allocator);
        //     UnsafeUtility.Free(m_Positions, m_Allocator);
        //     UnsafeUtility.Free(m_Visibles, m_Allocator);
        // }

        [BurstDiscard]
        public unsafe void Register([NotNull] BatchRendererGroup batchRendererGroup, GraphicsBufferHandle bufferHandle)
        {
            var metadataValues = m_BatchDescription.AsNativeArray();
            for (var i = 0; i < m_BatchDescription.WindowCount; i++)
            {
                var offset = (uint)(i * m_BatchDescription.AlignedWindowSize);
                var batchId = batchRendererGroup.AddBatch(metadataValues, bufferHandle, offset, m_BatchDescription.WindowSize);
                m_Batches[i] = batchId;
            }
        }

        [BurstDiscard]
        public unsafe void Unregister([NotNull] BatchRendererGroup batchRendererGroup,  bool onlyRemoveBatch)
        {
            for (var i = 0; i < WindowCount; i++)
            {
                batchRendererGroup.RemoveBatch(m_Batches[i]);
            }

            if (!onlyRemoveBatch)
            {
                if (BatchRendererData.MeshID != BatchMeshID.Null)
                    batchRendererGroup.UnregisterMesh(BatchRendererData.MeshID);
                if (BatchRendererData.MaterialID != BatchMaterialID.Null)
                    batchRendererGroup.UnregisterMaterial(BatchRendererData.MaterialID);
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

        public readonly unsafe BatchID* GetUnsafePtr()
        {
            return m_Batches;
        }

        [BRGMethodThreadUnsafe]
        public unsafe void SetPosition(int index, float3 position)
        {
            m_Positions[index] = position;
        }

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
#endif

            if (m_Allocator > Allocator.None)
            {
                UnsafeUtility.Free(m_DataBuffer, m_Allocator);
                UnsafeUtility.Free(m_Batches, m_Allocator);
                UnsafeUtility.Free(m_InstanceCount, m_Allocator);
                // persistent buffers
                UnsafeUtility.Free(m_Positions, m_Allocator);
                UnsafeUtility.Free(m_Visibles, m_Allocator);

                m_BatchDescription.Dispose();
                BatchRendererData.Dispose();
                
                m_Allocator = Allocator.Invalid;
            }

            m_DataBuffer = null;
            m_Batches = null;
            m_InstanceCount = null;
        }

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
    }
}