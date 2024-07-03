using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace BRGContainer.Runtime
{
    public partial class BRGContainer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static GraphicsBuffer CreateGraphicsBuffer(bool isUbo, int bufferSize)
        {
            var target = isUbo ? GraphicsBuffer.Target.Constant : GraphicsBuffer.Target.Raw;
            var count = isUbo ? bufferSize / 16 : bufferSize / 4;
            var stride = isUbo ? 16 : 4;

            return new GraphicsBuffer(target, count, stride);
        }

        internal unsafe BatchRendererData CreateRendererData(in RendererDescription description, Mesh mesh, uint submeshIndex, Material material)
        {
            var meshID = m_BatchRendererGroup.RegisterMesh(mesh);
            var materialID = m_BatchRendererGroup.RegisterMaterial(material);
            var batchRendererData = new BatchRendererData(description, meshID, materialID, submeshIndex);

            return batchRendererData;
        }

        [BRGMethodThreadSafe]
        private unsafe void GetNewBatchLODGroupID(out BatchLODGroupID batchLODGroupID)
        {
            batchLODGroupID = new BatchLODGroupID(Interlocked.Increment(ref m_BatchLODGroupGlobalID));
        }

        private unsafe BatchLODGroup CreateBatchLODGroup(in BatchDescription batchDescription, in RendererDescription rendererDescription, in BatchLODGroupID batchLODGroupID, ref BatchWorldObjectData worldObjectData, Allocator allocator)
        {
            RegisterMeshAndMaterialToBRG(ref worldObjectData);
            return new BatchLODGroup(this, in batchDescription, in rendererDescription, in batchLODGroupID, in worldObjectData, allocator);
        }

        private void RegisterMeshAndMaterialToBRG(ref BatchWorldObjectData worldObjectData)
        {
            for (uint lodIndex = 0u; lodIndex < worldObjectData.LODCount; lodIndex++)
            {
                BatchWorldObjectLODData worldObjectLODData = worldObjectData[lodIndex];
                for (uint submeshIndex = 0u; submeshIndex < worldObjectLODData.SubmeshCount; submeshIndex++)
                {
                    BatchWorldObjectSubMeshData newWorldObjectSubMeshData = worldObjectLODData[submeshIndex]; // copy ctor
                    BatchRendererData newBatchRendererData = newWorldObjectSubMeshData.m_RendererData; // copy ctor
                    newBatchRendererData.MaterialID = m_BatchRendererGroup.RegisterMaterial(newWorldObjectSubMeshData.m_Material);
                    newBatchRendererData.MeshID = m_BatchRendererGroup.RegisterMesh(newWorldObjectSubMeshData.m_Mesh);
                    newWorldObjectSubMeshData.m_RendererData = newBatchRendererData; // overwrite
                    worldObjectLODData[submeshIndex] = newWorldObjectSubMeshData; // overwrite
                }

                worldObjectData[lodIndex] = worldObjectLODData; // overwrite
            }
        }
    }
}