namespace BRGContainer.Runtime
{
    using System.Runtime.InteropServices;
    using Unity.Collections.LowLevel.Unsafe;

    [StructLayout(LayoutKind.Sequential)]
    internal struct BatchInstanceData
    {
        public int InstanceCount;
        [NativeDisableUnsafePtrRestriction]
        public unsafe int* Indices;
    }
}