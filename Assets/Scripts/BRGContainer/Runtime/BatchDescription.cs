using Unity.Mathematics;

namespace BRGContainer.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine;
    using UnityEngine.Rendering;

    [StructLayout(LayoutKind.Sequential)]
    [DebuggerTypeProxy(typeof(BatchDescriptionDebugView))]
    [DebuggerDisplay("MaxInstancePerWindow = {MaxInstancePerWindow}, WindowCount = {WindowCount}, Length = {MetadataLength}, IsCreated = {IsCreated}")]
    public struct BatchDescription : IEnumerable<MetadataValue>, INativeDisposable
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private static int m_StaticSafetyId;
        
        private AtomicSafetyHandle m_Safety; // this uses only for native array creation without allocations
#endif
        
        private static readonly int m_ObjectToWorldPropertyName = Shader.PropertyToID("unity_ObjectToWorld");
        private static readonly int m_WorldToObjectPropertyName = Shader.PropertyToID("unity_WorldToObject");
        
        private const uint PerInstanceBit = 0x80000000;
        public static readonly bool IsUBO = BatchRendererGroup.BufferTarget == BatchBufferTarget.ConstantBuffer;

        [NativeDisableUnsafePtrRestriction]
        internal unsafe UnsafeList<MetadataValue>* m_MetadataValues;
        [NativeDisableUnsafePtrRestriction]
        internal unsafe UnsafeParallelHashMap<int, MetadataInfo>* m_MetadataInfoMap;
        
        internal Allocator m_Allocator;

        public readonly unsafe MetadataValue this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (*m_MetadataValues)[index];
        }

        public readonly unsafe bool IsCreated => (IntPtr)m_MetadataValues != IntPtr.Zero && 
                                                 (IntPtr)m_MetadataInfoMap != IntPtr.Zero;

        public readonly int MetadataLength;

        public readonly int MaxInstanceCount;
        public readonly int SizePerInstance;

        public readonly int AlignedWindowSize;
        public readonly int MaxInstancePerWindow;
        public readonly int WindowCount;
        public readonly uint WindowSize;
        public readonly int TotalBufferSize;

        // [ExcludeFromBurstCompatTesting("BatchDescription creating is unburstable")]
        public unsafe BatchDescription(ref BatchDescription batchDescription, Allocator allocator, int newSize = -1)
        {
            if (IsUBO)
                throw new Exception("Not support!");
            if (newSize != -1 && (newSize < batchDescription.MaxInstanceCount))
                throw new Exception("Not support!");
            
            // MaxInstanceCount = batchDescription.MaxInstanceCount;
            MaxInstanceCount = newSize;
            m_Allocator = allocator;
            MetadataLength = batchDescription.MetadataLength;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = m_Allocator == Allocator.Temp ? AtomicSafetyHandle.GetTempMemoryHandle() : AtomicSafetyHandle.Create();
            InitStaticSafetyId(ref m_Safety);
            AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, m_StaticSafetyId);
