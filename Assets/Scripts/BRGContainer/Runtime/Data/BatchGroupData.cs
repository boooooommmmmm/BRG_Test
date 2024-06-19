// batch group data holds for main thread.

using Unity.Collections;

namespace BRGContainer.Runtime
{
    public struct BatchGroupData
    {
        private NativeArray<PackedMatrix> m_O2WArray;
        
        public readonly int Length;
        private Allocator m_Allocator;

        public BatchGroupData(int length, Allocator allocator)
        {
            Length = length;
            m_Allocator = allocator;

            m_O2WArray = new NativeArray<PackedMatrix>(length, allocator);
        }
    }
}