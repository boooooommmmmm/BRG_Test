namespace BRGContainer.Runtime
{
    using System.Runtime.InteropServices;
    using UnityEngine.Rendering;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate bool IsBatchAliveDelegate(ContainerID containerId, BatchID batchID);
}