using Unity.Collections;

namespace BRGContainer.Runtime
{
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;

    [StructLayout(LayoutKind.Sequential)]
    internal struct BatchInstanceData
    {
        public int VisibleInstanceCount;
        [NativeDisableUnsafePtrRestriction]
        // public unsafe int* Indices;
        public unsafe NativeArray<int> Indices; //sven test
    }
}