#endif
            
            m_MetadataValues = (UnsafeList<MetadataValue>*) UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<UnsafeList<MetadataValue>>(), UnsafeUtility.AlignOf<UnsafeList<MetadataValue>>(),
                m_Allocator);
            m_MetadataInfoMap = (UnsafeParallelHashMap<int, MetadataInfo>*) UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<UnsafeParallelHashMap<int, MetadataInfo>>(),
                UnsafeUtility.AlignOf<UnsafeParallelHashMap<int, MetadataInfo>>(),
                m_Allocator);
            
            *m_MetadataValues = new UnsafeList<MetadataValue>(MetadataLength, m_Allocator);
            (*m_MetadataValues).CopyFrom(*batchDescription.m_MetadataValues);
            *m_MetadataInfoMap = new UnsafeParallelHashMap<int, MetadataInfo>(MetadataLength, m_Allocator);
            foreach (var pair in *batchDescription.m_MetadataInfoMap)
            {
                (*m_MetadataInfoMap).Add(pair.Key, pair.Value);
            }

            SizePerInstance = batchDescription.SizePerInstance;
            if (newSize == -1)
            {
                AlignedWindowSize = batchDescription.AlignedWindowSize;
                MaxInstancePerWindow = batchDescription.MaxInstancePerWindow;
                WindowCount = batchDescription.WindowCount;
                WindowSize = batchDescription.WindowSize;
                TotalBufferSize = batchDescription.TotalBufferSize;
            }
            else
            {
                AlignedWindowSize = (MaxInstanceCount * SizePerInstance + 15) & -16;;
                MaxInstancePerWindow = MaxInstanceCount;
                WindowCount = 1;
                WindowSize = 0u;
                TotalBufferSize = WindowCount * AlignedWindowSize;
            }
        }

        // [ExcludeFromBurstCompatTesting("BatchDescription creating is unburstable")]
        public unsafe BatchDescription(int maxInstanceCount, Allocator allocator)
        {
            MaxInstanceCount = maxInstanceCount;
            m_Allocator = allocator;
            MetadataLength = 2;
            
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = allocator == Allocator.Temp ? AtomicSafetyHandle.GetTempMemoryHandle() : AtomicSafetyHandle.Create();
            InitStaticSafetyId(ref m_Safety);
            AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, m_StaticSafetyId);
