namespace BRGContainer.Runtime
{
    using System.Linq;
    using UnityEngine;
    using UnityEngine.Rendering;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;

    [StructLayout(LayoutKind.Sequential)]
    public struct BatchRendererData : INativeDisposable
    {
        public BatchMeshID MeshID;
        public BatchMaterialID MaterialID;
        public uint SubMeshIndex;
        public RendererDescription RendererDescription;
        
        public BatchRendererData(in RendererDescription description, in BatchMeshID meshID, in BatchMaterialID materialID, uint submeshIndex = 0)
        {
            MeshID = meshID;
            MaterialID = materialID;
            SubMeshIndex = submeshIndex;
            RendererDescription = description;
        }

        public void Dispose()
        {
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            return new JobHandle();
        }
    }
}