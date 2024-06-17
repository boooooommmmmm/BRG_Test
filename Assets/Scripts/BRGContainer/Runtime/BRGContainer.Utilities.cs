using System.Runtime.CompilerServices;
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

        private BatchGroup CreateBatchGroup(ref BatchDescription batchDescription, ref BatchRendererData rendererData, GraphicsBufferHandle graphicsBufferHandle,
            Allocator allocator)
        {
            var batchGroup = new BatchGroup(ref batchDescription, rendererData, allocator);
            batchGroup.Register(m_BatchRendererGroup, graphicsBufferHandle);

            return batchGroup;
        }

        private unsafe BatchRendererData CreateRendererData(in RendererDescription description, Mesh mesh, uint submeshIndex, Material material)
        {
            var meshID = m_BatchRendererGroup.RegisterMesh(mesh);
            var materialID = m_BatchRendererGroup.RegisterMaterial(material);
            var batchRendererData = new BatchRendererData(description, meshID, materialID, submeshIndex);

            return batchRendererData;
        }
    }
}