#endif

            m_MetadataValues = (UnsafeList<MetadataValue>*) UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<UnsafeList<MetadataValue>>(), UnsafeUtility.AlignOf<UnsafeList<MetadataValue>>(),
                allocator);
            m_MetadataInfoMap = (UnsafeParallelHashMap<int, MetadataInfo>*) UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<UnsafeParallelHashMap<int, MetadataInfo>>(),
                UnsafeUtility.AlignOf<UnsafeParallelHashMap<int, MetadataInfo>>(),
                allocator);
            
            *m_MetadataValues = new UnsafeList<MetadataValue>(MetadataLength, allocator);
            *m_MetadataInfoMap = new UnsafeParallelHashMap<int, MetadataInfo>(MetadataLength, allocator);

            SizePerInstance = UnsafeUtility.SizeOf<PackedMatrix>() * 2;

            if (IsUBO)
            {
                AlignedWindowSize = BatchRendererGroup.GetConstantBufferMaxWindowSize();
                MaxInstancePerWindow = AlignedWindowSize / SizePerInstance;
                WindowCount = (MaxInstanceCount + MaxInstancePerWindow - 1) / MaxInstancePerWindow;
                WindowSize = (uint) AlignedWindowSize;
                TotalBufferSize = WindowCount * AlignedWindowSize;
            }
            else
            {
                AlignedWindowSize = (MaxInstanceCount * SizePerInstance + 15) & -16;
                MaxInstancePerWindow = MaxInstanceCount;
                WindowCount = 1;
                WindowSize = 0u;
                TotalBufferSize = WindowCount * AlignedWindowSize;
            }

            var metadataOffset = 0;
            RegisterMetadata(UnsafeUtility.SizeOf<PackedMatrix>(), m_ObjectToWorldPropertyName, ref metadataOffset);
            RegisterMetadata(UnsafeUtility.SizeOf<PackedMatrix>(), m_WorldToObjectPropertyName, ref metadataOffset);
        }
        
        // [ExcludeFromBurstCompatTesting("BatchDescription creating is unburstable")]
        public unsafe BatchDescription(int maxInstanceCount, NativeArray<MaterialProperty> materialProperties, Allocator allocator)
        {
            MaxInstanceCount = math.ceilpow2(maxInstanceCount);
            m_Allocator = allocator;
            MetadataLength = materialProperties.Length + 2;
            
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = allocator == Allocator.Temp ? AtomicSafetyHandle.GetTempMemoryHandle() : AtomicSafetyHandle.Create();
            InitStaticSafetyId(ref m_Safety);
            AtomicSafetyHandle.SetStaticSafetyId(ref m_Safety, m_StaticSafetyId);
#endif

            m_MetadataValues = (UnsafeList<MetadataValue>*) UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<UnsafeList<MetadataValue>>(), UnsafeUtility.AlignOf<UnsafeList<MetadataValue>>(),
                allocator);
            m_MetadataInfoMap = (UnsafeParallelHashMap<int, MetadataInfo>*) UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<UnsafeParallelHashMap<int, MetadataInfo>>(),
                UnsafeUtility.AlignOf<UnsafeParallelHashMap<int, MetadataInfo>>(),
                allocator);
            
            *m_MetadataValues = new UnsafeList<MetadataValue>(MetadataLength, allocator);
            *m_MetadataInfoMap = new UnsafeParallelHashMap<int, MetadataInfo>(MetadataLength, allocator);

            SizePerInstance = UnsafeUtility.SizeOf<PackedMatrix>() * 2; //o2w, w2o
            for (var i = 0; i < materialProperties.Length; i++)
            {
                var size = (materialProperties[i].SizeInBytes + 15) & -16;
                SizePerInstance += size;
            }

            if (IsUBO)
            {
                AlignedWindowSize = BatchRendererGroup.GetConstantBufferMaxWindowSize();
                MaxInstancePerWindow = AlignedWindowSize / SizePerInstance;
                WindowCount = (MaxInstanceCount + MaxInstancePerWindow - 1) / MaxInstancePerWindow;
                WindowSize = (uint) AlignedWindowSize;
                TotalBufferSize = WindowCount * AlignedWindowSize;
            }
            else
            {
                AlignedWindowSize = (MaxInstanceCount * SizePerInstance + 15) & -16;
                MaxInstancePerWindow = MaxInstanceCount;
                WindowCount = 1;
                WindowSize = 0u;
                TotalBufferSize = WindowCount * AlignedWindowSize;
            }

            var metadataOffset = 0;
            RegisterMetadata(UnsafeUtility.SizeOf<PackedMatrix>(), m_ObjectToWorldPropertyName, ref metadataOffset);
            RegisterMetadata(UnsafeUtility.SizeOf<PackedMatrix>(), m_WorldToObjectPropertyName, ref metadataOffset);

            for (var i = 0; i < materialProperties.Length; i++)
            {
                var materialProperty = materialProperties[i];
                
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if(m_MetadataInfoMap->ContainsKey(materialProperty.PropertyId))
                    throw new InvalidOperationException($"Property with id {materialProperty.PropertyId} has been registered yet.");
#endif
                var alignedSize = (materialProperty.SizeInBytes + 15) & -16;
                RegisterMetadata(alignedSize, materialProperty.PropertyId, ref metadataOffset, materialProperty.IsPerInstance);
            }
        }

        public readonly unsafe NativeArray<MetadataValue> AsNativeArray()
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<MetadataValue>(m_MetadataValues->Ptr,
                m_MetadataValues->Length, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(m_Safety);
            var arraySafety = m_Safety;
            AtomicSafetyHandle.UseSecondaryVersion(ref arraySafety);
            
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, arraySafety);
#endif
            return array;
        }

        internal readonly unsafe MetadataInfo GetMetadataInfo(int propertyId)
        {
            return (*m_MetadataInfoMap)[propertyId];
        }

        public readonly Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }
        
        IEnumerator<MetadataValue> IEnumerable<MetadataValue>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        // [ExcludeFromBurstCompatTesting("BatchDescription disposing is unburstable")]
        public unsafe void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if(m_Allocator == Allocator.Invalid)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} can not be Disposed because it was not allocated with a valid allocator.");
            if((IntPtr)m_MetadataValues == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
            if((IntPtr)m_MetadataInfoMap == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
#endif
            
            if (m_Allocator > Allocator.None)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.Release(m_Safety);
                m_Safety = default;
#endif
                
                (*m_MetadataValues).Dispose();
                (*m_MetadataInfoMap).Dispose();
                
                UnsafeUtility.Free(m_MetadataValues, m_Allocator);
                UnsafeUtility.Free(m_MetadataInfoMap, m_Allocator);

                m_Allocator = Allocator.Invalid;
            }

            m_MetadataValues = null;
            m_MetadataInfoMap = null;
        }

        // [ExcludeFromBurstCompatTesting("BatchDescription disposing is unburstable")]
        public unsafe JobHandle Dispose(JobHandle inputDeps)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if(m_Allocator == Allocator.Invalid)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} can not be Disposed because it was not allocated with a valid allocator.");
            if((IntPtr)m_MetadataValues == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
            if((IntPtr)m_MetadataInfoMap == IntPtr.Zero)
                throw new InvalidOperationException($"The {nameof(BatchGroup)} is already disposed");
#endif
            
            if (m_Allocator > Allocator.None)
            {
                var disposeHandle = JobHandle.CombineDependencies((*m_MetadataValues).Dispose(inputDeps),
                    (*m_MetadataInfoMap).Dispose(inputDeps));
                
                var disposeData = new BatchDescriptionDisposeData
                {
                    MetadataValues = m_MetadataValues,
                    MetadataInfoMap = m_MetadataInfoMap,
                    AllocatorLabel = m_Allocator,
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_Safety = m_Safety
#endif
                };
                
                var jobHandle = new BatchDescriptionDisposeJob(ref disposeData).Schedule(disposeHandle);
                
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.Release(m_Safety);
                m_Safety = default;
#endif
                
                m_MetadataValues = null;
                m_MetadataInfoMap = null;
                m_Allocator = Allocator.Invalid;
                
                return jobHandle;
            }

            m_MetadataValues = null;
            m_MetadataInfoMap = null;
            
            return inputDeps;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BatchDescription CopyFrom(ref BatchDescription batchDescription, Allocator allocator)
        {
            return new BatchDescription(ref batchDescription, allocator);
        }
        
        public static BatchDescription CopyWithResize(ref BatchDescription batchDescription, int newSize)
        {
            return new BatchDescription(ref batchDescription, batchDescription.m_Allocator, newSize);
        }
        
        private unsafe void RegisterMetadata(int sizeInBytes, int propertyId, ref int metadataOffset, bool isPerInstance = true)
        {
            var metadataInfo = new MetadataInfo(sizeInBytes, metadataOffset, propertyId, isPerInstance);
            var metadataValue = new MetadataValue
            {
                NameID = propertyId,
                Value = (uint)metadataOffset | (isPerInstance ? PerInstanceBit : 0u)
            };
            
            m_MetadataValues->Add(metadataValue);
            m_MetadataInfoMap->Add(propertyId, metadataInfo);

            metadataOffset += sizeInBytes * MaxInstancePerWindow;
        }
        
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [BurstDiscard]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        private static void InitStaticSafetyId(ref AtomicSafetyHandle handle)
        {
            if(m_StaticSafetyId == 0)
                m_StaticSafetyId = AtomicSafetyHandle.NewStaticSafetyId<BatchDescription>();
                
            AtomicSafetyHandle.SetStaticSafetyId(ref handle, m_StaticSafetyId);
        }
#endif
        
        public struct Enumerator : IEnumerator<MetadataValue>
        {
            private readonly BatchDescription m_BatchDescription;
            private int m_Index;

            public MetadataValue Current => m_BatchDescription[m_Index];

            object IEnumerator.Current => Current;

            public Enumerator(BatchDescription batchDescription)
            {
                m_BatchDescription = batchDescription;
                m_Index = -1;
            }
            
            public bool MoveNext()
            {
                ++m_Index;
                return m_Index < m_BatchDescription.MetadataLength;
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