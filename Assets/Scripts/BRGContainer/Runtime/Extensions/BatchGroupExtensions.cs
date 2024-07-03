namespace BRGContainer.Runtime
{
    using System.Runtime.CompilerServices;
    
    public static class BatchGroupExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWindowCount(this BatchGroup batchGroup)
        {
            return 1;
            // return GetWindowCount(batchGroup, batchGroup.InstanceCount);
        }

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public static int GetWindowCount(this BatchGroup batchGroup, int instanceCount)
        // {
        //     var description = batchGroup.m_BatchDescription;
        //     return (instanceCount + description.MaxInstancePerWindow - 1) /
        //            description.MaxInstancePerWindow;
        // }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetInstanceCountPerWindow(this BatchLODGroup batchLODGroup, int subBatchIndex)
        {
            var description = batchLODGroup.m_BatchDescription;
            return description.MaxInstancePerWindow;
            
            // var description = batchGroup.m_BatchDescription;
            // var batchCount = GetWindowCount(batchGroup);
            // if (subBatchIndex >= batchCount)
            //     return 0;
            //
            // if (subBatchIndex == batchCount - 1)
            //     return description.MaxInstancePerWindow - (batchCount * description.MaxInstancePerWindow - batchGroup.InstanceCount);
            //
            // return description.MaxInstancePerWindow;
        }
    }
}