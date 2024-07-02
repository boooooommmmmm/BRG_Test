using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace BRGContainer.Runtime
{
    public static class BRGConstants
    {
        internal static readonly uint MaxLODCount = 3u;
        
        internal static readonly int SizeOfBool = UnsafeUtility.SizeOf<bool>();
        internal static readonly int SizeOfInt = UnsafeUtility.SizeOf<int>();
        internal static readonly int SizeOfUint = UnsafeUtility.SizeOf<uint>();
        internal static readonly int SizeOfFloat3 = UnsafeUtility.SizeOf<float3>();
        internal static readonly int SizeOfFloat4 = UnsafeUtility.SizeOf<float4>();
        internal static readonly int SizeOfBatchID = UnsafeUtility.SizeOf<BatchID>();
        internal static readonly int SizeOfBatchGroupID = UnsafeUtility.SizeOf<BatchLODGroupID>();
        internal static readonly int SizeOfBatchGroup = UnsafeUtility.SizeOf<BatchGroup>();

        internal static readonly int AlignOfInt = UnsafeUtility.AlignOf<int>();
        internal static readonly int AlignOfUint = UnsafeUtility.AlignOf<uint>();
        internal static readonly int AlignOfFloat4 = UnsafeUtility.AlignOf<float4>();
        internal static readonly int AlignOfBatchID = UnsafeUtility.AlignOf<BatchID>();
        internal static readonly int AlignOfBatchLODGroupID = UnsafeUtility.AlignOf<BatchLODGroupID>();
        internal static readonly int AlignOfBatchGroup = UnsafeUtility.AlignOf<BatchGroup>();
    }
}