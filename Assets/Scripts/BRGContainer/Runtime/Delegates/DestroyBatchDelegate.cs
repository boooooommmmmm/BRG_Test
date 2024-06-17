namespace BRGContainer.Runtime
{
    using System.Runtime.InteropServices;
    using UnityEngine.Rendering;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void DestroyBatchDelegate(ContainerID containerId, BatchID batchId);
}