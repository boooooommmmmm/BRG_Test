using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace BRGContainer.Runtime
{
    public static class BRGConstants
    {
        internal static int SizeOfBool = UnsafeUtility.SizeOf<bool>();
        internal static int SizeOfInt = UnsafeUtility.SizeOf<int>();
        internal static int SizeOfUint = UnsafeUtility.SizeOf<uint>();
        internal static int SizeOfFloat3 = UnsafeUtility.SizeOf<float3>();
        internal static int SizeOfFloat4 = UnsafeUtility.SizeOf<float4>();
        internal static int SizeOfBatchID = UnsafeUtility.SizeOf<BatchID>();
        internal static int SizeOfBatchGroupID = UnsafeUtility.SizeOf<BatchLODGroupID>();
        internal static int SizeOfBatchGroup = UnsafeUtility.SizeOf<BatchGroup>();
        

        internal static int AlignOfInt = UnsafeUtility.AlignOf<int>();
        internal static int AlignOfUint = UnsafeUtility.AlignOf<uint>();
        internal static int AlignOfFloat4 = UnsafeUtility.AlignOf<float4>();
        internal static int AlignOfBatchID = UnsafeUtility.AlignOf<BatchID>();
        internal static int AlignOfBatchLODGroupID = UnsafeUtility.AlignOf<BatchLODGroupID>();
        internal static int AlignOfBatchGroup = UnsafeUtility.AlignOf<BatchGroup>();
    }
}