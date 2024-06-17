﻿namespace BRGContainer.Runtime
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
    [DebuggerDisplay("Count = {Length}, InstanceCount = {InstanceCount}")]
    public struct BatchGroup : INativeDisposable, IEnumerable<BatchID>
    {
        internal BatchDescription m_BatchDescription;

        [NativeDisableUnsafePtrRestriction] private unsafe float4* m_DataBuffer;
        [NativeDisableUnsafePtrRestriction] private unsafe BatchID* m_Batches;
        [NativeDisableUnsafePtrRestriction] internal unsafe int* m_InstanceCount;

        //sven test
        // private unsafe NativeArray<PackedMatrix>* o2wArray;
        private unsafe PackedMatrix* o2wArray;

        public readonly int Length;
        private readonly int m_BufferLength;
        private Allocator m_Allocator;

        public BatchRendererData BatchRendererData;

        public readonly unsafe bool IsCreated => (IntPtr)m_DataBuffer != IntPtr.Zero &&
                                                 (IntPtr)m_Batches != IntPtr.Zero &&
                                                 (IntPtr)m_InstanceCount != IntPtr.Zero;

        public readonly unsafe BatchID this[int index] => m_Batches[index];

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
            Length = m_BatchDescription.WindowCount;

            m_Allocator = allocator;

            m_DataBuffer = (float4*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<float4>() * m_BufferLength,
                UnsafeUtility.AlignOf<float4>(),
                allocator, 0);
            m_Batches = (BatchID*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<BatchID>() * m_BufferLength,
                UnsafeUtility.AlignOf<BatchID>(), allocator, 0);

            m_InstanceCount = (int*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<int>(),
                UnsafeUtility.AlignOf<int>(), allocator, 0);
            UnsafeUtility.MemClear(m_InstanceCount, UnsafeUtility.SizeOf<int>());

            //sven test
            // o2wArray = (NativeArray<PackedMatrix>*)(new NativeArray<PackedMatrix>(m_BufferLength, allocator)).GetUnsafePtr();
            o2wArray = (PackedMatrix*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<PackedMatrix>() * m_BufferLength,
                UnsafeUtility.AlignOf<PackedMatrix>(), allocator, 0);
            UnsafeUtility.MemClear(o2wArray, UnsafeUtility.SizeOf<PackedMatrix>());
        }

        //sven test
        public unsafe NativeArray<PackedMatrix> GetO2WArray()
        {
            var a = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<PackedMatrix>(o2wArray, m_BufferLength, m_Allocator);
            return a;
        }
        
        public unsafe PackedMatrix* GetO2WArrayPtr()
        {
            return o2wArray;
        }
        
        public unsafe void SetO2WMatrix(int index, PackedMatrix matrix)
        {
            // (*o2wArray)[index] = matrix;
            UnsafeUtility.WriteArrayElement(o2wArray, index, matrix);
        }

        public readonly unsafe NativeArray<float4> GetNativeBuffer()
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float4>(m_DataBuffer, m_BufferLength, m_Allocator);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_Allocator == Allocator.Temp ? AtomicSafetyHandle.GetTempMemoryHandle() : AtomicSafetyHandle.Create());
#endif
            return array;
        }

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
        public unsafe void Unregister([NotNull] BatchRendererGroup batchRendererGroup)
        {
            for (var i = 0; i < Length; i++)
            {
                batchRendererGroup.RemoveBatch(m_Batches[i]);
            }

            if (BatchRendererData.MeshID != BatchMeshID.Null)
                batchRendererGroup.UnregisterMesh(BatchRendererData.MeshID);
            if (BatchRendererData.MaterialID != BatchMaterialID.Null)
                batchRendererGroup.UnregisterMaterial(BatchRendererData.MaterialID);
        }

        public unsafe void SetInstanceCount(int instanceCount)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (instanceCount < 0 || instanceCount > m_BatchDescription.MaxInstanceCount)
                throw new ArgumentOutOfRangeException($"Instance count {instanceCount} out of range from 0 to {m_BatchDescription.MaxInstanceCount} (include).");
#endif

            Interlocked.Exchange(ref *m_InstanceCount, instanceCount);
        }

        public unsafe NativeArray<PackedMatrix> GetObjectToWorldArray(Allocator allocator)
        {
            var nativeArray = new NativeArray<PackedMatrix>(InstanceCount, allocator);
            var windowCount = this.GetWindowCount();

            for (var i = 0; i < windowCount; i++)
            {
                var instanceCountPerWindow = this.GetInstanceCountPerWindow(i);
                var sourceOffset = i * m_BatchDescription.AlignedWindowSize;
                var destinationOffset = i * m_BatchDescription.MaxInstancePerWindow * UnsafeUtility.SizeOf<PackedMatrix>();
                var size = instanceCountPerWindow * UnsafeUtility.SizeOf<PackedMatrix>();

                var sourcePtr = (void*)((IntPtr)m_DataBuffer + sourceOffset);
                var destinationPtr = (void*)((IntPtr)nativeArray.GetUnsafePtr() + destinationOffset);

                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, size);
            }

            return nativeArray;
        }

        public readonly unsafe BatchID* GetUnsafePtr()
        {
            return m_Batches;
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
            //sven test
            if ((IntPtr)o2wArray == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
            
#endif

            if (m_Allocator > Allocator.None)
            {
                UnsafeUtility.FreeTracked(m_DataBuffer, m_Allocator);
                UnsafeUtility.FreeTracked(m_Batches, m_Allocator);
                UnsafeUtility.FreeTracked(m_InstanceCount, m_Allocator);

                m_BatchDescription.Dispose();
                BatchRendererData.Dispose();
                
                //sven test
                UnsafeUtility.FreeTracked(o2wArray, m_Allocator);

                m_Allocator = Allocator.Invalid;
            }

            m_DataBuffer = null;
            m_Batches = null;
            m_InstanceCount = null;
            o2wArray = null;//sven test
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
                return m_Index < m_BatchGroup.Length;
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